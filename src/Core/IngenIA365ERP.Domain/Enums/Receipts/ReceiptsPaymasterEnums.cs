namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, US7 (T0205; data-model §6, §19.7, §20): las enumeraciones de las pagadurías y la libranza en Recaudo. Se guardan como int y un valor nunca se renumera.
// Todas son (nuevo) y quedan en DT §2.7.

/// <summary>El estado del archivo de descuentos que Recaudo recibió de Cartera (data-model §19.7). (nuevo)</summary>
public enum PayrollDeductionFileState
{
    Received = 1,
    Sent = 2,
    Responded = 3,
    Superseded = 4,
}

/// <summary>Qué cobra una línea del archivo: la cuota de un crédito o la de una cuenta. (nuevo)</summary>
public enum PayrollDeductionLineKind
{
    Loan = 1,
    AccountInstallment = 2,
}

/// <summary>Por dónde se envió el archivo a la pagaduría (FR-076). (nuevo)</summary>
public enum PayrollFileChannel
{
    Email = 1,
    Portal = 2,
    Physical = 3,
    Other = 4,
}

/// <summary>Cómo se cargó la respuesta de la pagaduría (FR-077, FR-201). (nuevo)</summary>
public enum PaymasterResponseSource
{
    Format = 1,
    GenericTemplate = 2,
    Manual = 3,
}

/// <summary>El estado de una versión de la respuesta (data-model §19.7). (nuevo)</summary>
public enum PaymasterResponseState
{
    Previewed = 1,
    Emitted = 2,
    RejectedByLending = 3,
    AcceptedByLending = 4,
}

/// <summary>Lo que la pagaduría informa de una línea. (nuevo)</summary>
public enum PaymasterDeductionOutcome
{
    Deducted = 1,
    PartiallyDeducted = 2,
    NotApplied = 3,
}

/// <summary>De dónde salió el dinero del giro de la pagaduría (FR-078). (nuevo)</summary>
public enum PaymasterRemittanceSource
{
    BankItem = 1,
    CashReceipt = 2,
    CheckReceived = 3,
}

/// <summary>El estado del giro (data-model §6.5). (nuevo)</summary>
public enum PaymasterRemittanceState
{
    Registered = 1,
    Applying = 2,
    Applied = 3,
    PartiallyApplied = 4,
}

/// <summary>En qué orden cubre el giro las líneas descontadas (FR-079). (nuevo)</summary>
public enum PaymasterAllocationOrder
{
    AgreementPriority = 1,
    AnalystOrder = 2,
}

/// <summary>El resultado de una línea cubierta por el giro (data-model §6.5). (nuevo)</summary>
public enum PaymasterRemittanceLineOutcome
{
    Pending = 1,
    Applied = 2,
    Rejected = 3,
}
