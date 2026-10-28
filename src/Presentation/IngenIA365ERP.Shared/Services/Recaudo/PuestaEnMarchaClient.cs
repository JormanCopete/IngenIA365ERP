using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>Una línea del cuadre de apertura (concepto, dimensión, cargado, contable —nulo = sin dato—, diferencia). (nuevo)</summary>
public sealed record LineaDeCuadreDto(string Concept, string Dimension, string? DimensionLabel, decimal Loaded, decimal? Ledger, decimal Difference, bool IsComparable,
    bool Squares);

/// <summary>Un cuadre registrado, definitivo o provisional. (nuevo)</summary>
public sealed record CuadreDeAperturaDto(Guid PublicId, string Kind, string Status, DateOnly CutoffDate, decimal TotalLoaded, decimal TotalLedger, decimal TotalDifference,
    DateTime RegisteredAt, string? ApprovedBy, DateTime? ApprovedAt, string? Reason, DateOnly? DeadlineOn, Guid? MinutesAttachmentPublicId, Guid? DefinitiveSquarePublicId,
    IReadOnlyList<LineaDeCuadreDto> Lines);

/// <summary>La propuesta del cuadre a la fecha de corte, el vigente y el provisional pendiente. (nuevo)</summary>
public sealed record CuadreDeAperturaActualDto(DateOnly CutoffDate, IReadOnlyList<LineaDeCuadreDto> Proposal, decimal TotalLoaded, decimal TotalLedger,
    decimal TotalDifference, bool Squares, CuadreDeAperturaDto? Current, CuadreDeAperturaDto? PendingProvisional, Guid? MinutesOwnerPublicId = null);

/// <summary>Una cifra de referencia contra lo cargado y lo contable. (nuevo)</summary>
public sealed record CifraDeReferenciaCotejadaDto(DateOnly CutoffDate, string Concept, string Dimension, string? DimensionLabel, decimal Reference, decimal? Loaded,
    decimal? Ledger, decimal DifferenceLoadedVsReference);

public sealed record CuadreProvisionalRequest(string Reason, DateOnly DeadlineOn, DateOnly? CutoffDate);

public sealed record CuadreDefinitivoRequest(DateOnly? CutoffDate, Guid? MinutesAttachmentPublicId, string? Reason);

/// <summary>
/// Cliente tipado de la puesta en marcha de Recaudo y de Tesorería (feature 015, US13, T0290; api.md §14): el cuadre de apertura (propuesta, provisional y definitivo)
/// y las cifras de referencia. <c>modulo</c> es <c>receipts</c> o <c>treasury</c>; las plantillas van por <c>ImportarPlantilla</c> sobre
/// <see cref="RutaDePlantilla"/>. (nuevo)
/// </summary>
public sealed class PuestaEnMarchaClient(HttpClient http, CentralAuthClient auth)
{
    public const string Recaudo = "receipts";
    public const string Tesoreria = "treasury";

    /// <summary>La ruta base de una plantilla, para <c>ImportarPlantilla</c> (<c>…/template.xlsx</c>, <c>…/import</c>).</summary>
    public static string RutaDePlantilla(string modulo, string codigo) => $"/api/{modulo}/imports/{codigo}";

    public Task<ResultadoDeInventario<CuadreDeAperturaActualDto>> CuadreAsync(string modulo, DateOnly? corte, CancellationToken ct = default) =>
        Enviar<CuadreDeAperturaActualDto>(HttpMethod.Get, corte is { } c ? $"/api/{modulo}/opening-squares/current?cutoffDate={c:yyyy-MM-dd}" : $"/api/{modulo}/opening-squares/current",
            null, null, ct);

    public Task<ResultadoDeInventario<CuadreDeAperturaDto>> ProvisionalAsync(string modulo, CuadreProvisionalRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CuadreDeAperturaDto>(HttpMethod.Post, $"/api/{modulo}/opening-squares/provisional", r, clave, ct);

    public Task<ResultadoDeInventario<CuadreDeAperturaDto>> DefinitivoAsync(string modulo, CuadreDefinitivoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CuadreDeAperturaDto>(HttpMethod.Post, $"/api/{modulo}/opening-squares/definitive", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CifraDeReferenciaCotejadaDto>>> CifrasAsync(string modulo, DateOnly? corte, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CifraDeReferenciaCotejadaDto>>(HttpMethod.Get,
            corte is { } c ? $"/api/{modulo}/reference-figures?cutoffDate={c:yyyy-MM-dd}" : $"/api/{modulo}/reference-figures", null, null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}
