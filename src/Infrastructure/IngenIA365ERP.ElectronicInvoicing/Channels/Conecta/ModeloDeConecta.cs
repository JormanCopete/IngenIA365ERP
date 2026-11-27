using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.ElectronicInvoicing.Channels.Conecta;

// Feature 028 (contracts/canal-conecta.md §4–§8): la copia mínima del contrato público de IngenIA Conecta que usa el adaptador, con los
// nombres de su OpenAPI (Conecta365 web/src/api/openapi.json, leído el 2026-11-27). No se referencia el repositorio de Conecta: si su
// contrato cambia, cambia aquí y lo cazan los casos dorados del traductor.

/// <summary>Las opciones JSON del adaptador: camelCase, enumeraciones por nombre, nulos omitidos al escribir.</summary>
internal static class JsonDeConecta
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}

/// <summary><c>PaymentMeansRequest</c>: forma (<c>code</c>: 1 contado, 2 crédito), medio (<c>method</c>) y vencimiento.</summary>
public sealed record PagoConecta(string Code, string Method, DateOnly? DueDate);

/// <summary><c>AddressRequest</c>.</summary>
public sealed record DireccionConecta(
    string MunicipalityCode,
    string City,
    string DepartmentCode,
    string Department,
    string Line,
    string? PostalCode = null,
    string Country = "CO");

/// <summary><c>CustomerRequest</c>.</summary>
public sealed record AdquirenteConecta(
    string IdType,
    string IdNumber,
    string? Dv,
    string LegalName,
    string? TradeName,
    IReadOnlyList<string>? TaxLevelCodes,
    string Regime,
    string? Email,
    DireccionConecta Address,
    string? OrganizationType = null,
    string? Phone = null,
    string? GivenNames = null,
    string? FamilyNames = null,
    string? TaxScheme = null);

/// <summary><c>TaxRequest</c>: la tarifa en <b>porcentaje</b> (19), no en fracción.</summary>
public sealed record TributoConecta(string Code, decimal Rate, bool Exempt = false, bool Excluded = false, string? LegalNote = null);

/// <summary><c>AllowanceRequest</c>.</summary>
public sealed record DescuentoConecta(bool IsCharge, string Reason, decimal? Percent = null, decimal? Amount = null);

/// <summary><c>LineRequest</c>.</summary>
public sealed record LineaConecta(
    string Code,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    IReadOnlyList<TributoConecta>? Taxes,
    IReadOnlyList<DescuentoConecta>? Allowances);

/// <summary><c>InvoiceRequest</c> (<c>POST /v1/invoices</c>).</summary>
public sealed record FacturaConecta(
    Guid TenantId,
    string Prefix,
    long? Number,
    DateOnly? IssueDate,
    string OperationType,
    string InvoiceType,
    PagoConecta PaymentMeans,
    AdquirenteConecta Customer,
    IReadOnlyList<LineaConecta> Lines,
    IReadOnlyList<string>? Notes,
    IReadOnlyList<DescuentoConecta>? Allowances,
    string? OrderReference,
    IReadOnlyDictionary<string, string>? Metadata);

/// <summary><c>DiscrepancyRequest</c>.</summary>
public sealed record DiscrepanciaConecta(string Code, string Description);

/// <summary><c>ReferencedInvoiceRequest</c>.</summary>
public sealed record FacturaReferidaConecta(string Id, string Cufe, DateOnly IssueDate);

/// <summary><c>BillingPeriodRequest</c>.</summary>
public sealed record PeriodoConecta(DateOnly StartDate, DateOnly EndDate);

/// <summary><c>NoteRequest</c> (<c>POST /v1/credit-notes</c>, <c>/v1/debit-notes</c>).</summary>
public sealed record NotaConecta(
    Guid TenantId,
    string Prefix,
    long? Number,
    DateOnly? IssueDate,
    string OperationType,
    DiscrepanciaConecta Discrepancy,
    FacturaReferidaConecta? ReferencedInvoice,
    PeriodoConecta? Period,
    PagoConecta PaymentMeans,
    AdquirenteConecta Customer,
    IReadOnlyList<LineaConecta> Lines,
    IReadOnlyList<string>? Notes,
    IReadOnlyList<DescuentoConecta>? Allowances,
    string? OrderReference,
    IReadOnlyDictionary<string, string>? Metadata);

