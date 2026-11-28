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

    /// <summary>
    /// 2026-11-23 (cruce automático de anticipos): la porción reversada se había pagado con el medio de un anticipo de concepto; no hubo dinero, así que el valor
    /// vuelve al anticipo y el medio se reversa (la cuenta del anticipo) sin que nadie confirme nada.
    /// </summary>
    ReturnedToAdvance = 4,
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

    /// <summary>2026-11-23 (pedido del dueño): un anticipo de concepto recibido en la caja (un concepto vario marcado como anticipo).</summary>
    ConceptAdvance = 8,

    /// <summary>2026-11-23: un anticipo de concepto que venía del sistema anterior, cargado con su plantilla (sin hecho contable: ya está en la apertura).</summary>
    MigratedAdvance = 9,
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

    /// <summary>Feature 015, R1 (fase 26) (nuevo): la devolución en efectivo se anuló porque el dinero volvió a la caja; el saldo se reabre.</summary>
    CashRefundVoided = 6,

    /// <summary>2026-11-23: el anticipo se anuló con el recibo que lo recibió.</summary>
    VoidedWithReceipt = 7,

    /// <summary>2026-11-23: el recibo que cruzó el anticipo se anuló; lo cruzado vuelve al anticipo.</summary>
    ReopenedByVoiding = 8,

    /// <summary>2026-11-23 (cruce automático): el módulo reversó el pago que el anticipo había cruzado; lo reversado vuelve al anticipo.</summary>
    ReturnedByModuleReversal = 9,

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T069): lo que la pagaduría giró por un crédito ya recogido pasó a saldo a favor del asociado
    /// (<c>Recogida.GiroPosteriorDeCreditoRecogido = AFavorDelAsociado</c>): sale del saldo de la pagaduría y nace el de la persona.
    /// </summary>
    TransferredToAssociate = 10,
}

/// <summary>El estado de la cuenta por cobrar al pagador (nuevo).</summary>
public enum PayerReceivableState
{
    Open = 1,
    Collected = 2,
    WrittenOff = 3,
}
