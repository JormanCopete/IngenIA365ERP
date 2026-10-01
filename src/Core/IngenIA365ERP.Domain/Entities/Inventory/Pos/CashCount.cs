using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// El arqueo de una sesión (<c>INV_CashCounts</c>; feature 012, I3, T574; FR-099, T50; data-model §15 «Arqueo»): uno por sesión,
/// con una línea por medio usado o con saldo esperado. Se edita mientras su documento de diferencia está en borrador (reconteo
/// tras un rechazo, auditado); después es inmutable. Por eso lleva <see cref="Status"/> <b>(nuevo)</b>, la copia del estado de su
/// documento <c>CashCountDifference</c> —o <c>Confirmed</c> desde el cierre si nada difiere—, que es lo que mira la guarda de
/// <see cref="IInmutableTrasConfirmar"/>; sus líneas llevan la misma copia (<see cref="Fijar"/>).
/// </summary>
public class CashCount : AuditableEntity, IInmutableTrasConfirmar
{
    public int CashSessionId { get; set; }

    public CashSession? CashSession { get; set; }

    public DateTime CountedAt { get; set; }

    public int CountedByUserId { get; set; }

    /// <summary>Copia de la sesión.</summary>
    public bool IsBlind { get; set; }

    public decimal TotalExpected { get; set; }

    public decimal TotalCounted { get; set; }

    public decimal TotalDifference { get; set; }

    /// <summary>El <c>CashCountDifference</c>, si alguna línea difiere.</summary>
    public int? DifferenceDocumentId { get; set; }

    /// <summary>Copia del estado del documento de diferencia (<c>Draft</c> mientras se puede recontar). (nuevo)</summary>
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public ICollection<CashCountLine> Lines { get; set; } = new List<CashCountLine>();

    /// <summary>Fija el arqueo y sus líneas: su documento de diferencia se confirmó, o no hubo diferencia.</summary>
    public void Fijar()
    {
        Status = DocumentStatus.Confirmed;
        foreach (var linea in Lines) linea.Fijar();
    }
}
