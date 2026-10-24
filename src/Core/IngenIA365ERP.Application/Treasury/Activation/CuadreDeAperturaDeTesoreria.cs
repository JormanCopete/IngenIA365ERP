using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Cuadres;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Common.Cuadres;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Activation;

// Feature 015, US13 (T0288; FR-158, FR-228; data-model §15.2; api.md §14): el cuadre de apertura de Tesorería contra Contabilidad, con la misma mecánica que el de
// Recaudo (Common/Cuadres). Ningún hecho contable.

/// <summary>
/// Las líneas propuestas del cuadre de Tesorería a una fecha de corte (nuevo): el saldo en libros al corte de cada cuenta propia (<c>Banco</c>, por el vínculo
/// contable), los cheques girados todavía en tránsito de la puesta en marcha (informativa: el saldo en libros ya los descuenta) y el efectivo en tránsito de las
/// remesas y consignaciones (<c>EfectivoEnTransito</c>, por oficina).
/// </summary>
public sealed class LineasDelCuadreDeTesoreria(IApplicationDbContext db, IContabilidadParaTesoreria contabilidad)
{
    /// <summary>El concepto informativo de los cheques girados en tránsito (no es un rol de la matriz). (nuevo)</summary>
    public const string ChequesGiradosEnTransito = "ChequesGiradosEnTransito";

