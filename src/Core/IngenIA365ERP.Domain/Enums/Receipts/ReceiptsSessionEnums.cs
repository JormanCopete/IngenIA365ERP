namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, US2 (T0089; data-model §2, §4.4, §19.2, §20): las enumeraciones de la sesión de caja, el arqueo y la bóveda. Se guardan como int y un valor
// nunca se renumera. Las que data-model §20 no numera son (nuevo) y quedan en DT §2.7.

/// <summary>El estado de una sesión de caja (data-model §19.2). Un <see cref="Closed"/> no se reabre: se corrige con un ajuste.</summary>
public enum CashSessionState
{
    Open = 1,
    Closing = 2,
    ClosedWithDifferencePending = 3,
    Closed = 4,
    ForgottenOpen = 5,
}

/// <summary>La clase de un movimiento de caja (data-model §2.4).</summary>
public enum CashMovementKindR
{
    Opening = 1,
    SupplyFromVault = 2,
    WithdrawToVault = 3,
    TransferBetweenBoxes = 4,
    MeansReclassification = 5,
    Custody = 6,
    CustodyRelease = 7,
}

/// <summary>Qué se hace con una diferencia de arqueo (data-model §2.3): nada, cuenta por cobrar al cajero, gasto o sobrante.</summary>
public enum CountTreatment
{
    None = 0,
    CashierReceivable = 1,
    Expense = 2,
    Surplus = 3,
}

/// <summary>La clase de un arqueo: el del cierre, el sorpresivo o el diferido de una sesión olvidada (data-model §2.3). (nuevo)</summary>
public enum CashCountKind
{
    Closing = 1,
    Surprise = 2,
    Deferred = 3,
}

/// <summary>El estado del día de un punto (data-model §2.1). (nuevo)</summary>
public enum CollectionPointDayStatus
{
    Open = 1,
    Closed = 2,
}

/// <summary>La clase de un ajuste de una sesión cerrada (data-model §2.5). (nuevo)</summary>
public enum SessionAdjustmentKind
{
    CountCorrection = 1,
    MeansCorrection = 2,
}

/// <summary>El estado del cuadre diario de una oficina (data-model §2.8). (nuevo)</summary>
public enum DailySquareStatus
{
    Squared = 1,
    Difference = 2,
    Explained = 3,
}

/// <summary>El estado de una cuenta por cobrar a un cajero (data-model §4.4). (nuevo)</summary>
public enum CashierReceivableState
{
    Open = 1,
    Collected = 2,
    WrittenOff = 3,
}

/// <summary>El estado de un sobrante de caja (data-model §4.4). (nuevo)</summary>
public enum CashSurplusState
{
    Liability = 1,
    MovedToIncome = 2,
    Returned = 3,
}

/// <summary>La clase de un movimiento de bóveda (data-model §2.6); <see cref="RetainedCounterfeit"/> es el billete falso retenido con acta. (nuevo)</summary>
public enum VaultMovementKind
{
    SupplyToBox = 1,
    ReceiveFromBox = 2,
    RemittanceOut = 3,
    RemittanceIn = 4,
    Count = 5,
    RetainedCounterfeit = 6,

    /// <summary>El saldo de la bóveda al corte que trae la puesta en marcha (<c>recaudo.bovedas-al-corte</c>, US13): sin hecho contable. (nuevo)</summary>
    OpeningBalance = 7,
}
