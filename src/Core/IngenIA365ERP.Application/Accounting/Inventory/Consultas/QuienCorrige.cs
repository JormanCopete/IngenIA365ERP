using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Accounting.Inventory.Consultas;

/// <summary>
/// Quién corrige cada hallazgo de la validación previa, de la vista previa de un lote o de un rechazo (feature 012, T515;
/// contracts/contabilidad.md §4.3): el módulo, la pantalla y el permiso. Es la tabla de §4.3, una fila por código; dos códigos
/// dependen del caso —la tarifa distinta de la cuenta (C8) frente a la de la regla, y el tercero que falta según la clase del
/// documento lleve o no tercero—. (nuevo)
/// </summary>
public static class QuienCorrige
{
    public const string Contabilidad = "Contabilidad";
    public const string Inventario = "Inventario";
    public const string Core = "Core";

    private static readonly QuienCorrigeDto Matriz = new(Contabilidad, "/contabilidad/inventario/matriz", "Accounting.InventoryRules.Manage");
    private static readonly QuienCorrigeDto TiposDeComprobante = new(Contabilidad, "/contabilidad/inventario/tipos-de-comprobante", "Accounting.InventoryRules.Manage");
    private static readonly QuienCorrigeDto PlanDeCuentas = new(Contabilidad, "/contabilidad/plan-de-cuentas", "Accounting.Accounts.Manage");
    private static readonly QuienCorrigeDto Soporte = new(Inventario, null, null);

    /// <param name="codigo">El código del hallazgo.</param>
    /// <param name="clase">La clase del documento que se evaluó (decide quién pone el tercero y el centro).</param>
    /// <param name="tarifaDeLaCuenta">En <c>TaxRateMismatch</c>, si la tarifa distinta es la vigente de la cuenta y no la de la regla.</param>
    public static QuienCorrigeDto De(string codigo, DocumentClass? clase = null, bool tarifaDeLaCuenta = false) => codigo switch
    {
        "Accounting.InventoryRule.Missing" => Matriz,
        "Accounting.InventoryRule.TaxRateMismatch" => tarifaDeLaCuenta
            ? new QuienCorrigeDto(Contabilidad, "/contabilidad/plan-de-cuentas", "Accounting.Taxes.Manage")
            : Matriz,
        "Accounting.VoucherType.NotFound" or "Accounting.VoucherType.NotAllowedForModule" or "Accounting.VoucherType.Inactive" => TiposDeComprobante,
        "Accounting.Line.AccountNotMovement" or "Accounting.Line.AccountInactive" or "Accounting.Line.AccountNotEnabledForModule"
            or "Accounting.Line.AccountNotFound" or "Accounting.Line.CostCenterNotAllowed" or "Accounting.Line.TaxBaseRequired"
            or "Accounting.Line.TaxAmountMismatch" or "Accounting.Line.TaxAmountDiffers" or "Accounting.Line.TaxRateMissing"
            or "Accounting.Line.BranchRequired" => PlanDeCuentas,
        "Accounting.Line.ThirdPartyRequired" => ClaseLlevaTercero(clase) ? ElDocumento(clase) : Matriz,
        "Accounting.Line.CostCenterRequired" => ElDocumento(clase),
        "Accounting.Line.CrossDocumentRequired" => Matriz,
        "Accounting.Line.ThirdPartyInvalid" => new QuienCorrigeDto(Core, "/maestros/personas", "Core.People.Update"),
        "Accounting.Line.CrossDocumentTypeInvalid" => TiposDeComprobante,
        "Accounting.Period.Closed" => new QuienCorrigeDto(Contabilidad, "/contabilidad/periodos", "Accounting.Periods.Reopen"),
        "Accounting.Period.NotFound" => new QuienCorrigeDto(Contabilidad, "/contabilidad/periodos", "Accounting.Periods.CloseYear"),
        "Accounting.NotInitialized" => new QuienCorrigeDto(Contabilidad, "/contabilidad/configuracion", "Accounting.Setup.Manage"),
        // Defectos del emisor (contenido que no cuadra, más de dos decimales, moneda): los atiende soporte.
        "Accounting.InventoryMessage.Unbalanced" or "Accounting.InventoryMessage.CurrencyNotSupported" or "Accounting.Line.AmountInvalid"
            or "Accounting.Document.Unbalanced" or "Accounting.Document.TooFewLines" => Soporte,
        _ => new QuienCorrigeDto(Contabilidad, null, null),
    };

    /// <summary>¿La clase del documento lleva tercero? Compras y ventas (el proveedor o el cliente) y el arqueo (el cajero).</summary>
    public static bool ClaseLlevaTercero(DocumentClass? clase) => clase is not null && PermisoDeLaClase(clase) is "Inventory.Purchases.Create" or "Inventory.Sales.Create"
        || clase == DocumentClass.CashCountDifference;

    private static QuienCorrigeDto ElDocumento(DocumentClass? clase) => new(Inventario, null, PermisoDeLaClase(clase));

    /// <summary>El permiso de crear del grupo de la clase (p. ej. <c>Inventory.Sales.Create</c>).</summary>
    public static string? PermisoDeLaClase(DocumentClass? clase) => clase switch
    {
        DocumentClass.PurchaseRequest or DocumentClass.PurchaseOrder or DocumentClass.PurchaseReceipt or DocumentClass.SupplierInvoice
            or DocumentClass.SupplierNote or DocumentClass.SupportDocument or DocumentClass.SupportDocumentAdjustmentNote
            or DocumentClass.LandedCost or DocumentClass.SupplierReturn => "Inventory.Purchases.Create",
        DocumentClass.SalesQuote or DocumentClass.SalesOrder or DocumentClass.Shipment or DocumentClass.SalesInvoice
            or DocumentClass.SalesInvoiceFromShipments or DocumentClass.PosEquivalentDocument or DocumentClass.NonElectronicSalesReceipt
            or DocumentClass.NonElectronicSalesNote or DocumentClass.CreditNote or DocumentClass.PosAdjustmentNote
            or DocumentClass.DebitNote => "Inventory.Sales.Create",
        DocumentClass.TransferDispatch or DocumentClass.TransferReceipt => "Inventory.Transfers.Create",
        DocumentClass.CashMovement or DocumentClass.CashCountDifference => "Inventory.CashMovements.Create",
        null => null,
        _ => "Inventory.Adjustments.Create",
    };
}
