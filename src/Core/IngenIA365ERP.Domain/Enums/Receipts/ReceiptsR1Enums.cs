namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, R1 (migración RecaudoCompleto; data-model §7.3, §8.1): las enumeraciones de lo que trae la entrega R1. Dominio en inglés y valores fijos (data-model
// §20; LosEnumsDelContratoNumeranComoElDominio para los que viajan en mensajes). (nuevo)

/// <summary>El estado de un recaudo pendiente de aplicar (FR-046). (nuevo)</summary>
public enum PendingApplicationState
{
    Pending = 1,
    Applied = 2,
    Voided = 3,
}

/// <summary>Qué entrega un egreso de caja (FR-051 a FR-056; data-model §8.1). <see cref="CustodyRelease"/> es la devolución del dinero retenido en custodia. (nuevo)</summary>
public enum CashDisbursementKind
{
    SavingsWithdrawal = 1,
    LendingCreditBalanceRefund = 2,
    PayerCreditBalanceRefund = 3,
    MiscExpense = 4,
    AccountClosingDelivery = 5,
    CustodyRelease = 6,
}

/// <summary>El estado de un egreso de caja: entregado o anulado (el dinero volvió). (nuevo)</summary>
public enum CashDisbursementStatus
{
    Delivered = 1,
    Voided = 2,
}

/// <summary>El estado de una consignación reportada en ventanilla (FR-069; D11). (nuevo)</summary>
public enum ReportedDepositStatus
{
    ToVerify = 1,
    Verified = 2,
    Expired = 3,
}

/// <summary>El criterio de una regla de identificación de abonos (FR-067; D15). (nuevo)</summary>
public enum IdentificationCriterion
{
    PaymentReference = 1,
    DepositorTaxId = 2,
    ReportedDeposit = 3,
    ExactDueAmount = 4,
}

/// <summary>Qué transacciones entran al reporte de transacciones (FR-096; <c>Sarlaft.Reporte.Alcance</c>). (nuevo)</summary>
public enum CashTransactionReportScope
{
    /// <summary>Sólo el efectivo.</summary>
    CashOnly = 1,

    /// <summary>Efectivo, cheque y plataforma virtual: los tres medios del Anexo 2 TRSO (el defecto desde la segunda ronda del 2026-10-27).</summary>
    All = 2,

    /// <summary>Efectivo y cheque.</summary>
    CashAndCheck = 3,
}

/// <summary>El estado de aplicación del reporte en el período, sellado al generar (<c>Sarlaft.Reporte.Estado</c>). (nuevo)</summary>
public enum CashTransactionReportApplication
{
    Active = 1,
    Suspended = 2,
}

/// <summary>El estado de una generación del reporte (molde de <c>LND_RegulatoryFiles</c>). (nuevo)</summary>
public enum CashTransactionReportStatus
{
    Validated = 1,
    Generated = 2,
    Superseded = 3,
    Uploaded = 4,
}

/// <summary>La clase de una línea del reporte: transacción individual sobre el umbral o múltiples del mes de una persona. (nuevo)</summary>
public enum CashTransactionReportLineKind
{
    Individual = 1,
    Multiple = 2,
}
