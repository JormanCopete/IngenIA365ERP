using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Cuadres;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Common.Cuadres;
using IngenIA365ERP.Domain.Entities.Receipts.Activation;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Activation;

// Feature 015, US13 (T0288; FR-158, FR-228; data-model §15.2; api.md §14): el cuadre de apertura de Recaudo contra Contabilidad, definitivo o provisional (decisión
// del dueño: con aprobación, motivo y fecha límite). Lo cargado sale de lo que dejaron las plantillas de puesta en marcha y lo contable de SaldoContableAsync por rol y
// oficina; ningún hecho contable (FR-158).

/// <summary>
/// Las líneas propuestas del cuadre de Recaudo a una fecha de corte, por oficina (nuevo): efectivo en la bóveda (<c>Boveda</c>, la suma de sus movimientos), partidas
/// por identificar abiertas, cheques recibidos todavía en canje y saldos a favor abiertos. Las de pasivo (partidas, saldos a favor) comparan contra el saldo
/// contable con su signo (crédito). Una oficina sin carga ni dato contable no aparece.
/// </summary>
public sealed class LineasDelCuadreDeRecaudo(IApplicationDbContext db, IContabilidadParaRecaudo contabilidad)
{
    public static readonly IReadOnlyList<string> Conceptos =
        [RolesDeRecaudo.Boveda, RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.SaldoAFavorDePagadores];

    /// <summary>Los conceptos de naturaleza crédito: su saldo contable se compara con el signo cambiado.</summary>
    public static readonly IReadOnlySet<string> DePasivo = new HashSet<string>(StringComparer.Ordinal) { RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.SaldoAFavorDePagadores };

