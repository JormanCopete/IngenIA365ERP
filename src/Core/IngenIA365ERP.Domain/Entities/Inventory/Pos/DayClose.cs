using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// El cierre del día de un punto de venta (<c>INV_DayCloses</c>; feature 012, I3, T574; FR-099, T50; data-model §15). Consolida
/// las sesiones del día por medio, adquirente y datáfono; no emite mensajes. Exige todas las sesiones de esa fecha cerradas
/// (<c>Inventory.DayClose.SessionsOpen</c>) y después no se abren sesiones con esa fecha en el punto. Se reabre con permiso y
/// motivo (F12): la fila queda <c>Reopened</c> con sus líneas como evidencia y el siguiente cierre es otra fila con
/// <see cref="Version"/> + 1. Único cerrado por <c>(PointOfSaleId, OperatingDate)</c>.
/// </summary>
public class DayClose : AuditableEntity
{
    public int PointOfSaleId { get; init; }

    public PointOfSale? PointOfSale { get; set; }

    public DateOnly OperatingDate { get; init; }

    public short Version { get; init; } = 1;

    public DayCloseStatus Status { get; private set; } = DayCloseStatus.Closed;

    public DateTime ClosedAt { get; init; }

    public int ClosedByUserId { get; init; }

    public short SessionCount { get; set; }

    public decimal TotalExpected { get; set; }

    public decimal TotalCounted { get; set; }

    public decimal TotalDifference { get; set; }

    public DateTime? ReopenedAt { get; private set; }

    public int? ReopenedByUserId { get; private set; }

    public string? ReopenReason { get; private set; }

    public ICollection<DayCloseLine> Lines { get; set; } = new List<DayCloseLine>();

    /// <summary>Reabre el día con su motivo. El cierre siguiente es otra fila con la versión siguiente.</summary>
    public void Reabrir(int reabiertoPor, DateTime ahoraUtc, string motivo)
    {
        if (Status != DayCloseStatus.Closed)
            throw new InvalidOperationException($"El cierre del día {Id} ya está reabierto.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Reabrir el día exige motivo.", nameof(motivo));
        Status = DayCloseStatus.Reopened;
        ReopenedAt = ahoraUtc;
        ReopenedByUserId = reabiertoPor;
        ReopenReason = motivo.Trim();
    }
}
