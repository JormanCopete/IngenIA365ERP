using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Impuestos (feature 009 E4, US10) sobre <c>/api/accounting/taxes</c>: formularios tributarios y certificados de retención. Los informes
/// (retenciones, IVA, ICA, GMF y el resumen de cada formulario) son vistas de <see cref="InformeAsync"/>.
/// </summary>
public sealed partial class ContabilidadClient
{
    private const string BaseImpuestos = "/api/accounting/taxes";

    public Task<InvitationApiResult<IReadOnlyList<FormularioTributarioDto>>> ListarFormulariosTributariosAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<FormularioTributarioDto>>(HttpMethod.Get, $"{BaseImpuestos}/forms", null, ct);

    public Task<InvitationApiResult<FormularioTributarioDto>> GuardarFormularioTributarioAsync(string codigo, string nombre, bool activo, IReadOnlyList<RenglonDeFormularioDto> renglones, CancellationToken ct = default) =>
        EnviarAsync<FormularioTributarioDto>(HttpMethod.Put, $"{BaseImpuestos}/forms/{Uri.EscapeDataString(codigo)}",
            new { Name = nombre, IsActive = activo, Lines = renglones.Select(r => new { r.LineCode, r.Description, r.Selector, r.AccountPrefixes, r.Sign }).ToList() }, ct);

    public Task<InvitationApiResult<IReadOnlyList<FormularioTributarioDto>>> CargarFormulariosSugeridosAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<FormularioTributarioDto>>(HttpMethod.Post, $"{BaseImpuestos}/forms/load-suggested", null, ct);

    /// <summary>El plano de formularios lleno con lo configurado (sin formularios, la plantilla vacía).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> PlanoDeFormulariosAsync(CancellationToken ct = default) =>
        DescargarAsync($"{BaseImpuestos}/forms/plano.xlsx", ct);

    public Task<ResultadoContable<ImportacionDeFormulariosDto>> ImportarFormulariosAsync(string archivo, byte[] contenido, CancellationToken ct = default) =>
        SubirAsync<ImportacionDeFormulariosDto>($"{BaseImpuestos}/forms/import", archivo, contenido, null, ct);

    public Task<InvitationApiResult<IReadOnlyList<CertificadoDto>>> ListarCertificadosAsync(string? clase = null, int? anio = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(clase)) q.Add($"kind={Uri.EscapeDataString(clase)}");
        if (anio is { } a) q.Add($"year={a}");
        return EnviarAsync<IReadOnlyList<CertificadoDto>>(HttpMethod.Get, q.Count == 0 ? $"{BaseImpuestos}/certificates" : $"{BaseImpuestos}/certificates?{string.Join('&', q)}", null, ct);
    }

    public Task<InvitationApiResult<GeneracionDeCertificadosDto>> GenerarCertificadosAsync(string clase, int anio, byte? desde, byte? hasta, CancellationToken ct = default) =>
        EnviarAsync<GeneracionDeCertificadosDto>(HttpMethod.Post, $"{BaseImpuestos}/certificates/generate",
            new { Kind = clase, Year = anio, PeriodFrom = desde, PeriodTo = hasta }, ct);

    public Task<InvitationApiResult<CertificadoDto>> ReexpedirCertificadoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<CertificadoDto>(HttpMethod.Post, $"{BaseImpuestos}/certificates/{id}/reissue", null, ct);

    public Task<InvitationApiResult<CertificadoDto>> EnviarCertificadoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<CertificadoDto>(HttpMethod.Post, $"{BaseImpuestos}/certificates/{id}/send", null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarCertificadoAsync(Guid id, CancellationToken ct = default) =>
        DescargarAsync($"{BaseImpuestos}/certificates/{id}/pdf", ct);
}

public sealed record RenglonDeFormularioDto(string LineCode, string Description, string Selector, string AccountPrefixes, short Sign, int Order);

public sealed record FormularioTributarioDto(Guid PublicId, string Code, string Name, bool IsActive, IReadOnlyList<RenglonDeFormularioDto> Lines);

public sealed record CertificadoDto(
    Guid PublicId, string TaxKind, long Number, int Year, byte? PeriodFrom, byte? PeriodTo, Guid PersonPublicId, string PersonName, string PersonTaxId,
    string? PersonEmail, DateTime IssuedAt, string IssuedBy, string Status, decimal TotalBase, decimal TotalAmount, DateTime? LastSentAt, string? LastSentTo,
    Guid? VoidedByPublicId)
{
    public string ClaseTexto => TaxKind switch
    {
        "VatWithholding" => "Retención de IVA",
        "IcaWithholding" => "Retención de ICA",
        _ => "Retención en la fuente",
    };

    public string EstadoTexto => Status switch
    {
        "Current" => "Vigente",
        "Outdated" => "Desactualizado",
        "Voided" => "Anulado",
        _ => Status,
    };

    public string PeriodoTexto => PeriodFrom is { } d && PeriodTo is { } h ? $"{Year} ({d:D2}–{h:D2})" : Year.ToString();
}

public sealed record ImportacionDeFormulariosDto(int Forms, int Lines);

public sealed record GeneracionDeCertificadosDto(int Issued, int AlreadyCurrent, int Outdated, IReadOnlyList<CertificadoDto> Certificates);
