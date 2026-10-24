using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;

/// <summary>
/// <c>TES_BankAccounts</c> (feature 015, US10, T0135; FR-102, FR-103, FR-207; data-model §9.2): una cuenta bancaria propia de la cooperativa —banco con su
/// código de transferencia, número, tipo, oficina, propósitos, exención de GMF y el banco como tercero—. El vínculo con su cuenta contable no está aquí: lo
/// guarda Contabilidad en <c>ACC_OwnBankAccountLinks</c> y la pantalla lo ve por <c>IContabilidadParaTesoreria</c>. Con el propósito
/// <see cref="BankAccountPurpose.LiquidityFund"/> la cuenta no admite otro (FR-207). <see cref="Code"/> es la nomenclatura de la cooperativa
/// (<c>CodigoDeCatalogo</c>), el que viaja en las dimensiones de los hechos (<c>OwnBankAccountCode</c>, nuevo). No es un hecho: cambia por sus comandos y
/// nunca se borra; se inactiva con su vigencia. (nuevo)
/// </summary>
public class OwnBankAccount : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    /// <summary>El nombre con que la cooperativa la reconoce («Bancolombia recaudo»).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary><c>COR_Banks.Id</c>; el banco tiene que tener <c>TransferCode</c> (<c>Treasury.BankAccount.BankWithoutTransferCode</c>).</summary>
    public int BankId { get; set; }

    public string AccountNumber { get; set; } = string.Empty;

    public OwnBankAccountType AccountType { get; set; } = OwnBankAccountType.Savings;

    /// <summary>La oficina (<c>COR_Branches.Id</c>).</summary>
    public int BranchId { get; set; }

    public BankAccountPurpose Purposes { get; set; }

    public bool IsGmfExempt { get; set; }

    /// <summary>El banco como tercero (<c>COR_People.Id</c>) para la exógena y las cuentas que exigen tercero (FR-222).</summary>
    public int? BankThirdPartyPersonId { get; set; }

    public TreasuryCatalogStatus Status { get; set; } = TreasuryCatalogStatus.Active;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Proyección del último extracto cargado (FR-103); la escribe la carga del extracto (fase 11).</summary>
    public decimal? LastStatementBalance { get; set; }

    public DateOnly? LastStatementDate { get; set; }

    public bool EsDelFondoDeLiquidez => Purposes.HasFlag(BankAccountPurpose.LiquidityFund);

    public bool Activa(DateOnly fecha) => Status == TreasuryCatalogStatus.Active && ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}

/// <summary>
/// <c>TES_LiquidityFundUses</c> (feature 015, US10, T0135; FR-207; SC-028; data-model §9.4): un uso del fondo de liquidez con causal de la norma, soporte y
/// aprobación de otra persona. Un lote, una remesa o un traslado desde una cuenta del fondo exige un uso <see cref="LiquidityFundUseState.Approved"/> por el
/// monto (<c>Treasury.LiquidityFund.UseRequired</c>); al usarlo queda <see cref="LiquidityFundUseState.Used"/> con el documento que lo usó. Los abonos al
/// fondo no se restringen. No es un hecho: cambia por su aprobación y su uso. (nuevo)
/// </summary>
public class LiquidityFundUse : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public int BankAccountId { get; set; }

    public LiquidityFundCause Cause { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid SupportAttachmentPublicId { get; set; }

    public Guid? ApprovalRequestPublicId { get; set; }

    /// <summary>El aviso previo a la Supersolidaria (soporte); la alerta <c>Tesoreria.UsoDelFondoDeLiquidez</c> lo recuerda.</summary>
    public Guid? SupervisorNoticeAttachmentPublicId { get; set; }

    public LiquidityFundUseState Status { get; set; } = LiquidityFundUseState.PendingApproval;

    public int RequestedByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public int? DecidedByUserId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionReason { get; set; }

    /// <summary>El lote, la remesa o el traslado que lo usó.</summary>
    public Guid? UsedByDocumentPublicId { get; set; }
}
