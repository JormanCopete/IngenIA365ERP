namespace IngenIA365ERP.Domain.Enums.Treasury;

// Feature 015, US25 (T0276; data-model §12.2–§12.4, §20): las enumeraciones de la conciliación bancaria mínima de Tesorería. Se guardan como int y un valor nunca se
// renumera. ReconciliationState la numera data-model §20; las demás son (nuevo) y quedan en DT §2.7.

/// <summary>El estado de una conciliación (data-model §20). <see cref="Outdated"/> lo pone sólo la reapertura del período contable (E-04, TR41).</summary>
public enum ReconciliationState
{
    Open = 1,
    Closed = 2,
    Reopened = 3,
    Outdated = 4,
}

/// <summary>La clase de conciliación: la mínima de T0 o la completa de T2 (US17). (nuevo)</summary>
public enum ReconciliationKind
{
    Minimal = 1,
    Complete = 2,
}

/// <summary>De dónde viene el registro del lado de los libros que se cruza con una línea del extracto (data-model §12.3). (nuevo)</summary>
public enum ReconciliationRecordKind
{
    ReceiptsBankReceipt = 1,
    Remittance = 2,
    TreasuryPayment = 3,
    IssuedCheck = 4,
    LedgerMovement = 5,
    LendingDisbursementFile = 6,
}

/// <summary>Cómo se cruzó: el motor o una persona. (nuevo)</summary>
public enum ReconciliationMatchMode
{
    Automatic = 1,
    Manual = 2,
}

/// <summary>La clase de una partida conciliatoria (data-model §12.4). (nuevo)</summary>
public enum ReconcilingItemKind
{
    IssuedCheckNotCashed = 1,
    DepositInTransit = 2,
    RemittanceInTransit = 3,
    BankChargeNotRecorded = 4,
    BankCreditNotRecorded = 5,
    UnidentifiedCredit = 6,
    UnidentifiedDebit = 7,
}
