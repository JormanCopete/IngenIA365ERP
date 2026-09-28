using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core.Payments;

/// <summary>
/// Un datáfono de cobro de un adquirente (<c>COR_CardTerminals</c>; feature 012, I3, T573; FR-101; data-model §16). En pantalla
/// «Datáfonos de cobro» (no es <c>DEB_PosTerminals</c>). <see cref="Code"/> es el TER del voucher, único por adquirente entre
/// vivos. El datáfono por defecto de una caja vive en <c>INV_CashRegisters.DefaultCardTerminalId</c>, para que Core no apunte al
/// módulo.
/// </summary>
public class CardTerminal : AuditableEntity
{
    public int CardAcquirerId { get; set; }

    public CardAcquirer? CardAcquirer { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Serial { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
