using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US15 (T217; contracts/api.md §15; research §24): los DTO del fondo de liquidez, del riesgo de liquidez y de lo constituido tal como los responde
// la API (los enums viajan como número). (nuevo)

public sealed record RenglonDelFondoDto(string DepositClass, string? Band, int Accounts, decimal Balance, decimal AccruedAndToAccrue, decimal Base, decimal Percent,
    decimal Required);

public sealed record TransicionDelFondoDto(DateOnly EndsOn, decimal NewRegimeRequired);

public sealed record RegimenDelFondoDto(string Source, string Stage, TransicionDelFondoDto? Transition);

/// <summary>La posición del fondo a una fecha. <c>Alert</c>: <c>None</c>, <c>Margin</c> o <c>Shortfall</c>. (nuevo)</summary>
public sealed record PosicionDelFondoDto(
    DateOnly Date,
    string Category,
    RegimenDelFondoDto Regime,
    IReadOnlyList<RenglonDelFondoDto> Base,
    decimal Required,
    decimal Constituted,
    decimal Difference,
    decimal Shortfall,
    decimal Margin,
    string Alert,
    IReadOnlyList<string> Explanation);

public sealed record BandaDeRiesgoDto(string Band, int? UpToDays, decimal Inflows, decimal ContractualOutflows, decimal NonContractualOutflows, decimal Gap,
    decimal CumulativeGap, decimal? CumulativeIrl);

/// <summary>La medición del riesgo de liquidez: <c>Irl</c> o <c>Brecha</c>. (nuevo)</summary>
public sealed record RiesgoDeLiquidezDto(
    DateOnly Date,
    string Measure,
    decimal NetLiquidAssets,
    IReadOnlyList<BandaDeRiesgoDto> Bands,
    decimal? MinimumIrl,
    string? MinimumIrlBand,
    decimal Threshold,
    decimal AdjustmentPlanThreshold,
    bool BelowThreshold,
    decimal OutsideHorizon,
    IReadOnlyList<string> Assumptions);

public sealed record FotoDelFondoDto(
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
    IReadOnlyList<RenglonDelFondoDto> Base,
    bool TakenByProcess,
    string? Reason,
    DateTime RegisteredAt,
    Guid? AlertPublicId);

public sealed record InversionDelFondoDto(
    Guid PublicId,
    int Kind,
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

public sealed record TomarFotoRequest(DateOnly? Date, string? Reason);

public sealed record RegistrarInversionRequest(int Instrument, Guid EntityPersonPublicId, string Reference, decimal Amount, DateOnly ConstitutedOn,
    DateOnly? MaturityDate, Guid? SupportAttachmentPublicId, string? Note, Guid? PublicId);

public sealed record TerminarInversionRequest(DateOnly EndedOn, string Reason);

/// <summary>Los textos del fondo de liquidez en pantalla. (nuevo)</summary>
public static class TextosDeLiquidez
{
    /// <summary>El tipo de dueño del soporte de una inversión (<c>AdjuntosDeModulo.SoporteDelFondoDeLiquidez</c>).</summary>
    public const string DuenoDeSoportes = "LendingLiquidityFundSupport";

    public static readonly IReadOnlyList<(int Valor, string Texto)> Instrumentos =
    [
        (1, "Cuenta de ahorros o corriente"), (2, "CDT o CDAT"), (3, "Fondo de inversión colectiva"), (4, "TES clase B"), (99, "Otra"),
    ];

    public static string Instrumento(int clase) => Instrumentos.FirstOrDefault(i => i.Valor == clase).Texto ?? clase.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Clase(string clase) => clase switch
    {
        "ALaVista" => "Ahorro a la vista",
        "Cdat" => "CDAT",
        "Contractual" => "Ahorro contractual",
        "PermanenteConRetiroParcial" => "Ahorro permanente (retiro parcial)",
        "PermanenteSoloAlRetirarse" => "Ahorro permanente (sólo al retirarse)",
        _ => clase,
    };

    public static string Etapa(string etapa) => etapa switch
    {
        "General" => "Régimen general",
        "PorMadurez" => "Por madurez residual (categoría plena)",
        "Transicion" => "Transición (rige el general)",
        _ => etapa,
    };

    public static string Alerta(string alerta) => alerta switch
    {
        "Shortfall" => "Insuficiente: el constituido no alcanza el requerido",
        "Margin" => "En el margen: cubre el requerido pero no el colchón de la entidad",
        _ => "Cubierto",
    };
}

public sealed partial class CaptacionesClient
{
    public const string RutaDelFondoDeLiquidez = Base + "/liquidity-fund";

    public Task<ResultadoDeInventario<PosicionDelFondoDto>> PosicionDelFondoAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<PosicionDelFondoDto>(HttpMethod.Get, ConQuery(RutaDelFondoDeLiquidez, Query(("date", Texto(fecha)))), null, null, ct);

    /// <summary>La medición del riesgo: <c>Irl</c> o <c>Brecha</c>.</summary>
    public Task<ResultadoDeInventario<RiesgoDeLiquidezDto>> RiesgoDeLiquidezAsync(DateOnly? fecha = null, string medida = "Irl", CancellationToken ct = default) =>
        Enviar<RiesgoDeLiquidezDto>(HttpMethod.Get, ConQuery($"{RutaDelFondoDeLiquidez}/risk", Query(("date", Texto(fecha)), ("measure", medida))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<FotoDelFondoDto>>> FotosDelFondoAsync(DateOnly? desde = null, DateOnly? hasta = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<FotoDelFondoDto>>(HttpMethod.Get, ConQuery($"{RutaDelFondoDeLiquidez}/snapshots", Query(("from", Texto(desde)), ("to", Texto(hasta)))),
            null, null, ct);

    public Task<ResultadoDeInventario<FotoDelFondoDto>> TomarFotoDelFondoAsync(TomarFotoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<FotoDelFondoDto>(HttpMethod.Post, $"{RutaDelFondoDeLiquidez}/snapshots", request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<InversionDelFondoDto>>> InversionesDelFondoAsync(DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<InversionDelFondoDto>>(HttpMethod.Get, ConQuery($"{RutaDelFondoDeLiquidez}/holdings", Query(("date", Texto(fecha)))), null, null, ct);

    public Task<ResultadoDeInventario<InversionDelFondoDto>> RegistrarInversionDelFondoAsync(RegistrarInversionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<InversionDelFondoDto>(HttpMethod.Post, $"{RutaDelFondoDeLiquidez}/holdings", request, clave, ct);

    public Task<ResultadoDeInventario<InversionDelFondoDto>> TerminarInversionDelFondoAsync(Guid inversion, TerminarInversionRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<InversionDelFondoDto>(HttpMethod.Post, $"{RutaDelFondoDeLiquidez}/holdings/{inversion}/end", request, clave, ct);
}
