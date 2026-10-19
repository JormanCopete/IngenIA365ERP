using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

// Feature 014, US5 (T133, T139; api.md §4, «settlements»): las liquidaciones de intereses de una cuenta —la lista, la simulación a cualquier fecha sin
// efectos y la liquidación manual idempotente—. Todo dentro del alcance de la Cartera (oficina y línea de la cuenta). (nuevo)

/// <summary>Una liquidación de intereses (api.md §4, <c>InterestSettlement</c>). (nuevo)</summary>
public sealed record InterestSettlementDto(
    Guid PublicId,
    Guid OperationPublicId,
    string OperationNumber,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    decimal Accrued,
    decimal Settled,
    decimal RoundingAdjustment,
    decimal WithholdingBase,
    decimal? InflationaryComponent,
    decimal WithholdingRate,
    decimal Withholding,
    string? WithholdingExemptionReason,
    bool ThresholdReached,
    decimal Net,
    DateTime SettledAt,
    string Origin,
    bool Reversed)
{
    /// <summary>Repetir la liquidación de un período ya liquidado responde la existente (SC-003).</summary>
    public bool AlreadyExisted { get; init; }
}

/// <summary>Un día de la explicación de la simulación (api.md §1.4). (nuevo)</summary>
public sealed record InterestExplanationDayDto(DateOnly Date, decimal Balance, decimal Base, string RateTier, decimal AnnualRate, decimal DailyRate, decimal Interest,
    bool BelowMinimum, bool Suspended, string Explanation);

/// <summary>La retención de una simulación o una liquidación (api.md §4). (nuevo)</summary>
public sealed record InterestWithholdingDto(decimal Base, decimal? InflationaryComponentFraction, decimal InflationaryComponent, decimal TaxableBase, decimal Rate,
    decimal? Threshold, bool ThresholdApplies, IReadOnlyList<DateOnly> ThresholdReachedOn, decimal Amount, string? ExemptionReason, string HolderTaxProfile,
    string Explanation);

/// <summary>La simulación de la liquidación a una fecha (api.md §4, <c>POST /{id}/settlements/simulate</c>; FR-064). (nuevo)</summary>
public sealed record InterestSimulationDto(DateOnly Date, DateTime RecordedAsOf, DateOnly PeriodFrom, DateOnly PeriodTo, decimal Accrued, decimal Interest,
    decimal RoundingAdjustment, InterestWithholdingDto Withholding, decimal Net, IReadOnlyList<InterestExplanationDayDto> Explanation);

internal static class Liquidaciones
{
    public static InterestSettlementDto Dto(InterestSettlement l, MemberAccountOperation o, bool yaExistia = false) =>
        new(l.PublicId, o.PublicId, o.Number, l.PeriodFrom, l.PeriodTo, l.AccruedInterest, l.GrossInterest, l.RoundingDifference, l.WithholdingBase,
            l.InflationaryComponentAmount, l.WithholdingRate, l.WithholdingAmount, l.WithholdingExemptionReason, l.ThresholdReached, l.NetInterest, o.RegisteredAt,
            o.ActorKind.ToString(), l.ReversedByOperationId is not null)
        {
            AlreadyExisted = yaExistia,
        };

    public static InterestWithholdingDto Dto(RetencionCalculada r, PerfilDeRetencion perfil) =>
        new(r.Base, r.ComponenteInflacionario, r.ComponenteInflacionarioValor, r.BaseGravable, r.Tarifa, r.UmbralDiario, r.UmbralAplica, r.DiasQueAlcanzan, r.Valor,
            r.MotivoDeExencion, perfil.ToString(), r.Explicacion);
}

// ------------------------------------------------------------------------------------------------------------------ lista --

/// <summary>Las liquidaciones de una cuenta (api.md §4, <c>GET /{id}/settlements</c>, <c>Lending.Accounts.Read</c>). (nuevo)</summary>
public sealed record ListInterestSettlementsQuery(Guid AccountPublicId, int Page = 1, int PageSize = 50) : IRequest<Result<PagedResult<InterestSettlementDto>>>;

