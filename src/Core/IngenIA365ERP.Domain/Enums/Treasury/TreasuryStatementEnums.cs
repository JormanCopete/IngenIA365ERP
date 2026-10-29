namespace IngenIA365ERP.Domain.Enums.Treasury;

// Feature 015, US6 (T0191; data-model §12.1, §20): las enumeraciones del extracto bancario de Tesorería. Se guardan como int y un valor nunca se renumera. Las que
// data-model §20 no numera son (nuevo) y quedan en DT §2.7.

/// <summary>Cómo se cargó el extracto: con el formato del banco, la plantilla genérica o a mano (FR-196). (nuevo)</summary>
public enum BankStatementSource
{
    BankFormat = 1,
    GenericTemplate = 2,
    Manual = 3,
}

/// <summary>El sentido de un movimiento del extracto. (nuevo)</summary>
public enum StatementLineDirection
{
    Credit = 1,
    Debit = 2,
}

/// <summary>La decisión de una persona sobre una línea marcada «posible duplicado» (FR-197). (nuevo)</summary>
public enum StatementLineDuplicateDecision
{
    Pending = 1,
    Distinct = 2,
    Duplicate = 3,
}

/// <summary>Lo que Recaudo hizo con un abono (lo escribe el puerto <c>IMovimientosBancariosParaRecaudo</c>). Los cargos son <see cref="NotApplicable"/>. (nuevo)</summary>
public enum StatementLineRecaudoStatus
{
    NotApplicable = 1,
    Pending = 2,
    Identified = 3,
    PartiallyIdentified = 4,
}

/// <summary>El estado de la línea en la conciliación (fase 17). (nuevo)</summary>
public enum StatementLineReconciliationStatus
{
    Open = 1,
    Matched = 2,
    ManuallyMarked = 3,
}
