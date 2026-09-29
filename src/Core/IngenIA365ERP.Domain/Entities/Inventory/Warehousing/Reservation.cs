using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Warehousing;

/// <summary>
/// La cantidad comprometida por un pedido vigente (<c>INV_Reservations</c>; feature 012, I6, T855; FR-052, US14-1; data-model §3.6
/// y §14). <c>INV_StockBalances.Reserved</c> es su proyección: Σ (<see cref="QuantityBase"/> − <see cref="ConsumedQuantityBase"/>) de
/// las <see cref="ReservationStatus.Active"/>, escrita en la misma transacción bajo el cerrojo. <see cref="ExpiresOn"/> copia
/// <c>INV_Documents.ValidUntil</c>; la vence <c>ProgramadorDeTareas</c>. Es <b>estado</b>, no hecho: no implementa
/// <c>IHechoInmutable</c> y su historia es el diff de auditoría.
/// </summary>
public class Reservation : AuditableEntity
{
    public const int LargoDelMotivo = 200;

    /// <summary>El pedido (<c>SalesOrder</c>).</summary>
    public int DocumentId { get; set; }

    public int DocumentLineId { get; set; }

    public int ProductId { get; set; }

    public int WarehouseId { get; set; }

    /// <summary>Reservado en unidad base.</summary>
    public decimal QuantityBase { get; set; }

    /// <summary>Lo que ya salió por factura o remisión desde el pedido.</summary>
    public decimal ConsumedQuantityBase { get; set; }

    public DateOnly ExpiresOn { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Active;

    public DateTime? ReleasedAt { get; set; }

    /// <summary>La anulación del pedido, o la factura que consumió la última parte.</summary>
    public int? ReleasedByDocumentId { get; set; }

    public string? ReleaseReason { get; set; }
}
