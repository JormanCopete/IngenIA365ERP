using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Checks;

// Feature 015, R2 (fase 37, T0428; FR-084, FR-087; US15): el canje completo de los cheques recibidos. Los cheques de la remesa del día a una cuenta propia se arman en un
// lote de canje (pasan a «en canje»); una persona cruza el abono del extracto que los paga —el lote entero o una parte— y quedan pagados, sin registrarlo cheque por cheque;
// el que el banco devuelve sigue la devolución de FR-060, que reversa sus porciones. El abono queda identificado en Recaudo para que no vuelva a identificarse.

/// <summary>Un lote de canje, para la pantalla y el informe. (nuevo)</summary>
public sealed record ClearingBatchDto(Guid PublicId, string Number, Guid OwnBankAccountPublicId, DateOnly DepositedOn, int CheckCount, decimal TotalAmount, decimal PaidAmount,
    decimal ReturnedAmount, decimal InClearingAmount, ClearingBatchState Status, IReadOnlyList<ReceivedCheckDto> Checks);

/// <summary>Lo común del canje: el número, el estado del lote por sus cheques y el DTO. (nuevo)</summary>
public static class CanjeDeCheques
{
    /// <summary>La clase de numeración de los lotes de canje (<c>RCD:CN</c>).</summary>
    public const string Codigo = "CN";

    /// <summary>Recalcula lo pagado, lo devuelto y el estado del lote por el estado de sus cheques (ya seguidos).</summary>
    public static void Recalcular(ClearingBatch lote, IReadOnlyCollection<ReceivedCheck> cheques)
    {
        lote.PaidAmount = cheques.Where(c => c.Status == ReceivedCheckState.Paid).Sum(c => c.Amount);
        lote.ReturnedAmount = cheques.Where(c => c.Status == ReceivedCheckState.Returned).Sum(c => c.Amount);
        var enCanje = cheques.Any(c => c.Status is ReceivedCheckState.InClearing or ReceivedCheckState.Deposited);
        lote.Status = enCanje
            ? lote.PaidAmount > 0m || lote.ReturnedAmount > 0m ? ClearingBatchState.PartiallyPaid : ClearingBatchState.InClearing
            : lote.ReturnedAmount > 0m ? ClearingBatchState.Closed : ClearingBatchState.Paid;
    }

    public static async Task<ClearingBatchDto> DtoAsync(IApplicationDbContext db, ClearingBatch l, DateOnly hoy, CancellationToken ct)
    {
        var cheques = await db.ReceivedChecks.AsNoTracking().Where(c => c.ClearingBatchId == l.Id).OrderBy(c => c.Id).ToListAsync(ct);
        var dtos = new List<ReceivedCheckDto>(cheques.Count);
        foreach (var c in cheques) dtos.Add(await ChequesRecibidos.DtoAsync(db, c, hoy, ct));
        return new ClearingBatchDto(l.PublicId, l.Number, l.OwnBankAccountPublicId, l.DepositedOn, l.CheckCount, l.TotalAmount, l.PaidAmount, l.ReturnedAmount,
            cheques.Where(c => c.Status is ReceivedCheckState.InClearing or ReceivedCheckState.Deposited).Sum(c => c.Amount), l.Status, dtos);
    }

    /// <summary>Al devolverse un cheque de un lote (FR-060), recalcula el lote. Sin guardar.</summary>
    public static async Task AlDevolverAsync(IApplicationDbContext db, ReceivedCheck cheque, CancellationToken ct)
    {
        if (cheque.ClearingBatchId is not { } id) return;
        var lote = await db.ClearingBatches.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (lote is null) return;
        var cheques = await db.ReceivedChecks.Where(c => c.ClearingBatchId == id).ToListAsync(ct);
        Recalcular(lote, cheques);
    }
}

// ----------------------------------------------------------------------------------------------------------------- armar --

