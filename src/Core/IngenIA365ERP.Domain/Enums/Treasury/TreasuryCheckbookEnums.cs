namespace IngenIA365ERP.Domain.Enums.Treasury;

// Feature 015, US19 (T0225; data-model §11): las enumeraciones de las chequeras, los cheques girados y las remesas. Se guardan como int y un valor nunca se renumera.
// Todas son (nuevo) y quedan en DT §2.7.

public enum CheckbookState
{
    Active = 1,
    Exhausted = 2,
    Voided = 3,
}

public enum IssuedCheckState
{
    Issued = 1,
    Delivered = 2,
    Cashed = 3,
    Voided = 4,
    NotCashed = 5,
    SpoiledBlank = 6,
}

public enum RemittanceDirection
{
    VaultToBank = 1,
    BankToVault = 2,
}

public enum RemittanceState
{
    InTransit = 1,
    Confirmed = 2,
    Expired = 3,
    Voided = 4,
}