/// <summary><c>InvoiceTotalsResult</c>.</summary>
public sealed record TotalesConecta(
    decimal LineExtensionAmount,
    decimal TaxExclusiveAmount,
    decimal TaxInclusiveAmount,
    decimal AllowanceTotalAmount,
    decimal ChargeTotalAmount,
    decimal PayableAmount);

/// <summary><c>IssueInvoiceResult</c> e <c>IssueNoteResult</c> (la nota trae <c>cude</c> en lugar de <c>cufe</c>).</summary>
public sealed record EmisionConecta(
    Guid Id,
    string DocumentNumber,
    string? Cufe,
    string? Cude,
    string? QrUrl,
    string Status,
    string Environment,
    TotalesConecta? Totals,
    bool Replayed);

/// <summary><c>DocumentSummary</c>.</summary>
public sealed record DocumentoConecta(
    Guid Id,
    string Family,
    string? SubtypeCode,
    string DocumentNumber,
    string? IssueDate,
    string? UniqueCode,
    string? UniqueCodeAlgorithm,
    string Status,
    string? DianTrackId,
    string? DianStatusCode,
    string? DianStatusMessage,
    string Environment,
    TotalesConecta? Totals,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ProviderCode = null,
    string? ProviderDocumentId = null,
    string? ProviderStatusCode = null);

/// <summary><c>TimelineEntry</c>.</summary>
public sealed record EntradaDeLineaDeTiempoConecta(DateTimeOffset At, string Kind, string? FromStatus, string? ToStatus, string? Description);

/// <summary><c>DocumentDetail</c> (<c>GET /v1/documents/{id}</c>).</summary>
public sealed record DetalleConecta(DocumentoConecta Document, IReadOnlyList<EntradaDeLineaDeTiempoConecta>? Timeline);

/// <summary><c>DocumentPage</c> (<c>GET /v1/documents</c>).</summary>
public sealed record PaginaConecta(IReadOnlyList<DocumentoConecta> Items, string? NextCursor);

/// <summary><c>EmissionChannelSummary</c>.</summary>
public sealed record CanalDelTenantConecta(Guid Id, string Environment, string Kind, string? ProviderCode, string? ProviderName, bool HasCredentials);

/// <summary><c>ProviderNumberingSummary</c>.</summary>
public sealed record EnlaceDeNumeracionConecta(string Family, string Prefix, string ProviderCode);

/// <summary><c>EmissionChannelOverview</c> (<c>GET /v1/tenants/{id}/emission-channel</c>).</summary>
public sealed record PanoramaDelCanalConecta(
    CanalDelTenantConecta? Current,
    IReadOnlyList<EnlaceDeNumeracionConecta>? NumberingLinks,
    IReadOnlyList<string>? Missing);

/// <summary><c>TenantDetail</c> (<c>GET /v1/tenants/{id}</c>), lo que el ERP lee.</summary>
public sealed record TenantConecta(Guid Id, string Nit, string? Dv, string LegalName, string Environment, IReadOnlyList<string>? MissingToIssue);

/// <summary>La respuesta del token (OAuth 2.0).</summary>
public sealed record TokenConecta(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

/// <summary>
/// Un Problem Details (RFC 9457) de Conecta con sus extensiones: <c>dianCode</c>, <c>ruleIds</c>, <c>errors</c>, <c>provider</c>,
/// <c>prefix</c>, <c>traceId</c>.
/// </summary>
public sealed record ProblemaConecta(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? DianCode,
    string? DianMessage,
    string? RuleId,
    IReadOnlyList<string>? RuleIds,
    IReadOnlyList<string>? Errors,
    string? Provider,
    string? Prefix,
    string? TraceId)
{
    /// <summary>Lee el cuerpo como Problem Details; nulo si no lo es.</summary>
    public static ProblemaConecta? Leer(string? cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo)) return null;
        try
        {
            return JsonSerializer.Deserialize<ProblemaConecta>(cuerpo, JsonDeConecta.Opciones);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>El texto para la pantalla: el detalle, o el título.</summary>
    public string Texto => !string.IsNullOrWhiteSpace(Detail) ? Detail! : Title ?? "Conecta respondió un error sin detalle.";
}
