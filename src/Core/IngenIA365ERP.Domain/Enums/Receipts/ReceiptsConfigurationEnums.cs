namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, US1 (T0070; data-model §1, §2.6, §20): las enumeraciones de la configuración de Recaudo. Se guardan como int y un valor nunca se
// renumera. Las que data-model §20 no numera (estado de catálogo, modo de base, modo de bóveda, sentido del concepto, estado del talonario) son
// (nuevo) y quedan en DT §2.7.

/// <summary>La clase de un tipo de documento de Recaudo (FR-026). (data-model §20)</summary>
public enum ReceiptDocumentKind
{
    CashReceipt = 1,
    BankReceipt = 2,
    AgreementReceipt = 3,
    PaymasterReceipt = 4,
    OnlineReceipt = 5,
    ContingencyReceipt = 6,
    CashDisbursement = 7,
    Voiding = 8,
    CashMovement = 9,
    SessionAdjustment = 10,

    /// <summary>2026-11-23: el recibo con que el proceso cruza un anticipo de concepto contra la obligación que acaba de nacer (su propio consecutivo).</summary>
    AdvanceCrossingReceipt = 11,

    /// <summary>
    /// 2026-11-27 (feature 028, E4, T085; HU7): el recibo sin caja con que el desembolso de un crédito nuevo recoge facturas a crédito de Comercial (su propio
    /// consecutivo: lo crea la cooperativa en Tipos de documento, como el del cruce de anticipos). (nuevo)
    /// </summary>
    LoanDisbursementReceipt = 12,
}

/// <summary>El canal por el que entra el dinero de un recibo (FR-198). (data-model §20)</summary>
public enum ReceiptChannel
{
    CashDesk = 1,
    Bank = 2,
    Paymaster = 3,
    Agreement = 4,
    Online = 5,
    Contingency = 6,

    /// <summary>
    /// 2026-11-23: el cruce automático de un anticipo de concepto al nacer la obligación que cubre. No entra dinero: el medio es el del anticipo y el hecho es
    /// el del recibo de caja (<c>ReciboEmitido</c>), así la matriz es la misma que la del cruce a mano.
    /// </summary>
    AdvanceCrossing = 7,

    /// <summary>
    /// 2026-11-27 (feature 028, E4, T085; HU7, FR-026): la recogida de facturas a crédito de Comercial en el desembolso de un crédito nuevo. No entra dinero: el
    /// medio es el cruce interno (<c>InternalOffset</c>) y el hecho es el del recibo de caja (<c>ReciboEmitido</c>), así Contabilidad y Transporte se enteran
    /// como con un pago en la caja. Nace en la transacción del desembolso y sólo se anula anulando el crédito nuevo. (nuevo)
    /// </summary>
    LoanDisbursement = 8,
}

/// <summary>A qué se aplica una aplicación de un recibo (y una regla de modalidad por canal). (data-model §20)</summary>
public enum ApplicationTarget
{
    Loan = 1,
    Account = 2,
    MiscConcept = 3,
    CommercialReceivable = 4,
    PendingApplication = 5,
}

/// <summary>Si un catálogo de Recaudo se ofrece (activo) o ya no (inactivo; no se borra). (nuevo)</summary>
public enum ReceiptsCatalogStatus
{
    Active = 1,
    Inactive = 2,
}

/// <summary>Cómo se lleva la base de una caja (D7; FR-028): base del día o fondo fijo; se sella al abrir la sesión. (nuevo)</summary>
public enum CashBoxBaseMode
{
    DailyBase = 1,
    FixedFund = 2,
}

/// <summary>Una bóveda propia de la oficina o la caja principal que hace de bóveda (FR-180). (nuevo)</summary>
public enum VaultMode
{
    Vault = 1,
    MainCashBox = 2,
}

/// <summary>Si un concepto vario es de ingreso o de egreso (FR-027). (nuevo)</summary>
public enum MiscConceptDirection
{
    Income = 1,
    Expense = 2,
}

/// <summary>
/// Cómo se elige el tercero de un concepto vario en el recibo (2026-11-16, decisión del dueño): quien paga (como siempre), el tercero fijo del concepto (la
/// aseguradora del SOAT) o el que elige el cajero en cada cobro. El tercero contable del concepto es el beneficiario del ítem. (nuevo)
/// </summary>
public enum MiscConceptThirdPartyMode
{
    Payer = 1,
    FixedThirdParty = 2,
    ChosenByCashier = 3,
}

/// <summary>El estado de un talonario de contingencia (FR-186). (nuevo)</summary>
public enum ContingencyBookState
{
    Active = 1,
    Exhausted = 2,
    Voided = 3,
}

/// <summary>
/// El canal de un aviso de Recaudo al pagador (FR-020, FR-045): Recaudo no conoce el <c>MessageChannel</c> de Cartera (FR-008,
/// <c>LaCarteraSoloSeTocaPorSusInterfaces</c>); los valores coinciden con los suyos. Sólo el correo sale de verdad; SMS y mensajería siguen simulados. (nuevo)
/// </summary>
public enum ReceiptNoticeChannel
{
    Email = 1,
    Sms = 2,
    WhatsApp = 3,
}
