using System.Text.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de los archivos regulatorios de Cartera (feature 013, K5, US16, T1009; contracts/api.md §18): los layouts, validar (consulta por POST,
/// sin clave) y generar (con clave) el formato de cartera del SICSES y el archivo de cada central de riesgo, la lista y el detalle de las generaciones,
/// el enlace firmado de 60 s (sin clave: repetirlo emite otro; auditado) y marcar cargado con el radicado (con clave). Una generación con bloqueantes
/// responde 422 <see cref="CodigoBloqueantes"/> con <c>data { regulatoryFilePublicId, version, issues[] }</c>: la versión <c>Validated</c> sin archivo
/// queda guardada. Sin <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed class RegulatorioDeCarteraClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = GobiernoDeCarteraClient.Base + "/regulatory";
    public const string CodigoBloqueantes = "Lending.Regulatory.BlockingIssues";
    public const string CodigoSinCotejar = "Lending.Regulatory.LayoutSinCotejar";

    /// <summary>Clase del archivo (<c>RegulatoryFileKind</c>): el SICSES o una central.</summary>
    public const int ClaseSicses = 1, ClaseCentral = 2;

    public Task<ResultadoDeInventario<IReadOnlyList<LayoutRegulatorioDto>>> LayoutsAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<LayoutRegulatorioDto>>(HttpMethod.Get, Ruta + "/layouts", null, null, ct);

    /// <summary>Valida el formato del SICSES del período sin guardar nada (consulta por POST, sin clave).</summary>
    public Task<ResultadoDeInventario<ValidacionRegulatoriaDto>> ValidarSicsesAsync(int anio, int mes, CancellationToken ct = default) =>
        Enviar<ValidacionRegulatoriaDto>(HttpMethod.Post, $"{Ruta}/sicses/{anio}/{mes}/validate", null, null, ct);

    /// <summary>Genera el formato del SICSES; la versión anterior queda reemplazada.</summary>
    public Task<ResultadoDeInventario<ArchivoRegulatorioDetalleDto>> GenerarSicsesAsync(int anio, int mes, bool reconocerAdvertencias, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ArchivoRegulatorioDetalleDto>(HttpMethod.Post, $"{Ruta}/sicses/{anio}/{mes}/generate", new GenerarArchivoRegulatorioRequest(reconocerAdvertencias),
            clave, ct);

    /// <summary>Valida el archivo de una central para el período, con sus excluidos (consulta por POST, sin clave).</summary>
    public Task<ResultadoDeInventario<ValidacionRegulatoriaDto>> ValidarCentralAsync(string central, int anio, int mes, CancellationToken ct = default) =>
        Enviar<ValidacionRegulatoriaDto>(HttpMethod.Post, $"{Ruta}/bureaus/{Uri.EscapeDataString(central)}/{anio}/{mes}/validate", null, null, ct);

    /// <summary>Genera el archivo de una central.</summary>
    public Task<ResultadoDeInventario<ArchivoRegulatorioDetalleDto>> GenerarCentralAsync(string central, int anio, int mes, bool reconocerAdvertencias,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ArchivoRegulatorioDetalleDto>(HttpMethod.Post, $"{Ruta}/bureaus/{Uri.EscapeDataString(central)}/{anio}/{mes}/generate",
            new GenerarArchivoRegulatorioRequest(reconocerAdvertencias), clave, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ArchivoRegulatorioDto>>> ArchivosAsync(int? clase = null, string? central = null, int? anio = null,
        int? mes = null, int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ArchivoRegulatorioDto>>(HttpMethod.Get, ConQuery(Ruta + "/files", Query(("kind", Texto(clase)), ("bureau", central),
            ("year", Texto(anio)), ("month", Texto(mes)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<ArchivoRegulatorioDetalleDto>> ArchivoAsync(Guid archivo, CancellationToken ct = default) =>
        Enviar<ArchivoRegulatorioDetalleDto>(HttpMethod.Get, $"{Ruta}/files/{archivo}", null, null, ct);

    /// <summary>Un enlace firmado de 60 s al archivo (auditado). Va por POST y sin clave.</summary>
    public Task<ResultadoDeInventario<EnlaceDeCarteraDto>> EnlaceAsync(Guid archivo, CancellationToken ct = default) =>
        Enviar<EnlaceDeCarteraDto>(HttpMethod.Post, $"{Ruta}/files/{archivo}/download-link", null, null, ct);

    /// <summary>Marca cargado en el portal con el radicado: el período ya no se regenera.</summary>
    public Task<ResultadoDeInventario<ArchivoRegulatorioDto>> MarcarCargadoAsync(Guid archivo, MarcarCargadoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ArchivoRegulatorioDto>(HttpMethod.Post, $"{Ruta}/files/{archivo}/mark-uploaded", request, clave, ct);

    /// <summary>Las inconsistencias de una generación con bloqueantes (el <c>data.issues</c> del 422), o vacía si el error es otro.</summary>
    public static IReadOnlyList<IncidenciaRegulatoriaDto> IncidenciasDelRechazo<T>(ResultadoDeInventario<T> r) =>
        r.ErrorCode == CodigoBloqueantes ? r.Dato<List<IncidenciaRegulatoriaDto>>("issues") ?? [] : [];

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}

// ---------------------------------------------------------------------------------------------------------------------------------- DTOs --

public sealed record LayoutRegulatorioDto(
    string Code,
    int Kind,
    string? BureauCode,
    string Version,
    string Source,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string FileKind,
    int TotalFields,
    int VerifiedFields,
    int UnverifiedFields,
    string? VerifiedBy,
    DateOnly? VerifiedOn,
    string? VerifiedAgainst,
    IReadOnlyList<string> Notes);

/// <summary>Una inconsistencia: severidad (<c>Blocking</c> o <c>Warning</c>), código, registro, campo, crédito, ruta, fila y mensaje.</summary>
public sealed record IncidenciaRegulatoriaDto(
    string Severity,
    string Code,
    string? Record,
    int? Field,
    string? FieldName,
    string? Loan,
    string? Path,
    int? Row,
    string Message);

public sealed record AdvertenciaRegulatoriaDto(string Code, string Message);

public sealed record ExcluidoDeCentralDto(string Loan, Guid LoanPublicId, Guid PersonPublicId, bool IsDebtor, string Reason, string SuggestedAction);

public sealed record ValidacionRegulatoriaDto(
    string Layout,
    int Rows,
    int Blocking,
    IReadOnlyList<IncidenciaRegulatoriaDto> Issues,
    IReadOnlyList<AdvertenciaRegulatoriaDto> Warnings,
    IReadOnlyList<ExcluidoDeCentralDto> Excluded,
    DateOnly? DueDate);

public sealed record ConteosRegulatoriosDto(int Rows, int Blocking, int Warnings, int Excluded);

public sealed record CargueRegulatorioDto(string FilingNumber, DateOnly? FilingDate, DateTime? At, string? By);

public sealed record ArchivoRegulatorioDto(
    Guid PublicId,
    int Kind,
    string? BureauCode,
    int Year,
    int Month,
    int Version,
    int Status,
    string LayoutCode,
    IReadOnlyList<AdvertenciaRegulatoriaDto> Warnings,
    ConteosRegulatoriosDto Counts,
    string? FileName,
    string? Sha256,
    bool DownloadAvailable,
    string GeneratedBy,
    DateTime GeneratedAt,
    CargueRegulatorioDto? Uploaded);

public sealed record ArchivoRegulatorioDetalleDto(
    ArchivoRegulatorioDto File,
    IReadOnlyList<IncidenciaRegulatoriaDto> Issues,
    IReadOnlyList<ExcluidoDeCentralDto> Excluded,
    JsonElement Totals);

public sealed record GenerarArchivoRegulatorioRequest(bool AcknowledgeWarnings);

public sealed record MarcarCargadoRequest(string FilingNumber, DateTime UploadedAt, DateOnly? FilingDate, Guid? ReceiptAttachmentPublicId);

/// <summary>Los textos de los archivos regulatorios. (nuevo)</summary>
public static class TextosRegulatorios
{
    public static string Estado(int estado) => estado switch
    {
        1 => "Validado con inconsistencias (sin archivo)",
        2 => "Generado",
        3 => "Reemplazado",
        4 => "Cargado",
        _ => estado.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string Severidad(string severidad) => severidad == "Blocking" ? "Bloqueante" : "Advertencia";

    public static string Periodo(int anio, int mes) => $"{anio:0000}-{mes:00}";

    public static string Motivo(string motivo) => motivo switch
    {
        "NoReportAuthorization" => "Sin autorización de reporte",
        "NoPriorNoticeEvidence" => "Sin aviso previo con constancia",
        "DeceasedClaimInProcess" => "Reclamación al seguro de vida en trámite",
        "Insolvency" => "Insolvencia en negociación",
        "PaymasterArrearsWithinThreshold" => "Pagaduría en mora dentro del umbral",
        _ => motivo,
    };

    public static string Huella(string? sha256) => sha256 is { Length: > 16 } h ? $"{h[..16]}…" : sha256 ?? "—";
}
