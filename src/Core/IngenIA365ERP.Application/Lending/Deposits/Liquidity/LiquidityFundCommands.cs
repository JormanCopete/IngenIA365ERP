using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Liquidity;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Liquidity;

// Feature 014, US15 (T216; contracts/api.md §15; FR-025, FR-109, FR-110; research §24): la posición del fondo de liquidez a una fecha, la medición del riesgo de
// liquidez (IRL y brecha), las fotos inmutables con sus alertas y lo constituido que registra el tesorero. Sin datos de titulares. (nuevo)

/// <summary>Un renglón de la base del fondo (api.md §15). (nuevo)</summary>
public sealed record LiquidityFundBaseRowDto(
    string DepositClass,
    string? Band,
    int Accounts,
    decimal Balance,
    decimal AccruedAndToAccrue,
    decimal Base,
    decimal Percent,
    decimal Required);

/// <summary>La transición del régimen: hasta cuándo y lo que requeriría el régimen nuevo. (nuevo)</summary>
public sealed record LiquidityFundTransitionDto(DateOnly EndsOn, decimal NewRegimeRequired);

/// <summary>El régimen que rige: la fuente, la etapa y la transición si la hay. (nuevo)</summary>
public sealed record LiquidityFundRegimeDto(string Source, string Stage, LiquidityFundTransitionDto? Transition);

/// <summary>La posición del fondo a una fecha (api.md §15 <c>GET /</c>): sin titulares. <c>Alert</c>: <c>None</c>, <c>Margin</c> o <c>Shortfall</c>. (nuevo)</summary>
public sealed record LiquidityFundPositionDto(
    DateOnly Date,
    string Category,
    LiquidityFundRegimeDto Regime,
    IReadOnlyList<LiquidityFundBaseRowDto> Base,
    decimal Required,
    decimal Constituted,
    decimal Difference,
    decimal Shortfall,
    decimal Margin,
    string Alert,
    IReadOnlyList<string> Explanation);

/// <summary>Una banda de la medición del riesgo de liquidez. (nuevo)</summary>
public sealed record LiquidityRiskBandDto(
    string Band,
    int? UpToDays,
    decimal Inflows,
    decimal ContractualOutflows,
    decimal NonContractualOutflows,
    decimal Gap,
    decimal CumulativeGap,
    decimal? CumulativeIrl);

/// <summary>La medición del riesgo de liquidez (research §24): IRL o brecha, con su umbral y los supuestos. (nuevo)</summary>
public sealed record LiquidityRiskDto(
    DateOnly Date,
    string Measure,
    decimal NetLiquidAssets,
    IReadOnlyList<LiquidityRiskBandDto> Bands,
    decimal? MinimumIrl,
    string? MinimumIrlBand,
    decimal Threshold,
    decimal AdjustmentPlanThreshold,
    bool BelowThreshold,
    decimal OutsideHorizon,
    IReadOnlyList<string> Assumptions);

/// <summary>Una foto del fondo (<c>LND_LiquidityFundSnapshots</c>). (nuevo)</summary>
public sealed record LiquidityFundSnapshotDto(
    Guid PublicId,
    DateOnly Date,
    int Version,
    string Category,
    string RegimeStage,
    decimal Required,
    decimal? NewRegimeRequired,
    decimal Constituted,
    decimal Shortfall,
    decimal? MinimumIrl,
    IReadOnlyList<LiquidityFundBaseRowDto> Base,
    bool TakenByProcess,
    string? Reason,
    DateTime RegisteredAt,
    Guid? AlertPublicId);

/// <summary>Una inversión del fondo (<c>LND_LiquidityFundHoldings</c>). (nuevo)</summary>
public sealed record LiquidityFundHoldingDto(
    Guid PublicId,
    LiquidityHoldingKind Kind,
    Guid InstitutionPersonPublicId,
    string InstitutionName,
    string Reference,
    decimal Amount,
    DateOnly ConstitutedOn,
    DateOnly? MaturesOn,
    DateOnly? WithdrawnOn,
    Guid SupportAttachmentPublicId,
    string Note,
    string? EndReason,
    bool InForce);

