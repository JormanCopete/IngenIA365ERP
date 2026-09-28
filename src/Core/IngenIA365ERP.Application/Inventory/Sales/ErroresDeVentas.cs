using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Los errores de las ventas y notas de oficina (feature 012, I3, T608–T612; contracts/api.md §18.5, §22.5). Un solo catálogo: el
/// mismo código, mensaje y <c>data</c> en el borrador (como <c>issue</c>) y en la confirmación. <c>Inventory.Sales.SalespersonInvalid</c>
/// vive en <c>ErroresDelPos</c> (lo comparte el POS). (nuevo)
/// </summary>
public static class ErroresDeVentas
{
    public const string FiscalClassMismatchCode = "Inventory.Sales.FiscalClassMismatch";
    public const string PersonInactiveCode = "Inventory.Sales.PersonInactive";
    public const string NotReadyCode = "ElectronicInvoicing.NotReady";
    public const string CreditNoteClassMismatchCode = "Inventory.CreditNote.ClassMismatch";
    public const string ExceedsRemainingCode = "Inventory.CreditNote.ExceedsRemaining";
    public const string CreditNoteOriginInvalidCode = "Inventory.CreditNote.OriginInvalid";
    public const string CorrectionConceptRequiredCode = "Inventory.CreditNote.CorrectionConceptRequired";
    public const string RefundMeansNotAllowedCode = "Payments.RefundMeansNotAllowed";
    public const string FiscalUseCorrectionCode = "Inventory.Document.FiscalUseCorrection";

    /// <summary>La ruta de las notas de venta (§18.3): la corrección de un fiscal emitido.</summary>
    public const string RutaDeNotas = "/api/inventory/sales/credit-notes";

    /// <summary>El tipo pide una clase distinta de la que permite el veredicto de <see cref="GuardiaDeEmisionFiscal"/> (§18.2).</summary>
    public static Error FiscalClassMismatch(DocumentClass clase, EvaluacionFiscal evaluacion) => new ErrorConDatos(FiscalClassMismatchCode,
        evaluacion.Veredicto == VeredictoFiscal.NonElectronic
            ? $"La cooperativa no está obligada a facturar electrónicamente: use un tipo de comprobante no electrónico, no uno de clase {clase}."
            : $"El tipo de documento es de clase {clase}, que no corresponde a la forma de emisión de la cooperativa.",
        new { verdict = evaluacion.Veredicto.ToString(), allowedClasses = evaluacion.ClasesAdmitidas.Select(c => c.ToString()).ToList() });

    /// <summary>La guardia fiscal bloqueó: ninguna venta fiscal confirma hasta completar lo que falta (§24.3).</summary>
    public static Error NotReady(EvaluacionFiscal evaluacion) => new ErrorConDatos(NotReadyCode,
        "La cooperativa todavía no puede emitir documentos de venta: " + string.Join(" ", evaluacion.Motivos.Select(m => m.Message)),
        new { missing = evaluacion.Motivos.Select(m => new { code = m.Code, message = m.Message, where = m.Where, permission = m.Permission }).ToList() });

    /// <summary>Venta de contado a una persona inactiva con <c>Ventas.PersonaInactivaDeContado = Bloquear</c> (§18.2).</summary>
    public static Error PersonInactive(string nombre) => new ErrorConDatos(PersonInactiveCode,
        $"{nombre} está inactiva: la política de la cooperativa no permite venderle de contado.", new { name = nombre });

    /// <summary>La nota no es de la clase que corresponde a su original (§18.3).</summary>
    public static Error CreditNoteClassMismatch(DocumentClass originClass, DocumentClass expectedClass) => new ErrorConDatos(CreditNoteClassMismatchCode,
        $"La nota de un documento {originClass} es de clase {expectedClass}: elija un tipo de esa clase.",
        new { originClass = originClass.ToString(), expectedClass = expectedClass.ToString() });

    /// <summary>El original no admite nota: no es una venta confirmada de las clases de §18.3. (nuevo)</summary>
    public static Error CreditNoteOriginInvalid() => new(CreditNoteOriginInvalidCode,
        "Sólo se hace nota sobre una factura, un documento equivalente POS o un comprobante de venta confirmado.");

    /// <summary>La nota electrónica exige el concepto de corrección del catálogo DIAN (§18.3). (nuevo)</summary>
    public static Error CorrectionConceptRequired() => new(CorrectionConceptRequiredCode,
        "Indique el concepto de corrección de la DIAN: la nota electrónica lo exige.");

    /// <summary>Lo que queda por acreditar de una línea del original (<c>data.lines[]</c>).</summary>
    public sealed record Restante(Guid OriginLinePublicId, decimal RemainingQuantity, decimal RemainingAmount);

    /// <summary>Alguna línea acredita más de lo que queda (original − notas confirmadas o en aprobación) (§18.3).</summary>
    public static Error ExceedsRemaining(IReadOnlyList<Restante> lineas) => new ErrorConDatos(ExceedsRemainingCode,
        "La nota acredita más de lo que queda por acreditar en el documento original.", new { lines = lineas });

    /// <summary>Reintegro por un medio distinto de los de la venta sin <c>Inventory.Sales.RefundOtherMeans</c> (§18.3, 422 y no 404).</summary>
    public static Error RefundMeansNotAllowed(string meansCode) => new ErrorConDatos(RefundMeansNotAllowedCode,
        $"El reintegro va por el mismo medio de la venta; {meansCode} no fue un medio de esa venta y reintegrar por otro medio exige permiso.",
        new { paymentMeansCode = meansCode });

    /// <summary>Un fiscal emitido no se anula con documento contrario: se corrige con su nota (FR-066, §18.2).</summary>
    public static Error FiscalUseCorrection(DocumentClass originClass)
    {
        var correccion = originClass == DocumentClass.PosEquivalentDocument ? DocumentClass.PosAdjustmentNote : DocumentClass.CreditNote;
        return new ErrorConDatos(FiscalUseCorrectionCode,
            "Un documento fiscal emitido no se anula con un documento contrario: se corrige con su nota de anulación total.",
            new { correctionClass = correccion.ToString(), route = RutaDeNotas, totalVoid = true });
    }
}
