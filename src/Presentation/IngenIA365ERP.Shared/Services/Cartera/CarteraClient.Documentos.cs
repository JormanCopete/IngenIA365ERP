using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los documentos al deudor (feature 013, US13, T0791; contracts/api.md §15): la lista y el detalle (<c>Lending.Documents.Read</c>), emitir en un paso y
/// enviar por correo (<c>.Issue</c>, con clave), el enlace de 60 s del PDF guardado (<c>.Reprint</c>, por POST y sin clave: repetirlo emite otro),
/// anular con motivo (<c>.Void</c>, con clave) y verificar un código (<c>.Verify</c>). Además, el dueño y las imágenes vigentes del logotipo y de la
/// firma para su carga en los parámetros. Sin <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed partial class CarteraClient
{
    public const string RutaDeDocumentos = GobiernoDeCarteraClient.Base + "/documents";

    /// <summary>Los documentos del alcance por clase, persona, crédito, estado y fecha de emisión, los más recientes primero.</summary>
    public Task<ResultadoDeInventario<PaginaDeCarteraDto<DocumentoEmitidoDto>>> DocumentosAsync(string? clase = null, Guid? persona = null,
        Guid? credito = null, string? estado = null, DateOnly? desde = null, DateOnly? hasta = null, int pagina = 1, int tamano = 20,
        CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<DocumentoEmitidoDto>>(HttpMethod.Get, ConQuery(RutaDeDocumentos, Query(
            ("kind", clase), ("personPublicId", Texto(persona)), ("loanPublicId", Texto(credito)), ("status", estado), ("from", Texto(desde)),
            ("to", Texto(hasta)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<DetalleDeDocumentoDto>> DocumentoAsync(Guid documento, CancellationToken ct = default) =>
        Enviar<DetalleDeDocumentoDto>(HttpMethod.Get, $"{RutaDeDocumentos}/{documento}", null, null, ct);

    /// <summary>
    /// Emite en un paso. Se puede negar con <c>Lending.Document.ClearanceDenied</c> (<c>data.obligations[]</c>, <c>data.pendingSales[]</c>),
    /// <c>.KindNotAvailable</c>, <c>.SignerMissing</c>, <c>.NothingToCertify</c>, en K5 <c>.PeriodInvalid</c> (extracto) y <c>.TaxYearNotClosed</c>
    /// (certificado tributario) o, con correo, <c>Lending.DebtorMessage.ChannelNotAuthorized</c>.
    /// </summary>
    public Task<ResultadoDeInventario<DocumentoEmitidoRespuestaDto>> EmitirDocumentoAsync(EmitirDocumentoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DocumentoEmitidoRespuestaDto>(HttpMethod.Post, RutaDeDocumentos, request, clave, ct);

    /// <summary>El enlace firmado de 60 s del PDF guardado (idéntico al emitido; auditado como reimpresión). Por POST y sin clave.</summary>
    public Task<ResultadoDeInventario<EnlaceDeCarteraDto>> EnlaceDeDocumentoAsync(Guid documento, CancellationToken ct = default) =>
        Enviar<EnlaceDeCarteraDto>(HttpMethod.Post, $"{RutaDeDocumentos}/{documento}/download-link", null, null, ct);

    public Task<ResultadoDeInventario<DocumentoEnviadoDto>> EnviarDocumentoAsync(Guid documento, Guid? destinatario, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DocumentoEnviadoDto>(HttpMethod.Post, $"{RutaDeDocumentos}/{documento}/send", new EnviarDocumentoRequest(destinatario), clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> AnularDocumentoAsync(Guid documento, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeDocumentos}/{documento}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>
    /// Verifica un código (acepta minúsculas, guiones y espacios); un código de otra cooperativa o inexistente es <c>Lending.Document.NotFound</c>.
    /// Con <paramref name="sha256"/> (K5) compara además la huella de un PDF calculada en el navegador con la guardada (<c>pdfMatches</c>).
    /// </summary>
    public Task<ResultadoDeInventario<DocumentoVerificadoDto>> VerificarDocumentoAsync(string codigo, string? sha256 = null, CancellationToken ct = default) =>
        Enviar<DocumentoVerificadoDto>(HttpMethod.Get, ConQuery($"{RutaDeDocumentos}/verify/{Uri.EscapeDataString(codigo.Trim())}", Query(("sha256", sha256))),
            null, null, ct);

    /// <summary>El dueño de las imágenes de los documentos y las vigentes (<c>Lending.Parameters.View</c>).</summary>
    public Task<ResultadoDeInventario<ImagenesDeDocumentosDto>> ImagenesDeDocumentosAsync(CancellationToken ct = default) =>
        Enviar<ImagenesDeDocumentosDto>(HttpMethod.Get, $"{RutaDeDocumentos}/images", null, null, ct);
}