/// <summary>Las proyecciones de las respuestas del fondo. (nuevo)</summary>
public static class VistaDelFondoDeLiquidez
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<LiquidityFundBaseRowDto> Base(CalculoDelFondoDeLiquidez calculo) =>
        calculo.Renglones.Select(r => new LiquidityFundBaseRowDto(r.Clase.ToString(), r.Banda, r.Cuentas, r.Capital, r.Intereses, r.Base, r.Porcentaje, r.Requerido))
            .ToList();

    public static string Alerta(NivelDelFondoDeLiquidez nivel) => nivel switch
    {
        NivelDelFondoDeLiquidez.Insuficiente => "Shortfall",
        NivelDelFondoDeLiquidez.EnElMargen => "Margin",
        _ => "None",
    };

    public static LiquidityFundPositionDto Posicion(PosicionDelFondoDeLiquidez p) => new(p.Fecha, p.Categoria,
        new LiquidityFundRegimeDto(p.Regla.Fuente, p.Etapa,
            p.Calculo.RegimenNuevoEnTransicion is { } nuevo && p.Calculo.FinDeLaTransicion is { } fin ? new LiquidityFundTransitionDto(fin, nuevo.Requerido) : null),
        Base(p.Calculo), p.Calculo.Requerido, p.Constituido, p.Diferencia, p.Faltante, p.Margen, Alerta(p.Nivel), p.Calculo.Explicacion);

    public static LiquidityRiskDto Riesgo(MedicionDeLiquidezALaFecha m) => new(m.Fecha, m.Medida.ToString(), m.Medicion.ActivosLiquidosNetos,
        m.Medicion.Bandas.Select(b => new LiquidityRiskBandDto(b.Banda, b.HastaDias, b.Entradas, b.SalidasContractuales, b.SalidasNoContractuales, b.Brecha,
            b.BrechaAcumulada, b.IrlAcumulado)).ToList(),
        m.Medicion.IrlMinimo, m.Medicion.BandaDelIrlMinimo, m.Umbral, m.UmbralDePlanDeAjuste, m.BajoElUmbral, m.Medicion.FueraDelHorizonte, m.Supuestos);

    public static LiquidityFundSnapshotDto Foto(LiquidityFundSnapshot s) => new(s.PublicId, s.SnapshotDate, s.Version, s.Category, s.RegimeStage,
        s.RequiredAmount, s.NewRegimeRequiredAmount, s.ConstitutedAmount, s.Shortfall, s.MinimumIrl,
        JsonSerializer.Deserialize<List<LiquidityFundBaseRowDto>>(s.BaseByClassJson, Json) ?? [], s.ProcessRunId is not null, s.Reason, s.RegisteredAt,
        s.AlertPublicId);

    public static string BaseJson(CalculoDelFondoDeLiquidez calculo) => JsonSerializer.Serialize(Base(calculo), Json);

    public static string RiesgoJson(MedicionDeLiquidezALaFecha m) => JsonSerializer.Serialize(Riesgo(m), Json);

    public static async Task<IReadOnlyList<LiquidityFundHoldingDto>> InversionesAsync(IApplicationDbContext db, IReadOnlyList<LiquidityFundHolding> inversiones,
        DateOnly hoy, CancellationToken ct)
    {
        var ids = inversiones.Select(h => h.InstitutionPersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.PublicId, Nombre: ReferenciasDeGobierno.Nombre(p)), ct);
        return inversiones.Select(h =>
        {
            var p = personas.GetValueOrDefault(h.InstitutionPersonId);
            return new LiquidityFundHoldingDto(h.PublicId, h.Kind, p.PublicId, p.Nombre ?? string.Empty, h.Reference, h.Amount, h.ConstitutedOn, h.MaturesOn,
                h.WithdrawnOn, h.SupportAttachmentPublicId, h.Note, h.EndReason, h.VigenteEn(hoy));
        }).ToList();
    }
}

/// <summary>
/// El alcance del fondo de liquidez (feature 014, US15, T216): el fondo, el riesgo de liquidez y lo constituido son de toda la cooperativa —cifras agregadas, sin
/// titulares ni oficina—, así que los consulta y opera quien tiene alcance total (todas las oficinas y todas las líneas), como la decisión de asamblea. (nuevo)
/// </summary>
public static class AlcanceDelFondoDeLiquidez
{
    public const string CodigoSinAlcanceTotal = "Lending.LiquidityFund.ScopeNotTotal";

