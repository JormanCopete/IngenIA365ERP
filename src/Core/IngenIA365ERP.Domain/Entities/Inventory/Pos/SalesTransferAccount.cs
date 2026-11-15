using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// <c>INV_TransferAccounts</c> (feature 022, T040; FR-046, Q16): una cuenta bancaria propia de recaudo habilitada para recibir un medio de
/// transferencia o consignación en un punto de venta (y, si se da, sólo en una de sus cajas), como <c>RCD_TransferAccounts</c> en Recaudo. El
/// cajero elige entre las habilitadas para el medio en su caja; con una sola, se propone. La cuenta es de Tesorería: aquí sólo va su
/// <c>PublicId</c> y el rótulo copiado al habilitarla (la caja no lee cuentas de Tesorería). Se retira con baja lógica, no se edita.
/// </summary>
public class SalesTransferAccount : AuditableEntity
{
    public int PointOfSaleId { get; set; }

    /// <summary>Nula: todas las cajas del punto.</summary>
    public int? CashRegisterId { get; set; }

    public int PaymentMeansId { get; set; }

    public Guid OwnBankAccountPublicId { get; set; }

    /// <summary>Código, banco y número enmascarado de la cuenta, copiados de Tesorería al habilitarla.</summary>
    public string AccountLabel { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