    public async Task<CalculoDeCuadre> ProponerAsync(DateOnly corte, CancellationToken ct)
    {
        var propuestas = new List<LineaPropuestaDeCuadre>();
        var cuentas = await db.OwnBankAccounts.AsNoTracking().Where(c => c.Status == TreasuryCatalogStatus.Active).OrderBy(c => c.Code).ToListAsync(ct);
        var enTransito = await (from c in db.IssuedChecks.AsNoTracking()
                                join k in db.Checkbooks.AsNoTracking() on c.CheckbookId equals k.Id
                                where c.IsOpeningLoad && (c.Status == IssuedCheckState.Issued || c.Status == IssuedCheckState.Delivered) && c.IssuedOn <= corte
                                group c by k.BankAccountId into g
                                select new { Cuenta = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync(ct);
        foreach (var cuenta in cuentas)
        {
            var saldo = await contabilidad.SaldoContableAsync(new SolicitudDeSaldo(RolesDeTesoreria.Banco, corte, null, cuenta.PublicId), ct);
            decimal? contable = saldo.Count == 0 ? null : saldo[0].Balance;
            var cargado = cuenta.OpeningBalance ?? 0m;
            if (cargado != 0m || (contable ?? 0m) != 0m)
                propuestas.Add(new LineaPropuestaDeCuadre(RolesDeTesoreria.Banco, cuenta.Code, cuenta.Name, cargado, contable));
            var cheques = enTransito.FirstOrDefault(t => t.Cuenta == cuenta.Id)?.Total ?? 0m;
            if (cheques != 0m) propuestas.Add(new LineaPropuestaDeCuadre(ChequesGiradosEnTransito, cuenta.Code, cuenta.Name, cheques, null, Comparable: false));
        }

        var remesas = await db.CashRemittances.AsNoTracking().Where(r => r.Status == RemittanceState.InTransit && r.DispatchDate <= corte)
            .GroupBy(r => r.BranchId).Select(g => new { BranchId = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var oficinas = await db.Branches.AsNoTracking().Select(b => new { b.Id, b.PublicId, b.LegacyCode, b.Name }).ToListAsync(ct);
        foreach (var o in oficinas)
        {
            var cargado = remesas.FirstOrDefault(r => r.BranchId == o.Id)?.Total ?? 0m;
            var saldo = await contabilidad.SaldoContableAsync(new SolicitudDeSaldo(RolesDeTesoreria.EfectivoEnTransito, corte, o.PublicId, null), ct);
            decimal? contable = saldo.Count == 0 ? null : saldo[0].Balance;
            if (cargado == 0m && (contable ?? 0m) == 0m) continue;
            var dimension = string.IsNullOrWhiteSpace(o.LegacyCode) ? o.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) : o.LegacyCode;
            propuestas.Add(new LineaPropuestaDeCuadre(RolesDeTesoreria.EfectivoEnTransito, dimension, o.Name, cargado, contable));
        }
        return CalculoDelCuadreDeApertura.Calcular(propuestas);
    }
}

/// <summary><c>GET /api/treasury/opening-squares/current?cutoffDate=</c> (<c>Treasury.Activation.View</c>, <c>.Square</c> o <c>.ProvisionalSquare</c>). (nuevo)</summary>
public sealed record GetTreasuryOpeningSquareQuery(DateOnly? CutoffDate = null) : IRequest<Result<CurrentOpeningSquareDto>>;

public sealed class GetTreasuryOpeningSquareQueryHandler(IApplicationDbContext db, LineasDelCuadreDeTesoreria lineas, IDateTimeService reloj)
    : IRequestHandler<GetTreasuryOpeningSquareQuery, Result<CurrentOpeningSquareDto>>
{
    public async Task<Result<CurrentOpeningSquareDto>> Handle(GetTreasuryOpeningSquareQuery request, CancellationToken ct)
    {
        var corte = request.CutoffDate ?? reloj.HoyLocal.AddDays(-1);
        var calculo = await lineas.ProponerAsync(corte, ct);
        var (vigente, provisional) = await RegistroDeCuadres.VigentesAsync(db.TreasuryOpeningSquares, ct);
        return Result.Success(new CurrentOpeningSquareDto(corte, RegistroDeCuadres.Lineas(calculo), calculo.TotalCargado, calculo.TotalContable, calculo.TotalDiferencia,
            calculo.Cuadra, vigente is null ? null : await CuadresDeTesoreria.DtoAsync(db, vigente, ct),
            provisional is null ? null : await CuadresDeTesoreria.DtoAsync(db, provisional, ct)));
    }
}

/// <summary>Lo común de los comandos del cuadre de Tesorería. (nuevo)</summary>
internal static class CuadresDeTesoreria
{
    public const string Modulo = "Treasury";

    public static Task<OpeningSquareDto> DtoAsync(IApplicationDbContext db, TreasuryOpeningSquare c, CancellationToken ct) =>
        RegistroDeCuadres.DtoAsync(c, db.TreasuryOpeningSquareLines, db.TreasuryOpeningSquares,
            async (id, t) => id is null ? null : await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Username).FirstOrDefaultAsync(t), ct);

    public static async Task<Result<OpeningSquareDto>> RegistrarAsync(IApplicationDbContext db, LineasDelCuadreDeTesoreria lineas, IActorActual actorActual,
        AuditoriaDeTesoreria auditoria, IDateTimeService reloj, OpeningSquareKind tipo, DateOnly? corteSolicitado, string? motivo, DateOnly? fechaLimite, Guid? acta,
        CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<OpeningSquareDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var corte = corteSolicitado ?? reloj.HoyLocal.AddDays(-1);
        var calculo = await lineas.ProponerAsync(corte, ct);
        var r = await RegistroDeCuadres.RegistrarAsync(db.TreasuryOpeningSquares, db.TreasuryOpeningSquareLines, db.SaveChangesAsync, Modulo, tipo, corte, calculo, usuario,
            reloj.UtcNow, motivo, fechaLimite, acta, ct);
        if (r.IsFailure) return Result.Failure<OpeningSquareDto>(r.Error);
        await auditoria.AgregarAsync(tipo == OpeningSquareKind.Provisional ? "Treasury.Activation.ProvisionalSquareRegistered" : "Treasury.Activation.DefinitiveSquareRegistered",
            "TreasuryOpeningSquare", r.Value.PublicId, null,
            new { cutoff = corte, status = r.Value.Status.ToString(), loaded = calculo.TotalCargado, ledger = calculo.TotalContable, difference = calculo.TotalDiferencia,
                deadline = r.Value.DeadlineOn, reason = r.Value.Reason, minutes = acta, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await DtoAsync(db, r.Value, ct));
    }
}

/// <summary><c>POST /api/treasury/opening-squares/provisional</c> (<c>Treasury.Activation.ProvisionalSquare</c>, con clave; FR-228). (nuevo)</summary>
public sealed record RegisterTreasuryProvisionalOpeningSquareCommand(string Reason, DateOnly DeadlineOn, DateOnly? CutoffDate = null)
    : IRequest<Result<OpeningSquareDto>>, IOperacionIdempotente, IConMotivo, ISolicitudDeCuadreProvisional
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterTreasuryProvisionalOpeningSquareCommandValidator : ValidadorDeCuadreProvisional<RegisterTreasuryProvisionalOpeningSquareCommand>;

public sealed class RegisterTreasuryProvisionalOpeningSquareCommandHandler(IApplicationDbContext db, LineasDelCuadreDeTesoreria lineas, IActorActual actor,
    AuditoriaDeTesoreria auditoria, IDateTimeService reloj) : IRequestHandler<RegisterTreasuryProvisionalOpeningSquareCommand, Result<OpeningSquareDto>>
{
    public Task<Result<OpeningSquareDto>> Handle(RegisterTreasuryProvisionalOpeningSquareCommand request, CancellationToken ct) =>
        CuadresDeTesoreria.RegistrarAsync(db, lineas, actor, auditoria, reloj, OpeningSquareKind.Provisional, request.CutoffDate, request.Reason, request.DeadlineOn, null, ct);
}

/// <summary><c>POST /api/treasury/opening-squares/definitive</c> (<c>Treasury.Activation.Square</c>, con clave; FR-158). (nuevo)</summary>
public sealed record RegisterTreasuryDefinitiveOpeningSquareCommand(DateOnly? CutoffDate = null, Guid? MinutesAttachmentPublicId = null, string? Reason = null)
    : IRequest<Result<OpeningSquareDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterTreasuryDefinitiveOpeningSquareCommandHandler(IApplicationDbContext db, LineasDelCuadreDeTesoreria lineas, IActorActual actor,
    AuditoriaDeTesoreria auditoria, IDateTimeService reloj) : IRequestHandler<RegisterTreasuryDefinitiveOpeningSquareCommand, Result<OpeningSquareDto>>
{
    public Task<Result<OpeningSquareDto>> Handle(RegisterTreasuryDefinitiveOpeningSquareCommand request, CancellationToken ct) =>
        CuadresDeTesoreria.RegistrarAsync(db, lineas, actor, auditoria, reloj, OpeningSquareKind.Definitive, request.CutoffDate, request.Reason, null,
            request.MinutesAttachmentPublicId, ct);
}

/// <summary>
/// <c>GET /api/treasury/reference-figures?cutoffDate=</c> (<c>Treasury.Imports.Run</c> o <c>Treasury.Activation.View</c>; FR-160): las cifras de referencia vigentes del corte
/// (la última fecha cargada si no se pide) contra lo cargado y lo contable de la misma propuesta del cuadre. Sin nombrar al sistema de origen. (nuevo)
/// </summary>
public sealed record ListTreasuryReferenceFiguresQuery(DateOnly? CutoffDate = null) : IRequest<Result<IReadOnlyList<ReferenceFigureComparisonDto>>>;

public sealed class ListTreasuryReferenceFiguresQueryHandler(IApplicationDbContext db, LineasDelCuadreDeTesoreria lineas)
    : IRequestHandler<ListTreasuryReferenceFiguresQuery, Result<IReadOnlyList<ReferenceFigureComparisonDto>>>
{
    public async Task<Result<IReadOnlyList<ReferenceFigureComparisonDto>>> Handle(ListTreasuryReferenceFiguresQuery request, CancellationToken ct)
    {
        var corte = request.CutoffDate ?? await db.TreasuryReferenceFigures.AsNoTracking().OrderByDescending(c => c.CutoffDate).Select(c => (DateOnly?)c.CutoffDate).FirstOrDefaultAsync(ct);
        if (corte is null) return Result.Success<IReadOnlyList<ReferenceFigureComparisonDto>>([]);
        var cifras = await db.TreasuryReferenceFigures.AsNoTracking().Where(c => c.CutoffDate == corte).ToListAsync(ct);
        var propuesta = await lineas.ProponerAsync(corte.Value, ct);
        return Result.Success(RegistroDeCuadres.Cotejar(cifras, propuesta));
    }
}