    public async Task<CalculoDeCuadre> ProponerAsync(DateOnly corte, CancellationToken ct)
    {
        var hasta = corte.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var boveda = await (from m in db.VaultMovements.AsNoTracking()
                            join v in db.Vaults.AsNoTracking() on m.VaultId equals v.Id
                            where m.MovedAt < hasta
                            group m by v.BranchId into g
                            select new { BranchId = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var partidas = await db.UnidentifiedItems.AsNoTracking()
            .Where(p => (p.Status == UnidentifiedItemState.Open || p.Status == UnidentifiedItemState.PartiallyIdentified) && p.OpenedOn <= corte)
            .GroupBy(p => p.BranchId).Select(g => new { BranchId = g.Key, Total = g.Sum(x => x.Balance) }).ToListAsync(ct);
        var cheques = await db.ReceivedChecks.AsNoTracking()
            .Where(c => c.Status == ReceivedCheckState.Received || c.Status == ReceivedCheckState.InCustody || c.Status == ReceivedCheckState.Deposited
                        || c.Status == ReceivedCheckState.InClearing)
            .GroupBy(c => c.BranchId).Select(g => new { BranchId = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var saldos = await db.PayerCreditBalances.AsNoTracking().Where(s => s.Status == PayerCreditBalanceState.Open && s.OpenedOn <= corte)
            .GroupBy(s => s.BranchId).Select(g => new { BranchId = g.Key, Total = g.Sum(x => x.Balance) }).ToListAsync(ct);

        var cargado = new Dictionary<(string, int), decimal>();
        foreach (var x in boveda) cargado[(RolesDeRecaudo.Boveda, x.BranchId)] = x.Total;
        foreach (var x in partidas) cargado[(RolesDeRecaudo.PartidasPorIdentificar, x.BranchId)] = x.Total;
        foreach (var x in cheques) cargado[(RolesDeRecaudo.ChequesEnCanje, x.BranchId)] = x.Total;
        foreach (var x in saldos) cargado[(RolesDeRecaudo.SaldoAFavorDePagadores, x.BranchId)] = x.Total;

        var oficinas = await db.Branches.AsNoTracking().Select(b => new { b.Id, b.PublicId, b.LegacyCode, b.Name }).ToListAsync(ct);
        var propuestas = new List<LineaPropuestaDeCuadre>();
        foreach (var o in oficinas)
            foreach (var concepto in Conceptos)
            {
                var carga = cargado.GetValueOrDefault((concepto, o.Id));
                var saldo = await contabilidad.SaldoContableAsync(new SolicitudDeSaldo(concepto, corte, o.PublicId, null), ct);
                decimal? contable = saldo.Count == 0 ? null : DePasivo.Contains(concepto) ? -saldo[0].Balance : saldo[0].Balance;
                if (carga == 0m && (contable ?? 0m) == 0m) continue;
                propuestas.Add(new LineaPropuestaDeCuadre(concepto, Dimension(o.LegacyCode, o.Id), o.Name, carga, contable));
            }
        return CalculoDelCuadreDeApertura.Calcular(propuestas);
    }

    /// <summary>La dimensión de una oficina: su código, o su Id si no lo tiene (como en los hechos de Recaudo).</summary>
    public static string Dimension(string? codigo, int id) => string.IsNullOrWhiteSpace(codigo) ? id.ToString(System.Globalization.CultureInfo.InvariantCulture) : codigo;
}

/// <summary><c>GET /api/receipts/opening-squares/current?cutoffDate=</c> (<c>Receipts.Activation.View</c>, <c>.Square</c> o <c>.ProvisionalSquare</c>). (nuevo)</summary>
public sealed record GetReceiptsOpeningSquareQuery(DateOnly? CutoffDate = null) : IRequest<Result<CurrentOpeningSquareDto>>;

public sealed class GetReceiptsOpeningSquareQueryHandler(IApplicationDbContext db, LineasDelCuadreDeRecaudo lineas, IDateTimeService reloj)
    : IRequestHandler<GetReceiptsOpeningSquareQuery, Result<CurrentOpeningSquareDto>>
{
    public async Task<Result<CurrentOpeningSquareDto>> Handle(GetReceiptsOpeningSquareQuery request, CancellationToken ct)
    {
        var corte = request.CutoffDate ?? reloj.HoyLocal.AddDays(-1);
        var calculo = await lineas.ProponerAsync(corte, ct);
        var (vigente, provisional) = await RegistroDeCuadres.VigentesAsync(db.ReceiptsOpeningSquares, ct);
        return Result.Success(new CurrentOpeningSquareDto(corte, RegistroDeCuadres.Lineas(calculo), calculo.TotalCargado, calculo.TotalContable, calculo.TotalDiferencia,
            calculo.Cuadra, vigente is null ? null : await CuadresDeRecaudo.DtoAsync(db, vigente, ct),
            provisional is null ? null : await CuadresDeRecaudo.DtoAsync(db, provisional, ct)));
    }
}

/// <summary>Lo común de los comandos del cuadre de Recaudo. (nuevo)</summary>
internal static class CuadresDeRecaudo
{
    public const string Modulo = "Receipts";

    public static Task<OpeningSquareDto> DtoAsync(IApplicationDbContext db, ReceiptsOpeningSquare c, CancellationToken ct) =>
        RegistroDeCuadres.DtoAsync(c, db.ReceiptsOpeningSquareLines, db.ReceiptsOpeningSquares,
            async (id, t) => id is null ? null : await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Username).FirstOrDefaultAsync(t), ct);

    public static async Task<Result<OpeningSquareDto>> RegistrarAsync(IApplicationDbContext db, LineasDelCuadreDeRecaudo lineas, IActorActual actorActual,
        AuditoriaDeRecaudo auditoria, IDateTimeService reloj, OpeningSquareKind tipo, DateOnly? corteSolicitado, string? motivo, DateOnly? fechaLimite, Guid? acta,
        CancellationToken ct, IngenIA365ERP.Application.Common.Alerts.IAlertas? alertas = null)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<OpeningSquareDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var corte = corteSolicitado ?? reloj.HoyLocal.AddDays(-1);
        var calculo = await lineas.ProponerAsync(corte, ct);
        var r = await RegistroDeCuadres.RegistrarAsync(db.ReceiptsOpeningSquares, db.ReceiptsOpeningSquareLines, db.SaveChangesAsync, Modulo, tipo, corte, calculo, usuario,
            reloj.UtcNow, motivo, fechaLimite, acta, ct);
        if (r.IsFailure) return Result.Failure<OpeningSquareDto>(r.Error);
        await auditoria.AgregarAsync(tipo == OpeningSquareKind.Provisional ? "Receipts.Activation.ProvisionalSquareRegistered" : "Receipts.Activation.DefinitiveSquareRegistered",
            "ReceiptsOpeningSquare", r.Value.PublicId, null,
            new { cutoff = corte, status = r.Value.Status.ToString(), loaded = calculo.TotalCargado, ledger = calculo.TotalContable, difference = calculo.TotalDiferencia,
                deadline = r.Value.DeadlineOn, reason = r.Value.Reason, minutes = acta, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        // Fase 21 (T0321; FR-228): el definitivo cierra las alertas del provisional que reemplazó.
        if (tipo == OpeningSquareKind.Definitive && alertas is not null)
            foreach (var provisional in await db.ReceiptsOpeningSquares.AsNoTracking().Where(c => c.DefinitiveSquareId == r.Value.Id).Select(c => c.PublicId).ToListAsync(ct))
                await CuadreProvisionalPendiente.CerrarAlertasAsync(alertas, IngenIA365ERP.Application.Common.Alerts.TiposDeAlerta.RecaudoCuadreProvisionalPendiente, provisional, ct);
        return Result.Success(await DtoAsync(db, r.Value, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/opening-squares/provisional</c> (<c>Receipts.Activation.ProvisionalSquare</c>, con clave; FR-228): el cuadre provisional con su motivo y la
/// fecha límite del definitivo; quien lo registra con el permiso es quien lo aprueba. (nuevo)
/// </summary>
public sealed record RegisterProvisionalOpeningSquareCommand(string Reason, DateOnly DeadlineOn, DateOnly? CutoffDate = null)
    : IRequest<Result<OpeningSquareDto>>, IOperacionIdempotente, IConMotivo, ISolicitudDeCuadreProvisional
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterProvisionalOpeningSquareCommandValidator : ValidadorDeCuadreProvisional<RegisterProvisionalOpeningSquareCommand>;

public sealed class RegisterProvisionalOpeningSquareCommandHandler(IApplicationDbContext db, LineasDelCuadreDeRecaudo lineas, IActorActual actor,
    AuditoriaDeRecaudo auditoria, IDateTimeService reloj) : IRequestHandler<RegisterProvisionalOpeningSquareCommand, Result<OpeningSquareDto>>
{
    public Task<Result<OpeningSquareDto>> Handle(RegisterProvisionalOpeningSquareCommand request, CancellationToken ct) =>
        CuadresDeRecaudo.RegistrarAsync(db, lineas, actor, auditoria, reloj, OpeningSquareKind.Provisional, request.CutoffDate, request.Reason, request.DeadlineOn, null, ct);
}

/// <summary>
/// <c>POST /api/receipts/opening-squares/definitive</c> (<c>Receipts.Activation.Square</c>, con clave; FR-158): el definitivo, con diferencia cero o con el acta que la
/// acepta; enlaza al provisional pendiente. (nuevo)
/// </summary>
public sealed record RegisterDefinitiveOpeningSquareCommand(DateOnly? CutoffDate = null, Guid? MinutesAttachmentPublicId = null, string? Reason = null)
    : IRequest<Result<OpeningSquareDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDefinitiveOpeningSquareCommandHandler(IApplicationDbContext db, LineasDelCuadreDeRecaudo lineas, IActorActual actor,
    AuditoriaDeRecaudo auditoria, IDateTimeService reloj, IngenIA365ERP.Application.Common.Alerts.IAlertas? alertas = null) : IRequestHandler<RegisterDefinitiveOpeningSquareCommand, Result<OpeningSquareDto>>
{
    public Task<Result<OpeningSquareDto>> Handle(RegisterDefinitiveOpeningSquareCommand request, CancellationToken ct) =>
        CuadresDeRecaudo.RegistrarAsync(db, lineas, actor, auditoria, reloj, OpeningSquareKind.Definitive, request.CutoffDate, request.Reason, null,
            request.MinutesAttachmentPublicId, ct, alertas);
}

/// <summary>
/// <c>GET /api/receipts/reference-figures?cutoffDate=</c> (<c>Receipts.Imports.Run</c> o <c>Receipts.Activation.View</c>; FR-160): las cifras de referencia vigentes del corte
/// (la última fecha cargada si no se pide) contra lo cargado y lo contable de la misma propuesta del cuadre. Sin nombrar al sistema de origen. (nuevo)
/// </summary>
public sealed record ListReceiptsReferenceFiguresQuery(DateOnly? CutoffDate = null) : IRequest<Result<IReadOnlyList<ReferenceFigureComparisonDto>>>;

public sealed class ListReceiptsReferenceFiguresQueryHandler(IApplicationDbContext db, LineasDelCuadreDeRecaudo lineas)
    : IRequestHandler<ListReceiptsReferenceFiguresQuery, Result<IReadOnlyList<ReferenceFigureComparisonDto>>>
{
    public async Task<Result<IReadOnlyList<ReferenceFigureComparisonDto>>> Handle(ListReceiptsReferenceFiguresQuery request, CancellationToken ct)
    {
        var corte = request.CutoffDate ?? await db.ReceiptsReferenceFigures.AsNoTracking().OrderByDescending(c => c.CutoffDate).Select(c => (DateOnly?)c.CutoffDate).FirstOrDefaultAsync(ct);
        if (corte is null) return Result.Success<IReadOnlyList<ReferenceFigureComparisonDto>>([]);
        var cifras = await db.ReceiptsReferenceFigures.AsNoTracking().Where(c => c.CutoffDate == corte).ToListAsync(ct);
        var propuesta = await lineas.ProponerAsync(corte.Value, ct);
        return Result.Success(RegistroDeCuadres.Cotejar(cifras, propuesta));
    }
}
