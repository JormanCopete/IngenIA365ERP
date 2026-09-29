using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

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
    /// <param name="asignarNumeroFiscal">
    /// La vía exclusiva del caso b (<c>NumeradorFiscal.ReutilizarNumeroParaReemplazo</c>, que sólo invoca
    /// <c>ReplaceRejectedDocumentCommand</c>): la fuente la aplica al borrador antes de confirmarlo, así el reemplazo sale con el mismo
    /// número del rechazado sin consumir la resolución. (nuevo, T724)
    /// </param>
    Task<Result<ReemplazoConfirmado>> ConfirmarReemplazoAsync(Guid rejectedSourceDocumentPublicId, Guid replacementDocumentPublicId,
        Action<InventoryDocument> asignarNumeroFiscal, CancellationToken ct);

    /// <summary>
    /// Caso a (T722): la contraparte del documento tal como está <b>hoy</b> en el maestro de personas, en la forma de la copia fiscal y
    /// con la versión siguiente a la vigente. Nula si el documento no tiene persona (consumidor final): no hay qué corregir. No guarda.
    /// (nuevo)
    /// </summary>
    Task<Result<FotoFiscalDeEntrada?>> ContraparteDelMaestroAsync(Guid sourceDocumentPublicId, CancellationToken ct);

    /// <summary>
    /// Caso a (T722): agrega la versión <paramref name="foto"/> de la copia fiscal del documento, con su motivo (la única excepción de
    /// FR-005/SC-013; antes y después quedan en la versión electrónica y en la auditoría). La versión tiene que ser la siguiente a la
    /// vigente. Corre dentro de la transacción de quien llama y no guarda. (nuevo)
    /// </summary>
    Task<Result> RegistrarVersionDeContraparteAsync(Guid sourceDocumentPublicId, FotoFiscalDeEntrada foto, string motivo, CancellationToken ct);

    /// <summary>
    /// Casos b y c (T724, T725): el permiso de confirmar de la clase del documento, que se exige además de
    /// <c>ElectronicInvoicing.Documents.Correct</c> porque anular y reemplazar son operaciones del módulo dueño. (nuevo)
    /// </summary>
    string PermisoDeConfirmar(ElectronicDocumentKind tipo);

    // ------------------------------------------------------------------------------ eventos RADIAN (I5, T803–T805) --

    /// <summary>
    /// Al pedir la emisión de los eventos RADIAN de un documento del módulo (<c>EmitRadianEventCommand</c>): verifica en su base las reglas del
    /// módulo (alcance → 404; sólo a crédito; el 032 exige el 030 y una recepción confirmada; ya hecho o en emisión; fecha), anota quién lo
    /// pide y cuándo, deja un rechazado otra vez pendiente (el reintento) y devuelve la entrada neutral de cada evento. Corre dentro de la
    /// transacción de quien llama y no guarda. (nuevo)
    /// </summary>
    Task<Result<IReadOnlyList<EventoRadianPreparado>>> PrepararEventosRadianAsync(Guid sourceDocumentPublicId,
        IReadOnlyList<ElectronicDocumentKind> tipos, CancellationToken ct);

    /// <summary>La entrada neutral de un evento ya pedido, tal como la entregó <see cref="PrepararEventosRadianAsync"/> (para volver a armarlo). (nuevo)</summary>
    Task<Result<EntradaDeEventoRadian>> LeerEventoRadianAsync(Guid sourceDocumentPublicId, ElectronicDocumentKind tipo, CancellationToken ct);

    /// <summary>Enlaza el evento del módulo con su documento electrónico (<c>ElectronicDocumentPublicId</c>). No guarda. (nuevo)</summary>
    Task<Result> EnlazarEventoRadianAsync(Guid sourceDocumentPublicId, ElectronicDocumentKind tipo, Guid electronicDocumentPublicId, CancellationToken ct);

    /// <summary>
    /// La respuesta definitiva de la DIAN al documento electrónico del evento (T805): validado → el evento queda emitido (con su CUDE, la
    /// fecha y la fuente «ERP»); rechazado → rechazado, y se reintenta pidiéndolo otra vez. Sin ninguno pendiente, el módulo atiende sola su
    /// alerta de eventos faltantes. Guarda. (nuevo)
    /// </summary>
    Task<Result> RegistrarResultadoDeEventoRadianAsync(Guid sourceDocumentPublicId, ElectronicDocumentKind tipo, ResultadoDeEventoRadian resultado,
        CancellationToken ct);
}

/// <summary>
/// Un evento RADIAN listo para emitir (I5, T804): su tipo, la entrada neutral, el documento electrónico que ya tenía si es un reintento de
/// uno rechazado (conserva su número) y el del 030 que el 032 debe esperar si todavía no está validado. (nuevo)
/// </summary>
public sealed record EventoRadianPreparado(
    ElectronicDocumentKind Tipo,
    EntradaDeEventoRadian Entrada,
    Guid? ElectronicDocumentPublicId,
    Guid? EsperaAlDocumentoPublicId);

/// <summary>La respuesta definitiva de la DIAN al evento (T805): validado o rechazado, con el CUDE y la fecha del evento. (nuevo)</summary>
public sealed record ResultadoDeEventoRadian(bool Validado, string? Cude, DateOnly Fecha);

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
