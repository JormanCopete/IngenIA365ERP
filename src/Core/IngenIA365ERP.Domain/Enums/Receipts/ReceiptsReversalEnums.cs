namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, US5 (T0174; data-model §4.1 a §4.3, §20): las enumeraciones de la anulación, los reversos de los módulos y los saldos a favor. Se guardan como int y
// un valor nunca se renumera. Las que data-model §20 no numera son (nuevo) y quedan en DT §2.7.

/// <summary>El estado de la anulación de un recibo (nuevo).</summary>
public enum ReceiptVoidingState
{
    PendingApproval = 1,
    Applied = 2,
    Rejected = 3,
}

/// <summary>La causa del dinero de una aplicación reversada por el módulo (data-model §20).</summary>
public enum ModuleReversalCause
{
    MoneyReceived = 1,
    MoneyNotReceived = 2,
    PendingConfirmation = 3,
}

/// <summary>De quién es el saldo a favor (nuevo).</summary>
public enum PayerCreditBalanceKind
{
    Payer = 1,
    Paymaster = 2,
}

/// <summary>De dónde nació el saldo a favor (nuevo; <c>LendingRefund</c> es la devolución de un saldo de Cartera por el recaudador: nace devuelto por orden y sólo
/// queda abierto si Tesorería informa la orden rechazada o anulada).</summary>
public enum PayerCreditBalanceOrigin
{
    NonInteractiveSurplus = 1,
    VoidAfterSessionClose = 2,
    ModuleReversal = 3,
    PaymasterSurplus = 4,
    PaymasterUnapplied = 5,
    RejectedLine = 6,
    LendingRefund = 7,
}

/// <summary>El estado del saldo a favor (nuevo).</summary>
public enum PayerCreditBalanceState
{
    Open = 1,
    Resolved = 2,
}

/// <summary>Un movimiento del saldo a favor (nuevo; <c>RefundRejected</c> lo reabre cuando Tesorería informa la orden rechazada o anulada).</summary>
public enum PayerCreditBalanceMovementKind
{
    Recognized = 1,
    RefundedByOrder = 2,
    RefundedByCash = 3,
    AppliedByReceipt = 4,
    RefundRejected = 5,
}

/// <summary>El estado de la cuenta por cobrar al pagador (nuevo).</summary>
public enum PayerReceivableState
{
    Open = 1,
    Collected = 2,
    WrittenOff = 3,
}