    /// <summary>Nulo si quien pregunta tiene alcance total; si no, el error.</summary>
    public static async Task<Error?> TotalAsync(IAlcanceDeCartera alcance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(alcance);
        var vigente = await alcance.ObtenerAsync(ct);
        return vigente.TodasLasOficinas && vigente.TodasLasLineas
            ? null
            : new Error(CodigoSinAlcanceTotal, "El fondo de liquidez es de toda la cooperativa: lo consulta y lo opera quien tiene alcance sobre todas las oficinas y líneas.");
    }
}

// ------------------------------------------------------------------------------------------------------------ fotos --

/// <summary>
/// La foto del fondo de liquidez de una fecha (feature 014, US15, T216; data-model §6.7): la posición, la medición del IRL y las alertas
/// —<c>Cartera.FondoDeLiquidezInsuficiente</c> si el constituido no alcanza el requerido (US15-1, al tesorero y a la gerencia con la diferencia),
/// <c>Cartera.FondoDeLiquidezEnElMargen</c> si no supera el colchón y <c>Cartera.RiesgoDeLiquidezBajo</c> si el IRL de alguna banda queda bajo el mínimo—. Una
/// fecha tiene una sola foto: pedirla otra vez devuelve la que ya está. La usan la pantalla y el ciclo diario. Guarda. (nuevo)
/// </summary>
public sealed class FotosDelFondoDeLiquidez(
    IApplicationDbContext db,
    FondoDeLiquidezDeCartera fondo,
    IActorActual actorActual,
    IDateTimeService reloj,
    IAlertas? alertas = null,
    ILogger<FotosDelFondoDeLiquidez>? logger = null)
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public async Task<Result<(LiquidityFundSnapshot Foto, bool Nueva)>> TomarAsync(DateOnly fecha, string? motivo, int? corrida, CancellationToken ct)
    {
        var existente = await db.LiquidityFundSnapshots.AsNoTracking().Where(s => s.SnapshotDate == fecha).OrderByDescending(s => s.Version).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Success((existente, false));

        var posicion = await fondo.PosicionAsync(fecha, ct);
        if (posicion.IsFailure) return Result.Failure<(LiquidityFundSnapshot, bool)>(posicion.Error);
        var p = posicion.Value;
        var irl = await fondo.RiesgoAsync(fecha, MedidaDeRiesgoDeLiquidez.Irl, ct);
        var actor = corrida is null ? await actorActual.ObtenerAsync(ct) : null;

        Guid? alerta = null;
        if (p.Nivel == NivelDelFondoDeLiquidez.Insuficiente)
            alerta = await AlertarAsync(TiposDeAlerta.FondoDeLiquidezInsuficiente, $"Fondo de liquidez insuficiente al {fecha:yyyy-MM-dd}",
                $"El constituido ({p.Constituido:N2}) es menor que el requerido ({p.Calculo.Requerido:N2}): faltan {p.Faltante:N2}. Categoría {p.Categoria}, "
                + $"régimen {p.Etapa}. Recomponga el fondo y, si se usó para atender retiros, avise a la Supersolidaria con el plan de reconstitución.",
                fecha, ct);
        else if (p.Nivel == NivelDelFondoDeLiquidez.EnElMargen)
            alerta = await AlertarAsync(TiposDeAlerta.FondoDeLiquidezEnElMargen, $"Fondo de liquidez en el margen al {fecha:yyyy-MM-dd}",
                $"El constituido ({p.Constituido:N2}) cubre el requerido ({p.Calculo.Requerido:N2}) pero no el colchón de {p.Margen:P2} que fijó la entidad.",
                fecha, ct);
        if (irl.BajoElUmbral)
        {
            var deIrl = await AlertarAsync(TiposDeAlerta.RiesgoDeLiquidezBajo, $"IRL bajo el mínimo al {fecha:yyyy-MM-dd}",
                $"El IRL acumulado de la banda {irl.Medicion.BandaDelIrlMinimo} es {irl.Medicion.IrlMinimo:N4} (mínimo {irl.Umbral:N2}). La Circular Básica Contable "
                + $"y Financiera pide un plan de ajuste para volver a {irl.UmbralDePlanDeAjuste:N2} antes del siguiente corte.", fecha, ct);
            alerta ??= deIrl;
        }

        var foto = new LiquidityFundSnapshot
        {
            SnapshotDate = fecha,
            Version = 1,
            RegisteredAt = reloj.UtcNow,
            Category = Recortar(p.Categoria, LiquidityFundSnapshot.LargoDeLaCategoria),
            RegimeStage = p.Etapa,
            BaseByClassJson = VistaDelFondoDeLiquidez.BaseJson(p.Calculo),
            RequiredAmount = p.Calculo.Requerido,
            NewRegimeRequiredAmount = p.Calculo.RegimenNuevoEnTransicion?.Requerido,
            ConstitutedAmount = p.Constituido,
            Shortfall = p.Faltante,
            MinimumIrl = irl.Medicion.IrlMinimo,
            LiquidityRiskJson = VistaDelFondoDeLiquidez.RiesgoJson(irl),
            ParametersSnapshotJson = p.ParametrosJson,
            ProcessRunId = corrida,
            RequestedByUserId = actor?.UserId,
            Reason = motivo is null ? null : Recortar(motivo, LiquidityFundSnapshot.LargoDelMotivo),
            AlertPublicId = alerta,
            InputsSha256 = p.Huella,
        };
        db.LiquidityFundSnapshots.Add(foto);
        await db.SaveChangesAsync(ct);
        return Result.Success((foto, true));
    }

    private async Task<Guid?> AlertarAsync(string tipo, string asunto, string cuerpo, DateOnly fecha, CancellationToken ct)
    {
        if (alertas is null) return null;
        try
        {
            var r = await alertas.LevantarAsync(new AlertaALevantar(tipo, asunto, cuerpo, nameof(LiquidityFundSnapshot), null,
                DedupKey: $"{tipo}:{fecha:yyyyMMdd}", RecipientPermissions: TiposDeAlerta.DestinatariosDeLiquidez, ModuloDeOrigen: "Lending"), ct);
            return r.IsSuccess ? r.Value.AlertPublicId : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.FondoDeLiquidez] La alerta {Tipo} del {Fecha} no se levantó.", tipo, fecha);
            return null;
        }
    }

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}

