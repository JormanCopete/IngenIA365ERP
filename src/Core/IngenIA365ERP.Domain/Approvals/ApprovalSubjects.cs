namespace IngenIA365ERP.Domain.Approvals;

/// <summary>
/// Qué se aprueba (feature 012, T33; decisiones-transversales §2.5; data-model §21). Son constantes de texto, no un
/// enum: se guardan tal cual en <c>COR_ApprovalPolicies.Subject</c> y <c>COR_ApprovalRequests.Subject</c> y la API
/// las recibe por nombre. Otro valor es <c>Validation.Invalid</c>. La tabla de sujetos de data-model §21 es la única
/// fuente de qué política rige cada uno.
/// </summary>
public static class ApprovalSubjects
{
    /// <summary>Confirmar un documento de inventario (la política del tipo; el nivel 1 forzado por monto máximo).</summary>
    public const string DocumentConfirmation = "DocumentConfirmation";

    /// <summary>Un descuento de línea sobre el tope (regla fija de un nivel si no hay política).</summary>
    public const string DiscountOverCap = "DiscountOverCap";

    /// <summary>El crédito provisional de una venta (regla fija de un nivel si no hay política; T32).</summary>
    public const string ProvisionalCredit = "ProvisionalCredit";

    /// <summary>La resolución de un faltante o sobrante de traslado.</summary>
    public const string TransferDiscrepancy = "TransferDiscrepancy";

    /// <summary>Una excepción del cruce a tres vías de compras (I5).</summary>
    public const string PurchaseMatchException = "PurchaseMatchException";

    /// <summary>
    /// Feature 014, US4 (T111; api.md §1.6 de la 014; FR-051, US4-5): un retiro o un traslado de una cuenta de captación que supera la atribución con monto
    /// <c>Lending.Accounts.Withdraw</c> del usuario que actúa. Regla fija de un nivel; lo aprueba un presente con segundo factor. (nuevo)
    /// </summary>
    public const string AccountWithdrawal = "AccountWithdrawal";

    /// <summary>
    /// Feature 014, US4 (T111; api.md §1.6 de la 014; FR-059): una consignación en efectivo que supera la atribución con monto
    /// <c>Lending.Accounts.Deposit</c> del usuario que actúa (control de efectivo). Regla fija de un nivel. (nuevo)
    /// </summary>
    public const string AccountCashDeposit = "AccountCashDeposit";

    /// <summary>
    /// Feature 015, US12 (T0061; FR-013, FR-032; api.md §1.4): una diferencia de arqueo sobre la tolerancia del medio. Regla fija de un nivel con
    /// <c>Receipts.Count.ApproveDifference</c> (con monto); lo aprueba un presente con segundo factor, otra persona. (nuevo)
    /// </summary>
    public const string CashCountDifference = "CashCountDifference";

    /// <summary>Feature 015 (T0061; FR-030): la reclasificación entre medios de un pago ya confirmado. Regla fija de un nivel con <c>Receipts.Session.Reclassify</c>. (nuevo)</summary>
    public const string CashMeansReclassification = "CashMeansReclassification";

    /// <summary>Feature 015 (T0061; FR-047, FR-192): un recibo de caja con fecha valor anterior. Regla fija de un nivel con <c>Receipts.Receipt.Backdate</c> (con monto). (nuevo)</summary>
    public const string ReceiptBackdate = "ReceiptBackdate";

    /// <summary>Feature 015 (T0061; FR-033): el ajuste de una sesión de caja cerrada. Regla fija de un nivel con <c>Receipts.Session.Adjust</c>. (nuevo)</summary>
    public const string CashSessionAdjustment = "CashSessionAdjustment";

    /// <summary>Feature 015 (T0061; FR-057): la anulación de un recibo. Regla fija de un nivel con <c>Receipts.Voiding.Approve</c> (con monto). (nuevo)</summary>
    public const string ReceiptVoiding = "ReceiptVoiding";

    /// <summary>
    /// Feature 015 (T0061; FR-013, FR-110; US12-3): la aprobación diferida de un lote de pago de Tesorería, por niveles sucesivos hasta cubrir el monto.
    /// Sin política, regla fija de un nivel con <c>Treasury.Batches.Approve</c> (con monto). (nuevo)
    /// </summary>
    public const string PaymentBatchApproval = "PaymentBatchApproval";

    /// <summary>Feature 015 (T0061; FR-207): un uso del fondo de liquidez. Regla fija de un nivel con <c>Treasury.LiquidityFund.Approve</c> (con monto). (nuevo)</summary>
    public const string LiquidityFundUse = "LiquidityFundUse";

    /// <summary>Los cinco de data-model §21, en su orden, los dos de captaciones de la feature 014 y los siete de Recaudo y Tesorería de la 015.</summary>
    public static readonly IReadOnlyList<string> Todos =
        [DocumentConfirmation, DiscountOverCap, ProvisionalCredit, TransferDiscrepancy, PurchaseMatchException, AccountWithdrawal, AccountCashDeposit,
            CashCountDifference, CashMeansReclassification, ReceiptBackdate, CashSessionAdjustment, ReceiptVoiding, PaymentBatchApproval, LiquidityFundUse];

    /// <summary>Si el texto es uno de los sujetos, escrito exacto (distingue mayúsculas).</summary>
    public static bool EsValido(string? sujeto) => sujeto is not null && Todos.Contains(sujeto, StringComparer.Ordinal);
}

/// <summary>
/// De qué es una solicitud (<c>COR_ApprovalRequests.SourceType</c>; data-model §21). Cada historia registra un
/// <c>IFuenteDeAprobacion</c> por el suyo. (nuevo)
/// </summary>
public static class ApprovalSourceTypes
{
    public const string InventoryDocument = "InventoryDocument";
    public const string DocumentLineDiscount = "DocumentLineDiscount";
    public const string DocumentPayment = "DocumentPayment";
    public const string TransferDiscrepancy = "TransferDiscrepancy";
    public const string PurchaseMatchLine = "PurchaseMatchLine";

    /// <summary>Feature 014, US4 (T111): un retiro, un traslado o una consignación en efectivo de una cuenta de captación sobre la atribución. (nuevo)</summary>
    public const string MemberAccountOperation = "MemberAccountOperation";

    /// <summary>Feature 015 (T0061): una operación de Recaudo con aprobador presente (arqueo, reclasificación, fecha valor, ajuste, anulación). (nuevo)</summary>
    public const string ReceiptsOperation = "ReceiptsOperation";

    /// <summary>Feature 015 (T0061): un documento de Tesorería con aprobación diferida (lote de pago, uso del fondo de liquidez). (nuevo)</summary>
    public const string TreasuryDocument = "TreasuryDocument";
}
