using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Vaults;

/// <summary>
/// <c>RCD_VaultMovements</c> (H) (feature 015, T0089; FR-035, FR-182; data-model §2.6): lo que entra y sale de la bóveda —abastecer una caja, recibir de una
/// caja, remesas, conteos, billete retenido con acta—, con dos custodios distintos cuando <c>Caja.DobleCustodia</c> (activo por defecto, D34). (nuevo)
/// </summary>
public class VaultMovement : AuditableEntity, IHechoInmutable
{
    public string Number { get; set; } = string.Empty;

    public int VaultId { get; set; }

    public VaultMovementKind Kind { get; set; }

    /// <summary>El movimiento de caja par (abastecer o recibir).</summary>
    public int? CashMovementId { get; set; }

    /// <summary>La remesa de Tesorería (<c>TES_CashRemittances.PublicId</c>).</summary>
    public Guid? RemittancePublicId { get; set; }

    /// <summary>Con signo respecto de la bóveda: positivo entra, negativo sale; el conteo lleva cero.</summary>
    public decimal Amount { get; set; }

    public int CustodianUserId { get; set; }

    public int? SecondCustodianUserId { get; set; }

    public string? BagSeal { get; set; }

    public Guid? MinutesAttachmentPublicId { get; set; }

    public string? Reason { get; set; }

    public DateTime MovedAt { get; set; }
}

/// <summary><c>RCD_VaultMovementDenominations</c> (H): las denominaciones de un movimiento de bóveda, con signo como el movimiento. (nuevo)</summary>
public class VaultMovementDenomination : AuditableEntity, IHechoInmutable
{
    public int VaultMovementId { get; set; }

    public int CashDenominationId { get; set; }

    public int Quantity { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// <c>RCD_VaultDenominationBalances</c> (feature 015, T0089; FR-036; data-model §2.6): la proyección mutable del saldo de la bóveda por denominación, que el
/// cuadre diario compara contra la suma de sus movimientos. (nuevo)
/// </summary>
public class VaultDenominationBalance : AuditableEntity
{
    public int VaultId { get; set; }

    public int CashDenominationId { get; set; }

    public int Quantity { get; set; }

    public decimal Amount { get; set; }
}
