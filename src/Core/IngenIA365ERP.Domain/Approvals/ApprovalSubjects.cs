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

    /// <summary>Los cinco de data-model §21, en su orden, y los dos de captaciones de la feature 014.</summary>
    public static readonly IReadOnlyList<string> Todos =
        [DocumentConfirmation, DiscountOverCap, ProvisionalCredit, TransferDiscrepancy, PurchaseMatchException, AccountWithdrawal, AccountCashDeposit];

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
}
