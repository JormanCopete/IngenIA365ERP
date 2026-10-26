namespace IngenIA365ERP.Domain.Enums.Treasury;

// Feature 015, T2 (fases 40 a 45; data-model §13.2, §20): las enumeraciones de la Tesorería ampliada. Se guardan como int y un valor nunca se renumera. Todas son (nuevo)
// y quedan en DT §2.7.

/// <summary>Lo que trae una fila de la carga de pendientes heredados (FR-161): un cheque girado sin cobrar o una factura por pagar. (nuevo)</summary>
public enum LegacyPendingKind
{
    UncashedCheck = 1,
    PayableInvoice = 2,
}

/// <summary>El estado de una fila de pendientes heredados: cargada (cotejada con las cifras de referencia) o ya convertida en lo de Tesorería. (nuevo)</summary>
public enum LegacyPendingLoadState
{
    Loaded = 1,
    Migrated = 2,
}

/// <summary>La clase de una nota bancaria (FR-130, FR-218): GMF, comisión (con su IVA), intereses en bruto (con su retención), impuestos u otra. (nuevo)</summary>
public enum BankNoteKind
{
    Gmf = 1,
    Commission = 2,
    Interest = 3,
    Tax = 4,
    Other = 5,
}

/// <summary>El estado de una nota bancaria: por aprobar (otra persona), aprobada —con su hecho— o rechazada. (nuevo)</summary>
public enum BankNoteState
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
}

/// <summary>El criterio de una regla de emparejamiento de la conciliación (FR-129). (nuevo)</summary>
public enum ReconciliationRuleCriterion
{
    /// <summary>La misma referencia y el mismo valor, con la holgura de días de la regla.</summary>
    ReferenceAndAmount = 1,

    /// <summary>El mismo valor y la fecha dentro de la holgura (un solo candidato).</summary>
    AmountAndDate = 2,

    /// <summary>El mismo valor y la misma fecha.</summary>
    ExactAmountSameDay = 3,
}

/// <summary>El sentido de una categoría del flujo de caja (FR-138). (nuevo)</summary>
public enum CashFlowDirection
{
    Inflow = 1,
    Outflow = 2,
}

/// <summary>De dónde sale lo proyectado de una categoría del flujo de caja (FR-138): digitado o de un componente automático. (nuevo)</summary>
public enum CashFlowCategorySource
{
    /// <summary>Lo que se digita por período.</summary>
    Manual = 1,

    /// <summary>Las cuotas por vencer de Cartera (IRecaudosEsperadosDeCartera).</summary>
    ExpectedLending = 2,

    /// <summary>Las cuotas de aportes y ahorros por vencer de Captaciones (IRecaudosEsperadosDeCaptaciones).</summary>
    ExpectedDeposits = 3,

    /// <summary>Las órdenes abiertas de la bandeja por su fecha de pago.</summary>
    PaymentOrders = 4,

    /// <summary>Las dispersiones de Nómina informadas, por su fecha de pago.</summary>
    PayrollDispersions = 5,
}

/// <summary>El período del flujo de caja (FR-138). (nuevo)</summary>
public enum CashFlowPeriodKind
{
    Week = 1,
    Month = 2,
}

/// <summary>El estado de una conciliación de adquirente (FR-133): explicada, por aprobar (sobre la tolerancia) o aprobada. (nuevo)</summary>
public enum AcquirerSettlementState
{
    Explained = 1,
    PendingApproval = 2,
    Approved = 3,

    /// <summary>La diferencia no se aceptó: la liquidación queda sin hecho y se carga de nuevo corregida (fase 44). (nuevo)</summary>
    Rejected = 4,
}

/// <summary>El origen de un lote de datáfono en una conciliación de adquirente: Recaudo o el punto de venta de Comercial. (nuevo)</summary>
public enum AcquirerBatchSource
{
    Receipts = 1,
    PointOfSale = 2,
}
