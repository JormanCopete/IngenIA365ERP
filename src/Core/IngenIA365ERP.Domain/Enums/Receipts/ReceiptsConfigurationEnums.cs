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

/// <summary>El estado de un talonario de contingencia (FR-186). (nuevo)</summary>
public enum ContingencyBookState
{
    Active = 1,
    Exhausted = 2,
    Voided = 3,
}