public sealed class ListInterestSettlementsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<ListInterestSettlementsQuery, Result<PagedResult<InterestSettlementDto>>>
{
    public async Task<Result<PagedResult<InterestSettlementDto>>> Handle(ListInterestSettlementsQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<PagedResult<InterestSettlementDto>>(leida.Error);
        var cuenta = leida.Value;
        var consulta = db.InterestSettlements.AsNoTracking().Where(s => s.MemberAccountId == cuenta.Id && !s.IsDeleted);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await (from s in consulta
                           join o in db.MemberAccountOperations.AsNoTracking() on s.OperationId equals o.Id
                           orderby s.PeriodTo descending, s.Id descending
                           select new { s, o })
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        await auditoria.EmitirMovimientosDeCuentaVistosAsync(cuenta.PublicId, new { settlements = true, page = pagina.SafePage }, filas.Count, ct);
        return Result.Success(new PagedResult<InterestSettlementDto>(filas.Select(x => Liquidaciones.Dto(x.s, x.o)).ToList(), pagina.SafePage,
            pagina.SafePageSize, total));
    }
}

// ------------------------------------------------------------------------------------------------------------- simulación --

/// <summary>
/// La simulación de la liquidación a cualquier fecha, sin efectos (feature 014, US5, T133; api.md §4, <c>POST /{id}/settlements/simulate</c>,
/// <c>Lending.Accounts.Read</c>; FR-064, api.md §1.4): lo que se liquidaría desde la última liquidación hasta <c>date</c> con lo registrado hasta
/// <c>recordedAsOf</c> —los días todavía no causados los calcula el motor—, la retención con su umbral y la explicación día por día. No escribe. (nuevo)
/// </summary>
public sealed record SimulateInterestSettlementQuery(Guid AccountPublicId, DateOnly Date, DateTime? RecordedAsOf = null) : IRequest<Result<InterestSimulationDto>>;

public sealed class SimulateInterestSettlementQueryValidator : AbstractValidator<SimulateInterestSettlementQuery>
{
    public SimulateInterestSettlementQueryValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
    }
}

public sealed class SimulateInterestSettlementQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    LiquidacionDeIntereses liquidacion,
    IDateTimeService reloj) : IRequestHandler<SimulateInterestSettlementQuery, Result<InterestSimulationDto>>
{
    public async Task<Result<InterestSimulationDto>> Handle(SimulateInterestSettlementQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<InterestSimulationDto>(leida.Error);
        var cuenta = leida.Value;
        var control = await db.MemberAccountControls.AsNoTracking().FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        var hasta = request.RecordedAsOf ?? reloj.UtcNow;
        var r = await liquidacion.CalcularAsync(cuenta, control, request.Date, hasta, desdeElMotor: true, ct);
        if (r.IsFailure) return Result.Failure<InterestSimulationDto>(r.Error);
        var c = r.Value;
        await auditoria.EmitirMovimientosDeCuentaVistosAsync(cuenta.PublicId, new { simulation = request.Date }, c.Explicacion?.Dias.Count ?? 0, ct);
        return Result.Success(new InterestSimulationDto(request.Date, hasta, c.Desde.AddDays(1), c.Hasta, c.Causado, c.Bruto, c.Redondeo,
            Liquidaciones.Dto(c.Retencion, c.Perfil), c.Neto,
            (c.Explicacion?.Dias ?? []).Select(d => new InterestExplanationDayDto(d.Dia, d.Saldo, d.Base, d.Tramo, d.TasaEA, d.TasaDiaria, d.Causado, d.BajoElMinimo,
                d.Suspendido, d.Explicacion)).ToList()));
    }
}

// ------------------------------------------------------------------------------------------------------ liquidación manual --

/// <summary>
/// La liquidación manual de un período vencido (feature 014, US5, T132, T139; api.md §4, <c>POST /{id}/settlements</c>, <c>Lending.Interest.Settle</c>, con
/// clave): el período tiene que estar vencido —<c>periodTo</c> es el fin de un período de liquidación de la versión y no es futuro
/// (<c>Lending.Savings.SettlementPeriodOpen</c>)— y causado hasta su fin (<c>Lending.Savings.AccrualBehind</c>). <b>Idempotente</b>: repetirla responde la
/// existente (SC-003, US5-3). Los períodos anteriores vencidos sin liquidar se liquidan primero, en orden. (nuevo)
/// </summary>
public sealed record SettleAccountInterestCommand(Guid AccountPublicId, DateOnly PeriodTo, string Reason) : IRequest<Result<InterestSettlementDto>>,
    IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SettleAccountInterestCommandValidator : ValidadorConMotivo<SettleAccountInterestCommand>
{
    public SettleAccountInterestCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.PeriodTo).NotEmpty();
    }
}

