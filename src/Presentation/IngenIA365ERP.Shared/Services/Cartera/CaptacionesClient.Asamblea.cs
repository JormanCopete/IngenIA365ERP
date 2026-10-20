using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US12 (T205; contracts/api.md §12; research §22): los DTO de la asamblea tal como los responde la API (los enums viajan como número). (nuevo)

/// <summary>La línea de un asociado en una simulación o en lo aplicado. (nuevo)</summary>
public sealed record LineaDeAsambleaDto(
    PersonaDeCuentaDto Person,
    Guid ContributionAccountPublicId,
    string ContributionAccountNumber,
    decimal BaseAmount,
    decimal CalculatedAmount,
    decimal AppliedAmount,
    bool LimitedByMinimum,
    Guid? DestinationAccountPublicId,
    string? DestinationAccountNumber,
    string? OperationNumber,
    string? PayoutOrderNumber,
    bool WithdrawnDuringYear = false,
    decimal WithheldAmount = 0m);

public sealed record LimiteLegalDeAsambleaDto(bool Applied, decimal? Ipc, string? Source, DateOnly? ReferenceDate);

public sealed record ProrrateoDeAsambleaDto(bool Applied, decimal? Factor);

public sealed record TotalesDeAsambleaDto(decimal Base, decimal Calculated, decimal Applied, decimal FundRemainder, int Lines, decimal Withheld = 0m);

/// <summary>La simulación de una decisión de asamblea, con la huella que la aplicación compara. (nuevo)</summary>
public sealed record SimulacionDeAsambleaDto(
    Guid PublicId,
    DateOnly AsOf,
    decimal DecidedRate,
    decimal AppliedRate,
    IReadOnlyList<LineaDeAsambleaDto> Lines,
    LimiteLegalDeAsambleaDto LimitedByIpc,
    ProrrateoDeAsambleaDto ProratedToFund,
    TotalesDeAsambleaDto Totals,
    IReadOnlyList<string> Explanation,
    IReadOnlyList<string> Warnings,
    string Sha256);

/// <summary>Una decisión de asamblea. (nuevo)</summary>
public sealed record DecisionDeAsambleaDto(
    Guid PublicId,
    string Number,
    int Kind,
    int Year,
    int Status,
    int Base,
    decimal? Percent,
    decimal? Value,
    decimal Fund,
    int? Destination,
    string? DestinationLineCode,
    decimal RoundingUnit,
    Guid? SessionItemPublicId,
    string? SessionNumber,
    Guid? CorrectsPublicId,
    string? CorrectsNumber,
    decimal? LegalLimit,
    string? LegalLimitSource,
    decimal CalculatedTotal,
    decimal? ProrationFactor,
    decimal AppliedTotal,
    IReadOnlyList<Guid> SupportAttachmentPublicIds,
    string Reason,
    DateTime RegisteredAt,
    DateTime? AppliedAt,
    DateOnly? AppliedOn,
    DateTime? ReversedAt,
    string? ReversalReason,
    DateTime? VoidedAt,
    IReadOnlyList<LineaDeAsambleaDto> Lines,
    string PercentOrValue);

/// <summary>El cuerpo del registro de una decisión de asamblea (el porcentaje como fracción). (nuevo)</summary>
public sealed record RegistrarDecisionDeAsambleaRequest(int Kind, int Year, decimal FundAmount, decimal? Percent, decimal? Value, int? Base, int? Destination,
    string? TargetDepositLineCode, Guid? CorrectsPublicId, string Reason);

/// <summary>El cuerpo de la aplicación: el punto del acta, la huella de la simulación, los soportes y el motivo. (nuevo)</summary>
public sealed record AplicarDecisionDeAsambleaRequest(Guid? SessionItemPublicId, string ExpectedPreviewSha256, IReadOnlyList<Guid> SupportAttachmentPublicIds,
    string Reason);

/// <summary>Los textos de la asamblea en pantalla. (nuevo)</summary>
public static class TextosDeAsamblea
{
    public const string DuenoDeSoportes = "LendingAssemblySupport";

    public static string Clase(int clase) => clase switch
    {
        1 => "Revalorización de aportes",
        2 => "Distribución de excedentes",
        3 => "Amortización de aportes",
        _ => clase.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string Estado(int estado) => estado switch
    {
        1 => "Borrador",
        2 => "Aprobada",
        3 => "Aplicada",
        4 => "Descartada",
        5 => "Reversada",
        _ => estado.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string Base(int b) => b switch { 2 => "Saldo al 31 de diciembre", 3 => "Base subida por plantilla", _ => "Saldo promedio diario" };

    public static string Destino(int? d) => d switch
    {
        1 => "A aportes",
        2 => "A una cuenta de ahorro",
        3 => "Pago por orden de giro",
        _ => "—",
    };
}

public sealed partial class CaptacionesClient
{
    public const string RutaDeAsamblea = Base + "/assembly-distributions";

    public Task<ResultadoDeInventario<IReadOnlyList<DecisionDeAsambleaDto>>> DecisionesDeAsambleaAsync(int? anio = null, int? clase = null, int? estado = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<DecisionDeAsambleaDto>>(HttpMethod.Get, ConQuery(RutaDeAsamblea, Query(("year", Texto(anio)), ("kind", Texto(clase)),
            ("status", Texto(estado)))), null, null, ct);

    public Task<ResultadoDeInventario<DecisionDeAsambleaDto>> DecisionDeAsambleaAsync(Guid decision, CancellationToken ct = default) =>
        Enviar<DecisionDeAsambleaDto>(HttpMethod.Get, $"{RutaDeAsamblea}/{decision}", null, null, ct);

    public Task<ResultadoDeInventario<DecisionDeAsambleaDto>> RegistrarDecisionDeAsambleaAsync(RegistrarDecisionDeAsambleaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DecisionDeAsambleaDto>(HttpMethod.Post, RutaDeAsamblea, request, clave, ct);

    public Task<ResultadoDeInventario<SimulacionDeAsambleaDto>> SimularDecisionDeAsambleaAsync(Guid decision, CancellationToken ct = default) =>
        Enviar<SimulacionDeAsambleaDto>(HttpMethod.Post, $"{RutaDeAsamblea}/{decision}/simulate", null, null, ct);

    public Task<ResultadoDeInventario<DecisionDeAsambleaDto>> DescartarDecisionDeAsambleaAsync(Guid decision, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DecisionDeAsambleaDto>(HttpMethod.Post, $"{RutaDeAsamblea}/{decision}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<DecisionDeAsambleaDto>> AplicarDecisionDeAsambleaAsync(Guid decision, AplicarDecisionDeAsambleaRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<DecisionDeAsambleaDto>(HttpMethod.Post, $"{RutaDeAsamblea}/{decision}/apply", request, clave, ct);

    public Task<ResultadoDeInventario<DecisionDeAsambleaDto>> ReversarDecisionDeAsambleaAsync(Guid decision, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DecisionDeAsambleaDto>(HttpMethod.Post, $"{RutaDeAsamblea}/{decision}/reverse", new MotivoDeCarteraRequest(motivo), clave, ct);
}
