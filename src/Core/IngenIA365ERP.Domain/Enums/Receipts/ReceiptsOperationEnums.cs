namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, US3 (T0110; data-model §3, §19.3, §19.4, §20): las enumeraciones de la operación de caja y el recibo. Se guardan como int y un valor nunca se
// renumera. Las que data-model §20 no numera son (nuevo) y quedan en DT §2.7.

/// <summary>El estado de la operación de caja mientras se prepara (data-model §19.3).</summary>
public enum CashOperationState
{
    Preparing = 1,
    Blocked = 2,
    Withheld = 3,
    Confirmed = 4,
    Abandoned = 5,
}

/// <summary>El resultado de la consulta a listas de la operación (data-model §3.2).</summary>
public enum ScreeningOutcomeR
{
    Clear = 1,
    BindingMatch = 2,
    NonBindingMatch = 3,
}

/// <summary>El estado del recibo (data-model §19.4).</summary>
public enum ReceiptState
{
    Valid = 1,
    VoidPendingApproval = 2,
    Voided = 3,
}

/// <summary>El estado de una aplicación del recibo (data-model §19.4).</summary>
public enum ApplicationState
{
    Applied = 1,
    ReversedByModule = 2,
    Voided = 3,
}

/// <summary>El estado de un medio del recibo (data-model §3.4). (nuevo)</summary>
public enum ReceiptMeansState
{
    Active = 1,
    WrittenOff = 2,
}

/// <summary>El estado de una porción (data-model §3.6). (nuevo)</summary>
public enum PortionState
{
    Applied = 1,
    Reversed = 2,
}

/// <summary>Quién exigió la declaración de origen de fondos (data-model §3.7). (nuevo)</summary>
public enum SourceOfFundsRequiredBy
{
    ReceiptsThreshold = 1,
    ModuleSimulation = 2,
}
