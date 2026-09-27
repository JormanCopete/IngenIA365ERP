using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Pos;

/// <summary>
/// Los errores y avisos de la venta en el POS y de la entrega (feature 012, I3, T603–T607; contracts/api.md §20.2–§20.4).
/// <c>Inventory.Sales.SalespersonInvalid</c>, <c>Inventory.Product.NotFound</c> (del lector) e
/// <c>Inventory.Pos.DraftNotFound</c> son (nuevo). Una venta fuera del alcance de quien pregunta es el mismo 404 que la
/// inexistente. (nuevo)
/// </summary>
public static class ErroresDelPos
{
    public const string CashSessionNotOpenCode = "Inventory.CashSession.NotOpen";
    public const string NotEnabledCode = "Inventory.Pos.NotEnabled";
    public const string InvoiceRequiresCustomerCode = "Inventory.Pos.InvoiceRequiresCustomer";
    public const string NotSuspendedCode = "Inventory.Pos.NotSuspended";
    public const string RepricedCode = "Inventory.Pos.Repriced";
    public const string DraftNotFoundCode = "Inventory.Pos.DraftNotFound";
    public const string LineNotFoundCode = "Inventory.Pos.LineNotFound";
    public const string RoleNotConfiguredCode = "Inventory.Pos.RoleNotConfigured";
    public const string ProductNotFoundCode = "Inventory.Product.NotFound";
    public const string SalespersonInvalidCode = "Inventory.Sales.SalespersonInvalid";
    public const string TotalChangedCode = "Inventory.Document.TotalChanged";
    public const string AlreadyDeliveredCode = "Inventory.Document.AlreadyDelivered";
    public const string NotDeliverableCode = "ElectronicInvoicing.Document.NotDeliverable";
    public const string CustomerNotFoundCode = "Inventory.Pos.CustomerNotFound";
    public const string EmailRequiredCode = "Inventory.Document.EmailRequired";

    /// <summary>La sesión no está abierta o no es del usuario (422, §20.2).</summary>
    public static Error CashSessionNotOpen() => new(CashSessionNotOpenCode,
        "La sesión de caja no está abierta o no es suya: abra su turno en una caja del punto antes de vender.");

    /// <summary>El punto de la sesión no tiene POS (<c>INV_PointsOfSale.PosEnabled = false</c>, FR-058). (nuevo)</summary>
    public static Error NotEnabled(string pointOfSaleCode) => new ErrorConDatos(NotEnabledCode,
        $"El punto {pointOfSaleCode} no vende por el POS: sus cajas se usan para el cobro de las ventas de oficina.",
        new { pointOfSaleCode });

    /// <summary>La factura a petición del comprador exige un cliente identificado (422, §20.2).</summary>
    public static Error InvoiceRequiresCustomer() => new(InvoiceRequiresCustomerCode,
        "La factura a petición del comprador necesita un cliente identificado: elija el cliente antes de cambiar el documento.");

    public static Error NotSuspended() => new(NotSuspendedCode, "La venta no está suspendida: sólo se recupera una venta suspendida.");

    /// <summary>La venta no existe, no es del POS, no está en borrador de este punto o está fuera del alcance (404).</summary>
    public static Error DraftNotFound() => new(DraftNotFoundCode, "La venta no existe.");

    public static Error LineNotFound() => new(LineNotFoundCode, "La línea no existe en esta venta.");

    /// <summary>La caja no tiene tipo para el rol pedido (422). (nuevo)</summary>
    public static Error RoleNotConfigured(string cashRegisterCode, CashRegisterDocumentRole role) => new ErrorConDatos(RoleNotConfiguredCode,
        $"La caja {cashRegisterCode} no tiene tipo de documento para el rol {role}: configúrelo en Ventas › Puntos de venta.",
        new { cashRegisterCode, role });

    /// <summary>Ninguna coincidencia exacta por código de barras ni por código de producto (404, §20.2).</summary>
    public static Error ProductNotFound(string code) => new ErrorConDatos(ProductNotFoundCode,
        $"No hay un producto con el código «{code}». Búsquelo por nombre.", new { code });

    /// <summary>El vendedor no es una persona con rol vivo en <c>INV_Salespeople</c> (FR-057). (nuevo)</summary>
    public static Error SalespersonInvalid() => new(SalespersonInvalidCode,
        "El vendedor no existe o ya no está activo: elija un vendedor vigente.");

    /// <summary>El cliente no existe (404).</summary>
    public static Error CustomerNotFound() => new(CustomerNotFoundCode, "El cliente no existe.");

    /// <summary><c>expectedAmountDue</c> no es lo que la venta dice ahora (422, §20.4).</summary>
    public static Error TotalChanged(decimal expectedAmountDue, decimal amountDue) => new ErrorConDatos(TotalChangedCode,
        $"El valor a pagar cambió a {amountDue:N2}: revise la venta y vuelva a cobrar.", new { expectedAmountDue, amountDue });

    /// <summary>Ya se hizo la primera entrega en ese formato: se usa la reimpresión (422, §20.3).</summary>
    public static Error AlreadyDelivered(string format) => new ErrorConDatos(AlreadyDeliveredCode,
        "Esta venta ya se entregó en ese formato: use la reimpresión, que sale marcada como COPIA.", new { format });

    /// <summary>El documento todavía no es entregable (422, §20.3).</summary>
    public static Error NotDeliverable(DocumentStatus status) => new ErrorConDatos(NotDeliverableCode,
        "El documento todavía no se puede entregar.", new { status });

    /// <summary>Se pidió el correo y ni la copia fiscal ni el cuerpo traen una dirección. (nuevo)</summary>
    public static Error EmailRequired() => new(EmailRequiredCode, "El comprador no tiene correo: escriba la dirección a la que se envía.");

    /// <summary>El aviso de recuperar una venta en otra fecha operativa (no es error).</summary>
    public const string MensajeRepriced = "La venta se recuperó en otra fecha operativa: los precios se volvieron a calcular.";
}