// --------------------------------------------------------------------------------------------------------- consultas --

/// <summary><c>GET /api/lending/liquidity-fund?date=</c> (<c>Lending.LiquidityFund.View</c>): la posición del fondo a una fecha (hoy si no viene). (nuevo)</summary>
public sealed record GetLiquidityFundPositionQuery(DateOnly? Date) : IRequest<Result<LiquidityFundPositionDto>>;

public sealed class GetLiquidityFundPositionQueryHandler(FondoDeLiquidezDeCartera fondo, IAlcanceDeCartera alcance, IDateTimeService reloj)
    : IRequestHandler<GetLiquidityFundPositionQuery, Result<LiquidityFundPositionDto>>
{
    public async Task<Result<LiquidityFundPositionDto>> Handle(GetLiquidityFundPositionQuery request, CancellationToken ct)
    {
        if (await AlcanceDelFondoDeLiquidez.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<LiquidityFundPositionDto>(fuera);
        var r = await fondo.PosicionAsync(request.Date ?? reloj.HoyLocal, ct);
        return r.IsFailure ? Result.Failure<LiquidityFundPositionDto>(r.Error) : Result.Success(VistaDelFondoDeLiquidez.Posicion(r.Value));
    }
}

/// <summary>
/// <c>GET /api/lending/liquidity-fund/risk?date=&amp;measure=</c> (<c>Lending.LiquidityFund.View</c>; research §24, nueva en contracts): el IRL (<c>Irl</c>,
/// defecto) o la brecha de liquidez (<c>Gap</c>) a una fecha. (nuevo)
/// </summary>
public sealed record GetLiquidityRiskQuery(DateOnly? Date, MedidaDeRiesgoDeLiquidez Measure = MedidaDeRiesgoDeLiquidez.Irl) : IRequest<Result<LiquidityRiskDto>>;

public sealed class GetLiquidityRiskQueryValidator : AbstractValidator<GetLiquidityRiskQuery>
{
    public GetLiquidityRiskQueryValidator() => RuleFor(x => x.Measure).IsInEnum();
}

public sealed class GetLiquidityRiskQueryHandler(FondoDeLiquidezDeCartera fondo, IAlcanceDeCartera alcance, IDateTimeService reloj)
    : IRequestHandler<GetLiquidityRiskQuery, Result<LiquidityRiskDto>>
{
    public async Task<Result<LiquidityRiskDto>> Handle(GetLiquidityRiskQuery request, CancellationToken ct)
    {
        if (await AlcanceDelFondoDeLiquidez.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<LiquidityRiskDto>(fuera);
        return Result.Success(VistaDelFondoDeLiquidez.Riesgo(await fondo.RiesgoAsync(request.Date ?? reloj.HoyLocal, request.Measure, ct)));
    }
}

/// <summary><c>GET /api/lending/liquidity-fund/snapshots?from=&amp;to=</c> (<c>Lending.LiquidityFund.View</c>): las fotos del rango (los últimos 90 días si no viene). (nuevo)</summary>
public sealed record ListLiquidityFundSnapshotsQuery(DateOnly? From, DateOnly? To) : IRequest<Result<IReadOnlyList<LiquidityFundSnapshotDto>>>;

public sealed class ListLiquidityFundSnapshotsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, IDateTimeService reloj)
    : IRequestHandler<ListLiquidityFundSnapshotsQuery, Result<IReadOnlyList<LiquidityFundSnapshotDto>>>
{
    private const int DiasPorDefecto = 90;

    public async Task<Result<IReadOnlyList<LiquidityFundSnapshotDto>>> Handle(ListLiquidityFundSnapshotsQuery request, CancellationToken ct)
    {
        if (await AlcanceDelFondoDeLiquidez.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<IReadOnlyList<LiquidityFundSnapshotDto>>(fuera);
        var hasta = request.To ?? reloj.HoyLocal;
        var desde = request.From ?? hasta.AddDays(-DiasPorDefecto);
        if (desde > hasta) return Result.Failure<IReadOnlyList<LiquidityFundSnapshotDto>>(ErroresDelFondoDeLiquidez.Invalido("from", "El rango empieza después de terminar."));
        var fotos = await db.LiquidityFundSnapshots.AsNoTracking().Where(s => s.SnapshotDate >= desde && s.SnapshotDate <= hasta)
            .OrderByDescending(s => s.SnapshotDate).ThenByDescending(s => s.Version).ToListAsync(ct);
        return Result.Success<IReadOnlyList<LiquidityFundSnapshotDto>>(fotos.Select(VistaDelFondoDeLiquidez.Foto).ToList());
    }
}

/// <summary>
/// <c>GET /api/lending/liquidity-fund/holdings?date=</c> (<c>Lending.LiquidityFund.View</c>): lo constituido —con fecha, las inversiones vigentes ese día; sin
/// ella, todas, las terminadas al final—. (nuevo)
/// </summary>
public sealed record ListLiquidityFundHoldingsQuery(DateOnly? Date) : IRequest<Result<IReadOnlyList<LiquidityFundHoldingDto>>>;

public sealed class ListLiquidityFundHoldingsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, IDateTimeService reloj)
    : IRequestHandler<ListLiquidityFundHoldingsQuery, Result<IReadOnlyList<LiquidityFundHoldingDto>>>
{
    public async Task<Result<IReadOnlyList<LiquidityFundHoldingDto>>> Handle(ListLiquidityFundHoldingsQuery request, CancellationToken ct)
    {
        if (await AlcanceDelFondoDeLiquidez.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<IReadOnlyList<LiquidityFundHoldingDto>>(fuera);
        var consulta = db.LiquidityFundHoldings.AsNoTracking().Where(h => !h.IsDeleted);
        if (request.Date is { } d) consulta = consulta.Where(h => h.ConstitutedOn <= d && (h.WithdrawnOn == null || h.WithdrawnOn > d));
        var lista = await consulta.OrderBy(h => h.WithdrawnOn != null).ThenBy(h => h.ConstitutedOn).ThenBy(h => h.Id).ToListAsync(ct);
        return Result.Success(await VistaDelFondoDeLiquidez.InversionesAsync(db, lista, request.Date ?? reloj.HoyLocal, ct));
    }
}

// ---------------------------------------------------------------------------------------------------------- comandos --

/// <summary>
/// <c>POST /api/lending/liquidity-fund/snapshots</c> (<c>TakeLiquidityFundSnapshotCommand</c>, <c>Lending.LiquidityFund.Manage</c>): toma la foto de una fecha
/// (no futura) con sus alertas; repetir la misma fecha devuelve la existente. (nuevo)
/// </summary>
public sealed record TakeLiquidityFundSnapshotCommand(DateOnly? Date, string? Reason) : IRequest<Result<LiquidityFundSnapshotDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class TakeLiquidityFundSnapshotCommandValidator : AbstractValidator<TakeLiquidityFundSnapshotCommand>
{
    public TakeLiquidityFundSnapshotCommandValidator() => RuleFor(x => x.Reason).MaximumLength(LiquidityFundSnapshot.LargoDelMotivo);
}

public sealed class TakeLiquidityFundSnapshotCommandHandler(FotosDelFondoDeLiquidez fotos, IAlcanceDeCartera alcance, IDateTimeService reloj)
    : IRequestHandler<TakeLiquidityFundSnapshotCommand, Result<LiquidityFundSnapshotDto>>
{
    public async Task<Result<LiquidityFundSnapshotDto>> Handle(TakeLiquidityFundSnapshotCommand request, CancellationToken ct)
    {
        if (await AlcanceDelFondoDeLiquidez.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<LiquidityFundSnapshotDto>(fuera);
        var hoy = reloj.HoyLocal;
        var fecha = request.Date ?? hoy;
        if (fecha > hoy)
            return Result.Failure<LiquidityFundSnapshotDto>(ErroresDelFondoDeLiquidez.Invalido("date", $"La foto es de una fecha pasada o de hoy ({hoy:yyyy-MM-dd})."));
        var r = await fotos.TomarAsync(fecha, string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(), null, ct);
        return r.IsFailure ? Result.Failure<LiquidityFundSnapshotDto>(r.Error) : Result.Success(VistaDelFondoDeLiquidez.Foto(r.Value.Foto));
    }
}

/// <summary>
/// <c>POST /api/lending/liquidity-fund/holdings</c> (<c>RegisterLiquidityFundHoldingCommand</c>, <c>Lending.LiquidityFund.Manage</c>): registra una inversión del
/// fondo —clase, entidad depositaria, número del título o de la cuenta, valor, desde cuándo, vencimiento y el soporte subido antes con dueño
/// <paramref name="PublicId"/> o con el que cita—. (nuevo)
/// </summary>
public sealed record RegisterLiquidityFundHoldingCommand(
    LiquidityHoldingKind Instrument,
    Guid EntityPersonPublicId,
    string Reference,
    decimal Amount,
    DateOnly ConstitutedOn,
    DateOnly? MaturityDate,
    Guid? SupportAttachmentPublicId,
    string? Note,
    Guid? PublicId = null) : IRequest<Result<LiquidityFundHoldingDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterLiquidityFundHoldingCommandValidator : AbstractValidator<RegisterLiquidityFundHoldingCommand>
{
    public RegisterLiquidityFundHoldingCommandValidator()
    {
        RuleFor(x => x.Instrument).IsInEnum();
        RuleFor(x => x.EntityPersonPublicId).NotEmpty().WithMessage("Indique la entidad depositaria.");
        RuleFor(x => x.Reference).NotEmpty().WithMessage("Indique el número del título, de la cuenta o de la participación.")
            .MaximumLength(LiquidityFundHolding.LargoDeLaReferencia);
        RuleFor(x => x.Amount).GreaterThan(0m).Must(m => decimal.Round(m, 2) == m).WithMessage("El valor es mayor que cero y lleva a lo sumo dos decimales.");
        RuleFor(x => x.ConstitutedOn).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo está constituida.");
        RuleFor(x => x.MaturityDate).Must((x, v) => v is null || v >= x.ConstitutedOn).WithMessage("El vencimiento no es anterior a la constitución.");
        RuleFor(x => x.Note).MaximumLength(LiquidityFundHolding.LargoDeLaNota);
    }
}

public sealed class RegisterLiquidityFundHoldingCommandHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RegisterLiquidityFundHoldingCommand, Result<LiquidityFundHoldingDto>>
{
    public async Task<Result<LiquidityFundHoldingDto>> Handle(RegisterLiquidityFundHoldingCommand request, CancellationToken ct)
    {
        if (await AlcanceDelFondoDeLiquidez.TotalAsync(alcance, ct) is { } fuera) return Falla(fuera);
        var hoy = reloj.HoyLocal;
        if (request.ConstitutedOn > hoy)
            return Falla(ErroresDelFondoDeLiquidez.Invalido("constitutedOn", "Lo constituido se registra desde una fecha pasada o de hoy."));
        if (request.SupportAttachmentPublicId is not { } soporte) return Falla(ErroresDelFondoDeLiquidez.SoporteRequerido());
        var adjunto = await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == soporte && !a.IsDeleted && a.Status == EstadoDeAdjunto.Available
                                                                       && a.OwnerEntityType == AdjuntosDeModulo.SoporteDelFondoDeLiquidez, ct);
        if (!adjunto) return Falla(ErroresDelFondoDeLiquidez.SoporteRequerido());
        var entidad = await db.People.AsNoTracking().Where(p => p.PublicId == request.EntityPersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (entidad is null) return Falla(ErroresDelFondoDeLiquidez.Invalido("entityPersonPublicId", "La entidad depositaria no está registrada como persona."));
        if (request.PublicId is { } reservado && await db.LiquidityFundHoldings.AsNoTracking().IgnoreQueryFilters().AnyAsync(h => h.PublicId == reservado, ct))
            return Falla(ErroresDelFondoDeLiquidez.Invalido("publicId", "Ya hay una inversión registrada con ese identificador."));
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelFondoDeLiquidez.Invalido("user", "No se sabe quién registra la inversión."));

        var inversion = new LiquidityFundHolding
        {
            PublicId = request.PublicId ?? Guid.NewGuid(),
            Kind = request.Instrument,
            InstitutionPersonId = entidad.Value,
            Reference = request.Reference.Trim(),
            Amount = request.Amount,
            ConstitutedOn = request.ConstitutedOn,
            MaturesOn = request.MaturityDate,
            SupportAttachmentPublicId = soporte,
            Note = request.Note?.Trim() ?? string.Empty,
            RegisteredByUserId = usuario,
        };
        db.LiquidityFundHoldings.Add(inversion);
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDelFondoDeLiquidez.InversionesAsync(db, [inversion], hoy, ct))[0]);
    }

    private static Result<LiquidityFundHoldingDto> Falla(Error e) => Result.Failure<LiquidityFundHoldingDto>(e);
}

