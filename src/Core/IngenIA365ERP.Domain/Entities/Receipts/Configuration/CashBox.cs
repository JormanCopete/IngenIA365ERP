using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Configuration;

/// <summary>
/// <c>RCD_CashBoxes</c> (feature 015, T0070; US1; FR-021, FR-022, FR-028, FR-180, FR-181; data-model §1.3): una caja de un punto, con su base, su modo de base
/// (<see cref="CashBoxBaseMode.DailyBase"/> por defecto), su tope de aviso, su tope de bloqueo (nulo = no bloquea), el tope de egreso diario (R1) y si es la
/// caja principal de la oficina. Un cambio rige desde la <b>próxima</b> sesión: la abierta conserva lo que selló al abrir (US1-3). Código único por punto
/// entre vivas. (nuevo)
/// </summary>
public class CashBox : AuditableEntity
{
    public int CollectionPointId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CashBoxBaseMode BaseMode { get; set; } = CashBoxBaseMode.DailyBase;

    /// <summary>La base de la caja (Monto).</summary>
    public decimal BaseAmount { get; set; }

    /// <summary>El tope de efectivo que avisa trasladar a bóveda (Monto, opcional).</summary>
    public decimal? CashWarnCap { get; set; }

    /// <summary>El tope que bloquea los recibos en efectivo hasta trasladar (FR-181); nulo = no bloquea.</summary>
    public decimal? CashBlockCap { get; set; }

    /// <summary>El tope de egreso en efectivo por día (R1).</summary>
    public decimal? DailyCashOutCap { get; set; }

    /// <summary>La caja principal de la oficina (la que hace de bóveda en modo <see cref="VaultMode.MainCashBox"/>, FR-180).</summary>
    public bool IsMainCashBox { get; set; }

    public ReceiptsCatalogStatus Status { get; set; } = ReceiptsCatalogStatus.Active;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool VigenteEn(DateOnly fecha) => Status == ReceiptsCatalogStatus.Active && VigenciaDeRecaudo.Incluye(ValidFrom, ValidTo, fecha);
}

/// <summary>
/// <c>RCD_CashBoxCashiers</c> (feature 015, T0070; FR-022; data-model §1.4): un cajero asignado a una caja con su vigencia. El usuario debe tener persona
/// (<c>SEC_Users.PersonId</c>). Retirar cierra la vigencia; las vigencias del mismo usuario en la misma caja no se cruzan. (nuevo)
/// </summary>
public class CashBoxCashier : AuditableEntity
{
    public int CashBoxId { get; set; }

    /// <summary><c>SEC_Users.Id</c>.</summary>
    public int UserId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool VigenteEn(DateOnly fecha) => VigenciaDeRecaudo.Incluye(ValidFrom, ValidTo, fecha);
}
