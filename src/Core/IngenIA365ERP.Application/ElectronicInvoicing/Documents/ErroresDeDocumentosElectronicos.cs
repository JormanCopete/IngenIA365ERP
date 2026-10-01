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

    // ------------------------------------------------------------------------------------ casos a, b y c (T722–T725) --

    /// <summary>Caso a con la huella económica distinta: se sigue por el caso b (contracts/dian.md §8.2; api.md §24.5).</summary>
    public const string EconomicFootprintChangedCode = "ElectronicInvoicing.Document.EconomicFootprintChanged";

    /// <summary>
    /// 422, no 404 (api.md §1.2, §2.9): reemplazar o cancelar exige además el permiso de confirmar de la clase del documento, que depende del
    /// documento y no de la ruta. (nuevo, T724)
    /// </summary>
    public const string ClassPermissionRequiredCode = "ElectronicInvoicing.Document.ClassPermissionRequired";

    public static Error EconomicFootprintChanged(IReadOnlyList<string> campos) =>
        new ErrorConDatos(EconomicFootprintChangedCode,
            $"La corrección cambia lo económico del documento ({string.Join(", ", campos)}): no se corrige, se reemplaza. " +
            "Cree el borrador de reemplazo, ajústelo y confírmelo con el mismo número.",
            new { fields = campos });

    public static Error ClassPermissionRequired(string permiso) =>
        new ErrorConDatos(ClassPermissionRequiredCode,
            $"Reemplazar o cancelar este documento anula el documento del ERP: exige también el permiso {permiso}.",
            new { permissionCode = permiso });

    /// <summary>El veredicto de la máquina de estados que no procede, como error de la aplicación.</summary>
    public static Error DeLaTransicion(ResultadoDeTransicion transicion) =>
        new ErrorConDatos(transicion.Codigo!, Mensaje(transicion.Codigo!, transicion.Motivo), new { status = transicion.Hacia });

    private static string Mensaje(string codigo, string? motivo) => codigo switch
    {
        TransicionesDelDocumentoElectronico.CodigoEsperaRespuesta =>
            "El documento se envió y la DIAN todavía no responde: consulte su estado antes de corregirlo, reemplazarlo o cancelarlo.",
        TransicionesDelDocumentoElectronico.CodigoNoRechazado =>
            "Sólo un documento rechazado se corrige, se reemplaza o se cancela.",
        TransicionesDelDocumentoElectronico.CodigoRechazoSinConfirmar =>
            "Reemplazar o cancelar exige el rechazo confirmado: use «Consultar a la DIAN» y vuelva a intentarlo.",
        _ => motivo ?? "El documento no admite esa operación en su estado.",
    };
}