public sealed class SettleAccountInterestCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ICerrojoDeCartera cerrojo,
    LiquidacionDeIntereses liquidacion,
    LectorDeInteres lector,
    IDateTimeService reloj) : IRequestHandler<SettleAccountInterestCommand, Result<InterestSettlementDto>>
{
    public async Task<Result<InterestSettlementDto>> Handle(SettleAccountInterestCommand request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var vista = leida.Value;
        var existente = await (from s in db.InterestSettlements.AsNoTracking()
                               join o in db.MemberAccountOperations.AsNoTracking() on s.OperationId equals o.Id
                               where s.MemberAccountId == vista.Id && s.PeriodTo == request.PeriodTo && s.ReversedByOperationId == null && !s.IsDeleted
                               select new { s, o }).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Success(Liquidaciones.Dto(existente.s, existente.o, yaExistia: true));
        if (!LiquidacionDeIntereses.GanaInteres(vista))
            return Falla(new ErrorConDatos(LiquidacionDeIntereses.CodigoNoCausa, $"La cuenta {vista.Number} no liquida intereses por este camino.",
                new { depositClass = vista.DepositClass?.ToString() }));

        var hoy = reloj.HoyLocal;
        var versiones = await lector.VersionesAsync(vista.CreditLineId, ct);
        var version = LectorDeInteres.VersionDelDia(versiones, vista, request.PeriodTo, DateTime.MaxValue);
        var periodo = version is null ? null : InteresDeCaptacion.PeriodoDeLiquidacion(version.Version.SettlementPeriodicity, version.Version.SettlementDay, request.PeriodTo);
        if (request.PeriodTo > hoy || periodo is not { } p || p.Hasta != request.PeriodTo)
            return Falla(new ErrorConDatos(LiquidacionDeIntereses.CodigoPeriodoAbierto,
                $"El {ErroresDeCuentas.Fecha(request.PeriodTo)} no es el fin de un período de liquidación vencido de la cuenta {vista.Number}.",
                new { periodTo = request.PeriodTo, periodEnd = periodo?.Hasta }));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync([vista.Id], ModoDeEspera.Esperar, ct);
            var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == vista.Id, ct);
            var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == vista.Id, ct);
            if (control.LastSettledThrough is { } previo && request.PeriodTo <= previo)
                return Falla(new ErrorConDatos(LiquidacionDeIntereses.CodigoPeriodoAbierto,
                    $"La cuenta {cuenta.Number} ya está liquidada hasta el {ErroresDeCuentas.Fecha(previo)}.", new { lastSettledThrough = previo }));
            LiquidacionAplicada? ultima = null;
            foreach (var fin in (await liquidacion.PeriodosVencidosAsync(cuenta, control, request.PeriodTo, ct)).Where(f => f <= request.PeriodTo)
                         .DefaultIfEmpty(request.PeriodTo).Distinct())
            {
                var r = await liquidacion.LiquidarAsync(cuenta, control, fin, null, null, request.Reason, ct);
                if (r.IsFailure) return Falla(r.Error);
                ultima = r.Value;
            }
            await db.SaveChangesAsync(ct);
            if (ultima?.Liquidacion is not { } l || ultima.Operacion is not { } o)
                return Result.Success(new InterestSettlementDto(Guid.Empty, Guid.Empty, string.Empty, request.PeriodTo, request.PeriodTo, 0m, 0m, 0m, 0m, null, 0m, 0m,
                    RetencionSobreRendimientos.MotivoSinInteres, false, 0m, reloj.UtcNow, "User", false));
            return Result.Success(Liquidaciones.Dto(l, o, ultima.YaExistia));
        }, ct);
    }

    private static Result<InterestSettlementDto> Falla(Error e) => Result.Failure<InterestSettlementDto>(e);
}
