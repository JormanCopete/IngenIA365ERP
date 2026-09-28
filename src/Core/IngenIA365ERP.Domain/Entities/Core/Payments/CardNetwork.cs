using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Core.Payments;

/// <summary>
/// Una franquicia o red de tarjetas (<c>COR_CardNetworks</c>; feature 012, I3, T573; FR-096; data-model §16): Visa, Mastercard,
/// American Express, las débito de cada red. <see cref="Code"/> único entre vivos; con medios que la usan no se borra
/// (<c>Core.CardNetwork.InUse</c>).
/// </summary>
public class CardNetwork : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CardKind CardKind { get; set; } = CardKind.Both;

    public bool IsActive { get; set; } = true;
}
