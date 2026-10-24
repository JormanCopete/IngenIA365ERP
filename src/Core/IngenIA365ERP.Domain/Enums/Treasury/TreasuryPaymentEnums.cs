namespace IngenIA365ERP.Domain.Enums.Treasury;

// Feature 015, US10 y US11 (T0135, T0157; data-model §9, §10, §19.8 a §19.10, §20): las enumeraciones de las cuentas bancarias propias, el fondo de
// liquidez, las órdenes de pago, los lotes, los archivos y los resultados de la Tesorería nueva. Se guardan como int y un valor nunca se renumera. Las que
// data-model §20 no numera son (nuevo) y quedan en DT §2.7.

/// <summary>Para qué sirve una cuenta bancaria propia (data-model §9.2, §20). <see cref="LiquidityFund"/> es exclusivo (FR-207).</summary>
[Flags]
public enum BankAccountPurpose
{
    None = 0,
    Collection = 1,
    Payments = 2,
    Payroll = 4,
    Agreement = 8,
    LiquidityFund = 16,
}

/// <summary>El tipo de una cuenta bancaria propia (data-model §9.2). (nuevo)</summary>
public enum OwnBankAccountType
{
    Savings = 1,
    Checking = 2,
}

/// <summary>El estado de un catálogo de Tesorería (la cuenta propia) con su vigencia. (nuevo)</summary>
public enum TreasuryCatalogStatus
{
    Active = 1,
    Inactive = 2,
}

/// <summary>La causal de la norma para usar el fondo de liquidez (data-model §9.4; FR-207). (nuevo)</summary>
public enum LiquidityFundCause
{
    DepositorAttention = 1,
    DepositReduction = 2,
}

/// <summary>El estado de un uso del fondo de liquidez (data-model §9.4). (nuevo)</summary>
public enum LiquidityFundUseState
{
    PendingApproval = 1,
    Approved = 2,
    Used = 3,
    Rejected = 4,
    Expired = 5,
}

/// <summary>De dónde viene una orden de pago (data-model §10.2, §20).</summary>
public enum PaymentOrderOrigin
{
    LendingDisbursement = 1,
    DepositsPayout = 2,
    Receipts = 3,
    Manual = 4,
    SupplierInvoice = 5,
    AccountingPayable = 6,
}

/// <summary>Cómo se paga una orden (data-model §10.2, §20). No hay efectivo: el efectivo sale por la caja (FR-114, D20).</summary>
public enum PaymentForm
{
    Transfer = 1,
    Check = 2,
    ThirdPartyPayment = 3,
}

/// <summary>El estado de una orden de pago en la bandeja (data-model §19.8, §20).</summary>
public enum PaymentOrderState
{
    Received = 1,
    Pending = 2,
    Ready = 3,
    InBatch = 4,
    Approved = 5,
    Sent = 6,
    Disbursed = 7,
    Rejected = 8,
    CheckNotCashed = 9,
    Voided = 10,
    RejectedOnArrival = 11,
}

/// <summary>Cómo sale un lote: archivo al banco, digitado en el portal del banco o cheques (data-model §10.3, §20).</summary>
public enum PaymentBatchForm
{
    File = 1,
    BankPortal = 2,
    Checks = 3,
}

/// <summary>El estado de un lote de pago (data-model §10.3, §19.10, §20; DT §2.7).</summary>
public enum PaymentBatchState
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    FileGenerated = 4,
    Sent = 5,
    PartiallyConfirmed = 6,
    Confirmed = 7,
    RejectedByBank = 8,
    Voided = 9,
}

/// <summary>Las marcas antifraude de una orden en un lote (data-model §10.4; FR-210). (nuevo como enum de banderas)</summary>
[Flags]
public enum PaymentBatchItemFlags
{
    None = 0,
    RecentAccountChange = 1,
    SharedDestination = 2,
    InternalUserAccount = 4,
    ImportedAccountWithoutCertificate = 8,
}

/// <summary>El estado de una orden dentro de un lote (data-model §10.4). (nuevo)</summary>
public enum PaymentBatchItemState
{
    Included = 1,
    Removed = 2,
    Paid = 3,
    Rejected = 4,
}

/// <summary>De dónde salió la respuesta del banco a un archivo de pago (data-model §10.6). (nuevo)</summary>
public enum PaymentResponseSource
{
    BankFormat = 1,
    StatementMatch = 2,
    Manual = 3,
}

/// <summary>Lo que dice el banco de una línea de su respuesta (data-model §10.6). (nuevo)</summary>
public enum PaymentResponseOutcome
{
    Paid = 1,
    Rejected = 2,
}

/// <summary>El resultado del giro de una orden (data-model §10.7, §19.9, §20); viaja como texto en <c>ResultadoDelGiro</c> v1.</summary>
public enum DisbursementOutcome
{
    Disbursed = 1,
    Rejected = 2,
    CheckNotCashed = 3,
    Voided = 4,
}
