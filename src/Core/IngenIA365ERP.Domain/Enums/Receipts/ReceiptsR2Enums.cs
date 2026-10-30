namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, R2 (fases 37 y 38; data-model §8.2, §20): las enumeraciones de los canales de Recaudo. Se guardan como int y un valor nunca se renumera. Todas son (nuevo)
// y quedan en DT §2.7.

/// <summary>El estado de un lote de canje de cheques recibidos (FR-084): en canje, pagado en parte, pagado y cerrado (todo pagado o devuelto). (nuevo)</summary>
public enum ClearingBatchState
{
    InClearing = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Closed = 4,
}

/// <summary>La clase de un convenio de recaudo (FR-088, FR-091, FR-092): un banco, un corresponsal o una pasarela de pagos en línea (PSE). (nuevo)</summary>
public enum CollectionAgreementKind
{
    Bank = 1,
    Correspondent = 2,
    Gateway = 3,
}

/// <summary>A qué identifica la referencia de pago de un convenio (FR-088, FR-090): un producto (crédito o cuenta) o una persona. (nuevo)</summary>
public enum PaymentReferenceMode
{
    ByProduct = 1,
    ByPerson = 2,
}

/// <summary>El algoritmo del dígito de verificación de la referencia de pago (FR-088): sin dígito, módulo 10 (Luhn) o módulo 11. (nuevo)</summary>
public enum CheckDigitAlgorithm
{
    None = 0,
    Modulo10 = 1,
    Modulo11 = 2,
}

/// <summary>El estado de una referencia de pago: vigente o retirada (la del producto cancelado). Una referencia nunca se reutiliza. (nuevo)</summary>
public enum PaymentReferenceState
{
    Active = 1,
    Retired = 2,
}

/// <summary>El estado de un archivo de convenio o de pasarela cargado: aplicado del todo, con líneas que quedaron en partidas o rechazadas, o rechazado entero. (nuevo)</summary>
public enum CollectionFileState
{
    Applied = 1,
    AppliedWithExceptions = 2,
    Rejected = 3,
}

/// <summary>Lo que pasó con una línea de un archivo de convenio o de pasarela. (nuevo)</summary>
public enum CollectionFileLineOutcome
{
    Pending = 0,
    Applied = 1,
    UnidentifiedItem = 2,
    Rejected = 3,
    NotApproved = 4,
}

/// <summary>El estado de la conciliación de un archivo del convenio con su abono (FR-093): explicada, por aprobar (sobre la tolerancia) o aprobada. (nuevo)</summary>
public enum AgreementReconciliationState
{
    Explained = 1,
    PendingApproval = 2,
    Approved = 3,
}
