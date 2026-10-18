namespace IngenIA365ERP.Application.Common.Integration.Lending;

// Enums de los puertos que Cartera ofrece (feature 013, T0149; contracts/interfaces.md §3.1, data-model §9.4).
// Tipo en español, valores en inglés idénticos a los del motor cuando representan lo mismo (CH40). Viajan en proceso;
// donde se guardan, se guardan como texto. Los números los fija data-model §9.4 y los vigila
// LosEnumsDelContratoNumeranComoElDominio.

/// <summary>Quién recauda (FR-087, interfaces §3.2). (nuevo)</summary>
public enum ClaseDeRecaudador
{
    Receipts = 1,
    Payroll = 2,
    Contributions = 3,
    OwnLoanPayoff = 4,
    Test = 5,

    /// <summary>Feature 014 (T014; research C13, E04): Tesorería, para las transferencias que recibe a cuentas y créditos.</summary>
    Treasury = 6,
}

/// <summary>
/// La clase del medio con que se recaudó (FR-082, interfaces §3.14): los valores de <c>PaymentMeansClass</c> de la 012 más
/// tres propios de Cartera. Las clases de crédito (<see cref="AssociateCredit"/>, <see cref="CustomerCredit"/>) se declaran y el
/// contrato las rechaza (C30). (nuevo)
/// </summary>
public enum ClaseDeMedioDeRecaudo
{
    Cash = 1,
    CreditCard = 2,
    DebitCard = 3,
    AssociateCredit = 4,
    CustomerCredit = 5,
    BankDeposit = 6,
    Transfer = 7,
    Voucher = 8,
    Check = 9,
    OwnPayrollDeduction = 20,
    ExternalPaymasterDeduction = 21,
    InternalOffset = 22,
    Other = 99,
}

/// <summary>Modalidad de un pago (equivale a <c>PaymentModality</c> del motor). (nuevo)</summary>
public enum ModalidadDePago
{
    FullPayoff = 1,
    PastDue = 2,
    CycleInstallment = 3,
    Advance = 4,
    ExtraordinaryPrincipal = 5,
    SpecificInstallment = 6,
    FreeAmount = 7,
}

/// <summary>Elección del deudor en un abono extraordinario (equivale a <c>PrepaymentChoice</c>). (nuevo)</summary>
public enum EleccionDeAbono
{
    ReduceTerm = 1,
    ReduceInstallment = 2,
}

/// <summary>Forma de aplicar un pago (FR-073). (nuevo)</summary>
public enum FormaDeAplicacion
{
    Interactive = 1,
    NonInteractive = 2,
}

/// <summary>
/// Por qué una cuota de la nómina propia no se descontó (FR-112, FR-115; C56 suma <see cref="DeductionPriority"/>). Mismos nombres y
/// números que <c>NonDeductionReason</c> del dominio. Se guarda como texto en <c>PAY_PayrollLoanDeductions.NotDeductedReason</c>. (nuevo)
/// </summary>
public enum MotivoDeNoDescuento
{
    DeductionCap = 1,
    InsufficientNet = 2,
    Termination = 3,
    Leave = 4,
    Disability = 5,
    DeductionPriority = 6,
    Other = 99,
}

/// <summary>Resultado de cada ítem del contrato (FR-086; <see cref="Answered"/> = consulta atendida, C28). (nuevo)</summary>
public enum ResultadoDelContrato
{
    Applied = 1,
    AlreadyApplied = 2,
    Rejected = 3,
    Answered = 4,
}

/// <summary>Si Cartera está activada en la cooperativa (FR-142). (nuevo)</summary>
public enum EstadoDeCartera
{
    NotActivated = 1,
    Active = 2,
}

/// <summary>Estado de una obligación (equivale a <c>LoanStatus</c>). (nuevo)</summary>
public enum EstadoDeObligacion
{
    PendingDisbursement = 1,
    Active = 2,
    PaidOff = 3,
    WrittenOff = 4,
    Voided = 5,
    Novated = 6,
}

/// <summary>Concepto de saldo de una obligación (equivale a <c>LoanConcept</c>). (nuevo)</summary>
public enum ConceptoDeObligacion
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

/// <summary>Papel de la persona en la obligación. (nuevo)</summary>
public enum RolEnLaObligacion
{
    Debtor = 1,
    CoDebtor = 2,
}

/// <summary>Cómo se resuelve un saldo a favor o un anticipo (FR-083, interfaces §3.9). (nuevo)</summary>
public enum FormaDeResolucion
{
    RefundThroughCollector = 1,
    ApplyToOtherObligation = 2,
    ApplyAsExtraordinaryPrincipal = 3,
}

/// <summary>Clase de la obligación para quien la consulta: corriente o libranza (interfaces §6.1). (nuevo)</summary>
public enum ClaseDeObligacion
{
    Standard = 1,
    PayrollDeduction = 2,
}
