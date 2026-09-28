using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core.Payments;

/// <summary>
/// Un adquirente (<c>COR_CardAcquirers</c>; feature 012, I3, T573; FR-096, FR-101; data-model §16): Redeban, Credibanco… Su
/// <see cref="PersonId"/> es el tercero de la cuenta por cobrar a la red (como <c>COR_Banks.PersonId</c> en la 009) y viaja en el
/// pago de tarjeta. Con medios o datáfonos no se borra (<c>Core.CardAcquirer.InUse</c>).
/// </summary>
public class CardAcquirer : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary><c>COR_People</c>: el tercero de la cuenta por cobrar al adquirente.</summary>
    public int? PersonId { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CardTerminal> Terminals { get; set; } = new List<CardTerminal>();
}