/// <summary>
/// <c>POST /api/receipts/clearing-batches</c> (<c>Receipts.Checks.Manage</c>, con clave; nuevo): arma el lote de canje de los cheques recibidos o consignados que van a una
/// cuenta propia de recaudo, en la remesa del día si la hubo; los cheques pasan a «en canje» con el lote. Un cheque no va en dos lotes. (nuevo)
/// </summary>
public sealed record CreateClearingBatchCommand(Guid OwnBankAccountPublicId, IReadOnlyList<Guid> CheckPublicIds, Guid? RemittancePublicId = null, DateOnly? DepositedOn = null)
    : IRequest<Result<ClearingBatchDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateClearingBatchCommandValidator : AbstractValidator<CreateClearingBatchCommand>
{
    public CreateClearingBatchCommandValidator()
    {
        RuleFor(x => x.OwnBankAccountPublicId).NotEmpty().WithMessage("Indique la cuenta propia en que se consignan.");
        RuleFor(x => x.CheckPublicIds).NotEmpty().WithMessage("Elija los cheques del lote.");
    }
}

public sealed class CreateClearingBatchCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    ICuentasBancariasPropias cuentas,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<CreateClearingBatchCommand, Result<ClearingBatchDto>>
{
    public async Task<Result<ClearingBatchDto>> Handle(CreateClearingBatchCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ClearingBatchDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var cuenta = await cuentas.PorPublicIdAsync(request.OwnBankAccountPublicId, ct);
        if (cuenta is null) return Result.Failure<ClearingBatchDto>(ErroresDeRecaudo.NoEncontrado("La cuenta propia de recaudo"));
        var ids = request.CheckPublicIds.Distinct().ToList();
        var leidos = await db.ReceivedChecks.AsNoTracking().Where(c => ids.Contains(c.PublicId)).ToListAsync(ct);
        var mio = await alcance.ObtenerAsync(ct);
        if (leidos.Count != ids.Count || leidos.Any(c => !mio.IncluyeOficina(c.BranchId))) return Result.Failure<ClearingBatchDto>(ErroresDeRecaudo.NoEncontrado("Un cheque"));
        if (leidos.Any(c => c.ClearingBatchId is not null || c.Status is not (ReceivedCheckState.Received or ReceivedCheckState.Deposited)))
            return Result.Failure<ClearingBatchDto>(ErroresDeCanje.ChequeNoCanjeable());
        if (leidos.Select(c => c.BranchId).Distinct().Count() > 1) return Result.Failure<ClearingBatchDto>(ErroresDeCanje.OficinasDistintas());

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, TablasDeRecaudo.Cheques, leidos.Select(c => (long)c.Id).Order().ToList(), ct: ct);
        var cheques = await db.ReceivedChecks.Where(c => ids.Contains(c.PublicId)).ToListAsync(ct);
        if (cheques.Any(c => c.ClearingBatchId is not null || c.Status is not (ReceivedCheckState.Received or ReceivedCheckState.Deposited)))
            return Result.Failure<ClearingBatchDto>(ErroresDeCanje.ChequeNoCanjeable());
        var hoy = reloj.HoyLocal;
        var lote = new ClearingBatch
        {
            Number = await numerador.AsignarAsync(CanjeDeCheques.Codigo, CanjeDeCheques.Codigo, ct), OwnBankAccountPublicId = cuenta.PublicId, BranchId = cheques[0].BranchId,
            RemittancePublicId = request.RemittancePublicId, DepositedOn = request.DepositedOn ?? hoy, CheckCount = cheques.Count, TotalAmount = cheques.Sum(c => c.Amount),
            CreatedByUserId = usuario,
        };
        db.ClearingBatches.Add(lote);
        await db.SaveChangesAsync(ct);
        foreach (var c in cheques)
        {
            c.ClearingBatchId = lote.Id;
            c.RemittancePublicId ??= request.RemittancePublicId;
            ChequesRecibidos.Transicion(db, c, ReceivedCheckState.InClearing, usuario, reloj.UtcNow, nota: $"Lote de canje {lote.Number}");
        }
        await auditoria.AgregarAsync("Receipts.ClearingBatch.Created", "ClearingBatch", lote.PublicId, null,
            new { number = lote.Number, account = cuenta.Code, checks = lote.CheckCount, total = lote.TotalAmount, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await CanjeDeCheques.DtoAsync(db, lote, hoy, ct));
    }
}

