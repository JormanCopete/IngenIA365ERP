using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Los errores de la emisión, el estado, los artefactos y la entrega de un documento electrónico (feature 012, I4, T711–T721; api.md §24.4,
/// §24.8). Los de la máquina de estados (<c>.Final</c>, <c>.AwaitingResponse</c>, <c>.NotRejected</c>…) son de
/// <see cref="TransicionesDelDocumentoElectronico"/>. (nuevo)
/// </summary>
public static class ErroresDeDocumentosElectronicos
{
    public const string NotFoundCode = "ElectronicInvoicing.Document.NotFound";
    public const string InProgressCode = "ElectronicInvoicing.Document.InProgress";
    public const string ChannelNotLinkedCode = "ElectronicInvoicing.Document.ChannelNotLinked";
    public const string NotDeliverableCode = "ElectronicInvoicing.Document.NotDeliverable";

    /// <summary>El canónico reconstruido no coincide con el SHA-256 de la versión: no se emite (contracts/dian.md §4.1). (nuevo)</summary>
    public const string CanonicalMismatchCode = "ElectronicInvoicing.Document.CanonicalMismatch";

    /// <summary>Se pidió numerar en contingencia 03 y no hay evento 03 abierto para el canal sellado. (nuevo)</summary>
    public const string ContingencyNotOpenCode = "ElectronicInvoicing.Contingency.NotOpen";

    /// <summary>El artefacto pedido no existe (todavía) para esa versión o transmisión. (nuevo)</summary>
    public const string ArtifactNotFoundCode = "ElectronicInvoicing.Document.ArtifactNotFound";

    /// <summary>Ese documento no admite la operación pedida en su estado (p. ej. consultar uno en contingencia). (nuevo)</summary>
    public static Error NoAdmite(ElectronicDocumentStatus estado, string que) =>
        new ErrorConDatos(TransicionesDelDocumentoElectronico.CodigoTransicionNoPermitida,
            $"El documento está {estado}: no admite {que}.", new { status = estado });

    public static Error NotFound() => new(NotFoundCode, "El documento electrónico no existe.");

    /// <summary>422: otro proceso (el procesador, el intento en línea del POS u otra persona) lo está trabajando.</summary>
    public static Error InProgress(DateTime leaseUntil) =>
        new ErrorConDatos(InProgressCode,
            "Otro proceso está transmitiendo este documento en este momento. Espere un momento y consulte su estado.",
            new { leaseUntil });

    public static Error ChannelNotLinked(string motivo) => new(ChannelNotLinkedCode, motivo);

    public static Error NotDeliverable(ElectronicDocumentStatus estado) =>
        new ErrorConDatos(NotDeliverableCode,
            "El documento todavía no se puede entregar al comprador: se entrega validado por la DIAN o expedido en contingencia.",
            new { status = estado });

    public static Error CanonicalMismatch(string numero) =>
        new(CanonicalMismatchCode,
            $"El documento {numero} no se transmitió: al reconstruirlo no coincide con lo que se registró al confirmarlo. Soporte debe revisarlo.");

    public static Error ContingencyNotOpen(string canal) =>
        new(ContingencyNotOpenCode, $"No hay una contingencia de facturación abierta en el canal {canal}: el documento no se numera en contingencia.");

    public static Error ArtifactNotFound() => new(ArtifactNotFoundCode, "Ese archivo del documento electrónico no existe.");
}
