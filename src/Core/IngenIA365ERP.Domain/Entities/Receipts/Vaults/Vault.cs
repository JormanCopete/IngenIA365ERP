using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Vaults;

/// <summary>
/// <c>RCD_Vaults</c> (feature 015, T0070; US1; FR-035, FR-180; data-model §2.6): la bóveda de una oficina (una por oficina), propia
/// (<see cref="VaultMode.Vault"/>) o la caja principal que hace de bóveda (<see cref="VaultMode.MainCashBox"/>, con <see cref="MainCashBoxId"/>). Abastece y
/// recibe de las cajas y despacha las remesas; su saldo por denominación es una proyección que el cuadre diario verifica. (nuevo)
/// </summary>
public class Vault : AuditableEntity
{
    /// <summary>La oficina (<c>COR_Branches.Id</c>), única.</summary>
    public int BranchId { get; set; }

    public VaultMode Mode { get; set; } = VaultMode.Vault;

    /// <summary>La caja principal cuando la bóveda es ella (FR-180).</summary>
    public int? MainCashBoxId { get; set; }

    public ReceiptsCatalogStatus Status { get; set; } = ReceiptsCatalogStatus.Active;
}

/// <summary>
/// <c>RCD_VaultCustodians</c> (feature 015, T0070; FR-035, FR-182; data-model §2.6): un custodio de la bóveda con vigencia. Con <c>Caja.DobleCustodia</c> los
/// movimientos de bóveda llevan dos custodios vigentes distintos. (nuevo)
/// </summary>
public class VaultCustodian : AuditableEntity
{
    public int VaultId { get; set; }

    /// <summary><c>SEC_Users.Id</c>.</summary>
    public int UserId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool VigenteEn(DateOnly fecha) => VigenciaDeRecaudo.Incluye(ValidFrom, ValidTo, fecha);
}