// ------------------------------------------------------------------------------------------------------ pagado por el extracto --

/// <summary>
/// <c>POST /api/receipts/clearing-batches/{id}/statement-match</c> (<c>Receipts.Checks.Manage</c>, con clave; nuevo): cruza un abono del extracto de la cuenta del lote con los
/// cheques que paga —todos los que siguen en canje o los elegidos— cuando el valor coincide exacto; esos cheques quedan pagados con la línea, el abono queda identificado en
/// Recaudo por ese valor (no vuelve a identificarse) y el lote recalcula lo pagado. (nuevo)
/// </summary>
public sealed record MatchClearingBatchStatementCommand(Guid ClearingBatchPublicId, Guid StatementLinePublicId, IReadOnlyList<Guid>? CheckPublicIds = null)
    : IRequest<Result<ClearingBatchDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class MatchClearingBatchStatementCommandValidator : AbstractValidator<MatchClearingBatchStatementCommand>
{
    public MatchClearingBatchStatementCommandValidator()
    {
        RuleFor(x => x.ClearingBatchPublicId).NotEmpty();
        RuleFor(x => x.StatementLinePublicId).NotEmpty().WithMessage("Elija el abono del extracto que paga los cheques.");
    }
}

public sealed class MatchClearingBatchStatementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    IMovimientosBancariosParaRecaudo movimientos,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj,
    Accounting.EmisionDeRecaudo? emision = null) : IRequestHandler<MatchClearingBatchStatementCommand, Result<ClearingBatchDto>>
{
    public async Task<Result<ClearingBatchDto>> Handle(MatchClearingBatchStatementCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ClearingBatchDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var lote = await db.ClearingBatches.FirstOrDefaultAsync(b => b.PublicId == request.ClearingBatchPublicId, ct);
        if (lote is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(lote.BranchId)) return Result.Failure<ClearingBatchDto>(ErroresDeRecaudo.NoEncontrado("El lote de canje"));
        var linea = await movimientos.LineaAsync(request.StatementLinePublicId, ct);
        if (linea is null || linea.OwnBankAccountPublicId != lote.OwnBankAccountPublicId) return Result.Failure<ClearingBatchDto>(ErroresDeCanje.AbonoDeOtraCuenta());
        if (linea.Amount - linea.Identified <= 0m) return Result.Failure<ClearingBatchDto>(ErroresDeRecaudo.AbonoYaIdentificado());

        var todos = await db.ReceivedChecks.AsNoTracking().Where(c => c.ClearingBatchId == lote.Id).ToListAsync(ct);
        var elegidos = request.CheckPublicIds is { Count: > 0 } e ? todos.Where(c => e.Contains(c.PublicId)).ToList() : todos;
        var enCanje = elegidos.Where(c => c.Status is ReceivedCheckState.InClearing or ReceivedCheckState.Deposited).ToList();
        if (enCanje.Count == 0) return Result.Failure<ClearingBatchDto>(ErroresDeCanje.NadaEnCanje());
        var valor = enCanje.Sum(c => c.Amount);
        if (valor != linea.Amount - linea.Identified) return Result.Failure<ClearingBatchDto>(ErroresDeCanje.ValorNoCoincide(valor, linea.Amount - linea.Identified));

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, TablasDeRecaudo.Cheques, todos.Select(c => (long)c.Id).Order().ToList(), ct: ct);
        var cheques = await db.ReceivedChecks.Where(c => c.ClearingBatchId == lote.Id).ToListAsync(ct);
        var ahora = reloj.UtcNow;
        foreach (var c in cheques.Where(x => enCanje.Any(y => y.Id == x.Id)))
        {
            c.PaidByStatementLinePublicId = linea.PublicId;
            ChequesRecibidos.Transicion(db, c, ReceivedCheckState.Paid, usuario, ahora, nota: $"Pagado por el extracto en el lote {lote.Number}");
            // Segunda ronda del 2026-10-27 (D-l; FR-235): lo que el recibo debitó al canje pasa al banco de la cuenta del lote, con la fecha del abono.
            if (emision is not null) await emision.ChequeRecibidoPagadoAsync(c, lote.OwnBankAccountPublicId, linea.PublicId, lote.Number, linea.MovementDate, ct);
        }
        CanjeDeCheques.Recalcular(lote, cheques);
        lote.StatementLinePublicIds = string.Join(",", new[] { lote.StatementLinePublicIds, linea.PublicId.ToString() }.Where(s => !string.IsNullOrEmpty(s)));
        db.BankItemIdentifications.Add(new BankItemIdentification
        {
            BankStatementLinePublicId = linea.PublicId, Sequence = await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, linea.PublicId, ct),
            OwnBankAccountPublicId = linea.OwnBankAccountPublicId, MovementDate = linea.MovementDate, Amount = valor, Outcome = BankItemOutcome.ClearingBatch,
            IdentifiedByUserId = usuario, IdentifiedAt = ahora, Note = string.Create(CultureInfo.InvariantCulture, $"Lote de canje {lote.Number}"),
        });
        await movimientos.MarcarIdentificacionAsync(linea.PublicId, linea.Identified + valor, ct);
        await auditoria.AgregarAsync("Receipts.ClearingBatch.Paid", "ClearingBatch", lote.PublicId, null,
            new { number = lote.Number, line = linea.PublicId, checks = enCanje.Count, amount = valor, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await CanjeDeCheques.DtoAsync(db, lote, reloj.HoyLocal, ct));
    }
}

