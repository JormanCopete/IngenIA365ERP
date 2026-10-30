using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>Información exógena (feature 009 E4, US11) sobre <c>/api/accounting/exogenous</c>.</summary>
public sealed partial class ContabilidadClient
{
    private const string BaseExogena = "/api/accounting/exogenous";

    public Task<InvitationApiResult<IReadOnlyList<FormatoDeExogenaDto>>> ListarFormatosDeExogenaAsync(int anio, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<FormatoDeExogenaDto>>(HttpMethod.Get, $"{BaseExogena}/formats?year={anio}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<FormatoDeExogenaDto>>> CargarSemillaDeExogenaAsync(int anio, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<FormatoDeExogenaDto>>(HttpMethod.Post, $"{BaseExogena}/formats/{anio}/load-seed", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<FormatoDeExogenaDto>>> CopiarExogenaDelAnioAsync(int anio, int desde, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<FormatoDeExogenaDto>>(HttpMethod.Post, $"{BaseExogena}/formats/{anio}/copy-from/{desde}", null, ct);

    public Task<InvitationApiResult<FormatoDeExogenaDto>> GuardarFormatoDeExogenaAsync(int anio, FormatoDeExogenaDto f, CancellationToken ct = default) =>
        EnviarAsync<FormatoDeExogenaDto>(HttpMethod.Put, $"{BaseExogena}/formats/{anio}/{Uri.EscapeDataString(f.FormatCode)}",
            new { f.FormatVersion, f.Name, f.Applies, f.MinAmount, f.MinorAmountsRule, f.MinorAmountsTaxId, f.Layout, f.Concepts }, ct);

    public Task<InvitationApiResult<IReadOnlyList<GeneracionDeExogenaDto>>> ListarGeneracionesDeExogenaAsync(int anio, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<GeneracionDeExogenaDto>>(HttpMethod.Get, $"{BaseExogena}/runs?year={anio}", null, ct);

    public Task<InvitationApiResult<GeneracionDeExogenaDto>> GenerarExogenaAsync(int anio, string formato, CancellationToken ct = default) =>
        EnviarAsync<GeneracionDeExogenaDto>(HttpMethod.Post, $"{BaseExogena}/runs/{anio}/{Uri.EscapeDataString(formato)}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<InconsistenciaDeExogenaDto>>> InconsistenciasDeExogenaAsync(Guid generacion, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<InconsistenciaDeExogenaDto>>(HttpMethod.Get, $"{BaseExogena}/runs/{generacion}/issues", null, ct);

    public Task<InvitationApiResult<TablaReporteDto>> TablaDeExogenaAsync(Guid generacion, CancellationToken ct = default) =>
        EnviarAsync<TablaReporteDto>(HttpMethod.Get, $"{BaseExogena}/runs/{generacion}/table", null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarExcelDeExogenaAsync(Guid generacion, CancellationToken ct = default) =>
        DescargarAsync($"{BaseExogena}/runs/{generacion}/table?format=xlsx", ct);

    /// <summary>El XML del prevalidador; la primera vez deja la generación exportada y su archivo guardado.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> ExportarXmlDeExogenaAsync(Guid generacion, CancellationToken ct = default) =>
        DescargarAsync(HttpMethod.Post, $"{BaseExogena}/runs/{generacion}/xml", ct);
}

public sealed record ColumnaDeExogenaDto(string Field, string Label, string Xml);

public sealed record DisposicionDeExogenaDto(string Element, string? ConceptAttribute, IReadOnlyList<string> Identity, IReadOnlyList<ColumnaDeExogenaDto> Columns);

public sealed record CuentaDeConceptoDto(string ValueField, string AccountPrefix, string Selector);

public sealed record ConceptoDeExogenaDto(string ConceptCode, string Name, IReadOnlyList<CuentaDeConceptoDto> Accounts);

public sealed record FormatoDeExogenaDto(
    Guid PublicId, int TaxYear, string FormatCode, int FormatVersion, string Name, bool Applies, string Origin, decimal MinAmount, bool MinorAmountsRule,
    string MinorAmountsTaxId, DisposicionDeExogenaDto? Layout, IReadOnlyList<ConceptoDeExogenaDto> Concepts);

public sealed record GeneracionDeExogenaDto(
    Guid PublicId, int TaxYear, string FormatCode, string FormatName, int Version, string Status, DateTime GeneratedAt, string GeneratedBy,
    DateTime? ExportedAt, string? ExportedBy, Guid? XmlAttachmentPublicId, int Lines, int IssueCount, int BlockingIssueCount)
{
    public bool Exportada => Status == "Exported";
}

public sealed record InconsistenciaDeExogenaDto(Guid? PersonPublicId, string IdNumber, string? Name, string ConceptCode, string Code, string Message, bool Blocking);
