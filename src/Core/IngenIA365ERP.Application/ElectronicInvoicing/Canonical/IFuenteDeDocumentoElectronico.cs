using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Canonical;

/// <summary>
/// El puerto por el que un módulo fuente le entrega a la plataforma sus documentos comerciales y hace, en su propia base, lo que
/// los casos b y c de un rechazo le piden (feature 012, I4, T701; contracts/dian.md §4.1 y §8.3; api.md §24.5; T40). La plataforma
/// nunca lee ni escribe tablas del módulo: Inventario lo implementa con <c>FuenteDeEmisionDeInventario</c> (<c>SourceModule =
/// "INV"</c>); mañana Cartera o servicios con la suya. Varias fuentes conviven en el contenedor y se eligen por
/// <see cref="SourceModule"/>, el que quedó sellado en <c>COR_ElectronicDocuments.SourceModule</c>. (nuevo)
/// </summary>
public interface IFuenteDeDocumentoElectronico
{
    /// <summary>El módulo fuente («INV»).</summary>
    string SourceModule { get; }

    /// <summary>La entrada neutral del documento comercial <b>ya confirmado</b>.</summary>
    Task<Result<EntradaDeDocumentoElectronico>> LeerAsync(Guid sourceDocumentPublicId, CancellationToken ct);

    /// <summary>
    /// Preparación del caso b (<c>POST /documents/{id}/replacement-draft</c>): un borrador de la <b>misma clase</b> que el
    /// rechazado, precargado con sus líneas y su contraparte, vinculado con <c>ReplacementOf</c> y sin número. Si ya hay un
    /// borrador de reemplazo vivo → <c>ElectronicInvoicing.Document.ReplacementDraftExists</c> (<c>data.replacementDraftPublicId</c>).
    /// (nuevo)
    /// </summary>
    Task<Result<BorradorDeReemplazo>> CrearBorradorDeReemplazoAsync(Guid rejectedSourceDocumentPublicId, CancellationToken ct);

    /// <summary>
    /// Casos b y c: anula el documento comercial rechazado con un documento contrario <b>sin efecto fiscal</b> (devuelve las
    /// existencias y emite sus mensajes de anulación) y libera su número fiscal (<c>FiscalNumberReleased</c>). Corre dentro de la
    /// transacción de quien llama. (nuevo)
    /// </summary>
    Task<Result<AnulacionSinEfectoFiscal>> AnularSinEfectoFiscalAsync(Guid rejectedSourceDocumentPublicId, CasoFiscalDeAnulacion caso, string motivo, CancellationToken ct);

    /// <summary>
    /// Caso b: confirma el borrador de reemplazo (misma clase, vinculado con <c>ReplacementOf</c> al rechazado) por el flujo
    /// canónico de su módulo. El número fiscal que toma lo decide la numeración de la plataforma (la vía exclusiva del
    /// reemplazo); la fuente no numera. Corre dentro de la transacción de quien llama. (nuevo)
    /// </summary>
    Task<Result<ReemplazoConfirmado>> ConfirmarReemplazoAsync(Guid rejectedSourceDocumentPublicId, Guid replacementDocumentPublicId, CancellationToken ct);
}

/// <summary>Por qué se anula sin efecto fiscal: caso b (se reemplaza) o caso c (se cancela). (nuevo)</summary>
public enum CasoFiscalDeAnulacion
{
    /// <summary>Caso b: <c>fiscalCase = DianRejectionReplaced</c>.</summary>
    DianRejectionReplaced = 1,

    /// <summary>Caso c: <c>fiscalCase = DianRejectionCancelled</c>.</summary>
    DianRejectionCancelled = 2,
}

/// <summary>El borrador de reemplazo creado: dónde se edita (<c>editRoute</c>, la ruta de su grupo). (nuevo)</summary>
public sealed record BorradorDeReemplazo(Guid ReplacementDraftPublicId, string SourceModule, string EditRoute);

/// <summary>El documento contrario que anuló el rechazado sin efecto fiscal. (nuevo)</summary>
public sealed record AnulacionSinEfectoFiscal(Guid VoidingDocumentPublicId, string? VoidingDocumentNumber);

/// <summary>El reemplazo confirmado: su <c>PublicId</c> (el nuevo <c>SourceDocumentPublicId</c>) y su número visible. (nuevo)</summary>
public sealed record ReemplazoConfirmado(Guid ReplacementDocumentPublicId, string? DisplayNumber);