/// <summary><c>GET /api/receipts/clearing-batches?state=</c> (<c>Receipts.Checks.View</c>; nuevo): los lotes de canje de las oficinas al alcance. (nuevo)</summary>
public sealed record ListClearingBatchesQuery(ClearingBatchState? State = null) : IRequest<Result<IReadOnlyList<ClearingBatchDto>>>;

public sealed class ListClearingBatchesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<ListClearingBatchesQuery, Result<IReadOnlyList<ClearingBatchDto>>>
{
    public async Task<Result<IReadOnlyList<ClearingBatchDto>>> Handle(ListClearingBatchesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.ClearingBatches.AsNoTracking();
        if (request.State is { } s) consulta = consulta.Where(b => b.Status == s);
        var filas = (await consulta.OrderByDescending(b => b.Id).Take(200).ToListAsync(ct)).Where(b => mio.IncluyeOficina(b.BranchId)).ToList();
        var lista = new List<ClearingBatchDto>(filas.Count);
        foreach (var b in filas) lista.Add(await CanjeDeCheques.DtoAsync(db, b, reloj.HoyLocal, ct));
        return Result.Success<IReadOnlyList<ClearingBatchDto>>(lista);
    }
}

/// <summary>Los errores del canje completo (feature 015, R2, fase 37; nuevos, DT §2.7). (nuevo)</summary>
public static class ErroresDeCanje
{
    public static Error ChequeNoCanjeable() => new("Receipts.ClearingBatch.CheckNotEligible",
        "Sólo van a un lote de canje los cheques recibidos o consignados que no están en otro lote: revise los elegidos.");

    public static Error OficinasDistintas() => new("Receipts.ClearingBatch.MixedBranches", "Un lote de canje es de una sola oficina: arme uno por oficina.");

    public static Error AbonoDeOtraCuenta() => new("Receipts.ClearingBatch.StatementLineMismatch",
        "El abono no es de la cuenta propia en que se consignó el lote: elija un abono del extracto de esa cuenta.");

    public static Error NadaEnCanje() => new("Receipts.ClearingBatch.NothingInClearing", "Ninguno de los cheques elegidos sigue en canje.");

    public static Error ValorNoCoincide(decimal cheques, decimal abono) => new ErrorConDatos("Receipts.ClearingBatch.AmountMismatch",
        string.Create(CultureInfo.GetCultureInfo("es-CO"),
            $"Los cheques elegidos suman {cheques:N2} y el abono por identificar es de {abono:N2}: elija los cheques que el banco pagó con ese abono."),
        new { checks = cheques, statementLine = abono });
}
