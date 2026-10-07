namespace IngenIA365ERP.Domain.Enums.Lending;

// Crédito, libro, operaciones y numeración de Cartera (feature 013, T0201; decisiones-transversales §2.7; data-model §9.1 y
// parte 1 §10, C65). Se guardan por su número; un valor nunca se renumera (EnumeracionesDeCarteraTests).

/// <summary>Concepto de saldo de un crédito (M). <c>Advance</c> y <c>CreditBalance</c> son pasivos con el deudor.</summary>
public enum LoanConcept
{
    Principal = 1,
    CurrentInterest = 2,
    DefaultInterest = 3,
    CurrentInterestInSuspense = 4,
    DefaultInterestInSuspense = 5,
    Insurance = 6,
    CollectionFee = 7,
    OtherCharge = 8,
    Advance = 9,
    CreditBalance = 10,
}

/// <summary>Clase de una fila del libro (<c>LND_LoanTransactions</c>, M).</summary>
public enum LoanTransactionKind
{
    Disbursement = 1,
    OpeningBalance = 2,
    Accrual = 3,
    AccrualAdjustment = 4,
    SuspenseReclassification = 5,
    PaymentAllocation = 6,
    PaymentReallocation = 7,
    AdvanceReceived = 8,
    AdvanceApplied = 9,
    CreditBalanceRecognized = 10,
    CreditBalanceSettled = 11,
    Charge = 12,
    Waiver = 13,
    CommercialAdjustment = 14,
    WriteOff = 15,
    Recovery = 16,
}

/// <summary>Clase de una operación (<c>LND_LoanOperations</c>, M, FR-048, FR-142).</summary>
public enum LoanOperationKind
{
    Disbursement = 1,
    OpeningBalance = 2,
    Payment = 3,
    PaymentReversal = 4,
    DisbursementReversal = 5,
    RateReset = 6,
    TermsChange = 7,
    Charge = 8,
    Waiver = 9,
    CommercialAdjustment = 10,
    WriteOff = 11,
    PreferenceChange = 12,
    CreditBalanceResolution = 13,
    ProvisionalCollection = 14,
}

/// <summary>Estado del crédito (spec, Key Entities; data-model §8.1).</summary>
public enum LoanStatus
{
    PendingDisbursement = 1,
    Active = 2,
    PaidOff = 3,
    WrittenOff = 4,
    Voided = 5,
    Novated = 6,
}

/// <summary>Por qué camino nació el crédito.</summary>
public enum LoanOrigin
{
    Origination = 1,
    Commercial = 2,
    Migration = 3,
}

/// <summary>Estado de la obligación de una venta que Cartera responde a Comercial (estados de <c>ObligacionCreada</c>, T18).</summary>
public enum SaleObligationStatus
{
    Validated = 1,
    ValidationFailed = 2,
    Migrated = 3,
    CancelledBeforeCutoff = 4,
}

/// <summary>Vínculo de un crédito con su origen (<c>LND_Loans.OriginLinkKind</c>): reemplazo de Comercial (K1), novación y refinanciación (K3).</summary>
public enum LoanLinkKind
{
    Replacement = 1,
    Novation = 2,
    Refinancing = 3,
}

/// <summary>
/// Causa de una versión de condiciones (<c>LND_LoanTermsVersions.Cause</c>). 5 a 10 nacen con K3. El traslado de pagaduría y el
/// cambio de forma de pago no son versión de condiciones: son un vínculo nuevo en <c>LND_LoanPayrollLinks</c> (C44).
/// </summary>
public enum TermsVersionCause
{
    Origination = 1,
    CommercialSale = 2,
    Migration = 3,
    RateReset = 4,
    Restructuring = 5,
    Modification = 6,
    Extension = 7,
    PaymentDateChange = 8,
    RateChange = 9,
    Novation = 10,
}

/// <summary>Forma de un tramo de desembolso; <c>InKind</c> es la venta de Comercial y nunca emite orden de giro.</summary>
public enum DisbursementForm
{
    Transfer = 1,
    Check = 2,
    ThirdPartyPayment = 3,
    InKind = 4,
}

/// <summary>Estado de una orden de giro (FR-045; data-model §8.4).</summary>
public enum DisbursementOrderStatus
{
    Issued = 1,
    Disbursed = 2,
    Rejected = 3,
    CheckNotCashed = 4,
    Voided = 5,
}

/// <summary>Estado de una reserva de número de Comercial (PM-10, T22).</summary>
public enum LoanNumberReservationStatus
{
    Reserved = 1,
    Used = 2,
    Released = 3,
    Expired = 4,
}

/// <summary>Qué numera una fila de <c>LND_NumberSequences</c> (C48). <c>PayrollDeductionFile</c> es de K2 y <c>RegulatoryFile</c> de K5.</summary>
public enum LendingNumberKind
{
    Loan = 1,
    Operation = 2,
    Application = 3,
    DisbursementOrder = 4,
    DisbursementFile = 5,
    SessionMinutes = 6,
    IssuedDocument = 7,
    PayrollDeductionFile = 8,
    RegulatoryFile = 9,
}
