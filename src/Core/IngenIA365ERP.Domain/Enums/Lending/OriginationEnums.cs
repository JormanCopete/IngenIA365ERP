namespace IngenIA365ERP.Domain.Enums.Lending;

// Configuración de líneas y originación (feature 013, T0201; data-model parte 1 §1, §2 y §10, C65).

/// <summary>Estado de una línea de crédito (<c>LND_CreditLines.Status</c>).</summary>
public enum CreditLineStatus
{
    Draft = 1,
    Active = 2,
    Closed = 3,
}

/// <summary>Por qué se cerró una línea a créditos nuevos.</summary>
public enum CreditLineClosedReason
{
    Manual = 1,
    ModalityRetired = 2,
}

/// <summary>Tipo de deudor: asociado o cliente. «Empleado» no es tipo: es <c>ForEmployees</c> de la versión (C96).</summary>
public enum DebtorKind
{
    Associate = 1,
    Customer = 2,
}

/// <summary>Qué traduce una equivalencia de Comercial (<c>LND_CommercialTermEquivalences.Kind</c>).</summary>
public enum CommercialTermKind
{
    Periodicity = 1,
    TermUnit = 2,
}

/// <summary>Solicitud de crédito o de cupo rotativo (FR-049).</summary>
public enum ApplicationKind
{
    Loan = 1,
    RevolvingLimit = 2,
}

/// <summary>
/// Estado de la solicitud (data-model §8.2). «Aprobación vencida» y «bloqueada por listas» no son estados: se calculan al leer
/// (C53).
/// </summary>
public enum CreditApplicationStatus
{
    Draft = 1,
    Submitted = 2,
    PendingDecision = 3,
    Returned = 4,
    Approved = 5,
    Denied = 6,
    Disbursed = 7,
    Granted = 8,
    Cancelled = 9,
}

/// <summary>Papel de una persona en la solicitud o en el crédito.</summary>
public enum CreditPartyRole
{
    Applicant = 1,
    CoDebtor = 2,
    Guarantor = 3,
}

/// <summary>Cada validación de FR-036 (<c>LND_CreditApplicationChecks.Kind</c>).</summary>
public enum ApplicationCheckKind
{
    RestrictiveLists = 1,
    CreditBureau = 2,
    PaymentCapacity = 3,
    DeductionCapacity = 4,
    ConsolidatedExposure = 5,
    CreditLimit = 6,
    Concentration = 7,
    Usury = 8,
    LineRequirements = 9,
    DataAuthorization = 10,
    Scoring = 11,
    RelatedParty = 12,
    Documents = 13,
}

/// <summary>Momento de una validación.</summary>
public enum CheckStage
{
    AtSubmission = 1,
    BeforeDecision = 2,
    BeforeDisbursement = 3,
}

/// <summary>Resultado de una validación.</summary>
public enum CheckOutcome
{
    Passed = 1,
    Warning = 2,
    Blocking = 3,
    NotApplicable = 4,
    Unavailable = 5,
}

/// <summary>En qué contexto se consultaron listas restrictivas (T70, C90).</summary>
public enum ScreeningContext
{
    Application = 1,
    Sale = 2,
    CreditLimit = 3,
    ListRescan = 4,

    /// <summary>Feature 014, US2 (T073; FR-032): la apertura de una cuenta de captación o de aportes (titular y cotitulares).</summary>
    AccountOpening = 5,
}

/// <summary>Resultado de una consulta a listas restrictivas.</summary>
public enum ScreeningOutcome
{
    NoMatch = 1,
    PossibleMatch = 2,
    Match = 3,
    Unavailable = 4,
}

/// <summary>Para qué se consultó a centrales (T73).</summary>
public enum BureauQueryPurpose
{
    Application = 1,
    CreditLimit = 2,
}

/// <summary>Clase de garantía (FR-043). <c>LND_Collaterals</c> sólo guarda <c>Pledge</c>, <c>Mortgage</c>, <c>GuaranteeFund</c> y <c>Contributions</c>.</summary>
public enum CollateralKind
{
    PromissoryNote = 1,
    CoDebtor = 2,
    Pledge = 3,
    Mortgage = 4,
    GuaranteeFund = 5,
    Contributions = 6,
}

/// <summary>Estado de una garantía (FR-043).</summary>
public enum CollateralStatus
{
    Constituted = 1,
    Valid = 2,
    Released = 3,
    Extinguished = 4,
    Executed = 5,
}

/// <summary>Pagaré físico o desmaterializado.</summary>
public enum PromissoryNoteKind
{
    Physical = 1,
    Dematerialized = 2,
}

/// <summary>Estado del pagaré (F-19).</summary>
public enum PromissoryNoteStatus
{
    Created = 1,
    PendingSignature = 2,
    Signed = 3,
    InCustody = 4,
    Endorsed = 5,
    Voided = 6,
}

/// <summary>Clase de cupo (FR-029, FR-049).</summary>
public enum CreditLimitKind
{
    Consolidated = 1,
    ByLine = 2,
    Revolving = 3,
}

/// <summary>De dónde salió un cupo.</summary>
public enum CreditLimitOrigin
{
    Calculated = 1,
    Assigned = 2,
    Migration = 3,
}

/// <summary>Motivo de un bloqueo del deudor (FR-052, FR-060, FR-097, FR-142).</summary>
public enum DebtorBlockReason
{
    Manual = 1,
    Arrears = 2,
    ValidationFailed = 3,
    WrittenOff = 4,
    RestrictiveListMatch = 5,
    AssociateWithdrawal = 6,
    Deceased = 7,
    Insolvency = 8,
    Garnishment = 9,
    LimitRevalidation = 10,
}

/// <summary>De dónde salió una preferencia de abono a capital (D24).</summary>
public enum PreferenceOrigin
{
    Application = 1,
    CreditLimitGrant = 2,
    DebtorRequest = 3,
    Migration = 4,
    InheritedFromLimit = 5,
}
