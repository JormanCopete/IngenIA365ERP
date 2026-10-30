namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, US14 (T0239; data-model §7.1): el registro de transacciones en efectivo. Se guardan como int y un valor nunca se renumera. (nuevo; DT §2.7)

public enum CashTransactionDocumentKind
{
    Receipt = 1,
    CashDisbursement = 2,
    Voiding = 3,
}

public enum CashTransactionDirection
{
    In = 1,
    Out = 2,
}