/// <summary>
/// <c>POST /api/lending/liquidity-fund/holdings/{holdingId}/end</c> (<c>EndLiquidityFundHoldingCommand</c>, <c>Lending.LiquidityFund.Manage</c>): la inversión deja
/// de contar desde <paramref name="EndedOn"/> (retiro, redención o vencimiento sin renovar), con su motivo. No se borra. (nuevo)
/// </summary>
public sealed record EndLiquidityFundHoldingCommand(Guid HoldingPublicId, DateOnly EndedOn, string Reason)
    : IRequest<Result<LiquidityFundHoldingDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class EndLiquidityFundHoldingCommandValidator : ValidadorConMotivo<EndLiquidityFundHoldingCommand>
{
    public EndLiquidityFundHoldingCommandValidator()
    {
        RuleFor(x => x.HoldingPublicId).NotEmpty();
        RuleFor(x => x.EndedOn).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo deja de contar.");
    }
}

public sealed class EndLiquidityFundHoldingCommandHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<EndLiquidityFundHoldingCommand, Result<LiquidityFundHoldingDto>>
{
    public async Task<Result<LiquidityFundHoldingDto>> Handle(EndLiquidityFundHoldingCommand request, CancellationToken ct)
    {
        if (await AlcanceDelFondoDeLiquidez.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<LiquidityFundHoldingDto>(fuera);
        var inversion = await db.LiquidityFundHoldings.FirstOrDefaultAsync(h => h.PublicId == request.HoldingPublicId && !h.IsDeleted, ct);
        if (inversion is null) return Result.Failure<LiquidityFundHoldingDto>(ErroresDelFondoDeLiquidez.Inexistente());
        if (inversion.WithdrawnOn is { } ya) return Result.Failure<LiquidityFundHoldingDto>(ErroresDelFondoDeLiquidez.YaTerminada(ya));
        var hoy = reloj.HoyLocal;
        if (request.EndedOn > hoy.AddDays(1) || request.EndedOn < inversion.ConstitutedOn)
            return Result.Failure<LiquidityFundHoldingDto>(ErroresDelFondoDeLiquidez.Invalido("endedOn",
                $"La inversión deja de contar entre su constitución ({inversion.ConstitutedOn:yyyy-MM-dd}) y mañana."));
        var actor = await actorActual.ObtenerAsync(ct);
        try
        {
            inversion.Terminar(request.EndedOn, request.Reason.Trim(), actor.UserId);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<LiquidityFundHoldingDto>(ErroresDelFondoDeLiquidez.Invalido("endedOn", ex.Problema.Mensaje));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDelFondoDeLiquidez.InversionesAsync(db, [inversion], hoy, ct))[0]);
    }
}
