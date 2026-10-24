namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, US6 (T0192; data-model §5, §19.6, §20): las enumeraciones de la identificación de abonos, las partidas por identificar y las corridas no interactivas.
// Se guardan como int y un valor nunca se renumera. Las que data-model §20 no numera son (nuevo) y quedan en DT §2.7.

/// <summary>Qué se hizo con (una parte de) un abono del extracto (data-model §5.1). (nuevo)</summary>
public enum BankItemOutcome
{
    Receipt = 1,
    UnidentifiedItem = 2,
    Classified = 3,
    ReturnedDisbursement = 4,
    PaymasterRemittance = 5,
    PossibleDuplicateDismissed = 6,

    /// <summary>R2 (fase 37; FR-084): el abono del extracto que paga cheques de un lote de canje. (nuevo)</summary>
    ClearingBatch = 7,

    /// <summary>R2 (fase 38; FR-093): el abono del extracto que liquida un archivo de un convenio de recaudo o de una pasarela. (nuevo)</summary>
    AgreementSettlement = 8,
}

/// <summary>El estado de una partida por identificar (data-model §19.6).</summary>
public enum UnidentifiedItemState
{
    Open = 1,
    PartiallyIdentified = 2,
    Identified = 3,
    Classified = 4,
    ReturnedToDepositor = 5,
}

/// <summary>De dónde nació la partida (data-model §5.2). (nuevo)</summary>
public enum UnidentifiedItemOrigin
{
    Statement = 1,
    Reconciliation = 2,
    OpeningLoad = 3,
}

/// <summary>
/// Cómo se resolvió (una parte de) una partida (data-model §5.2). <see cref="RefundRejected"/> devuelve el saldo cuando Tesorería informa la orden de la devolución
/// rechazada, con cheque no cobrado o anulada (nuevo). (nuevo)
/// </summary>
public enum UnidentifiedItemResolutionKind
{
    IdentifiedToReceipt = 1,
    ClassifiedToConcept = 2,
    RefundedByOrder = 3,
    MovedToIncome = 4,
    RefundRejected = 5,
}

/// <summary>Qué procesa una corrida no interactiva (data-model §5.3). (nuevo)</summary>
public enum ProcessingRunKind
{
    BankItem = 1,
    PaymasterRemittance = 2,
    AgreementFile = 3,
}

/// <summary>El resultado de una línea de una corrida (data-model §5.3); <see cref="Pending"/> espera su tanda. (nuevo)</summary>
public enum ProcessingLineOutcome
{
    Applied = 1,
    AlreadyApplied = 2,
    Rejected = 3,
    Skipped = 4,
    Pending = 5,
}
