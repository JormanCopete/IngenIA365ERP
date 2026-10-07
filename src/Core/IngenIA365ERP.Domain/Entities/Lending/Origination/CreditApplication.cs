using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplications</c> (feature 013; data-model §2.1; FR-034–FR-042, FR-047, FR-049): la solicitud de un crédito o de un
/// cupo rotativo, desde el borrador hasta el desembolso, el otorgamiento o la anulación. No es hecho: se edita en <c>Draft</c> y
/// <c>Returned</c>. «Aprobación vencida» y «bloqueada por listas» no son estados: se calculan al leer (C53). La libranza de
/// pagaduría externa (<c>PayrollAgreementId</c>, <c>PayrollEmployeeIdentifier</c>) nace con K2. Forma sin comportamiento.
/// </summary>
public class CreditApplication : AuditableEntity
{
    /// <summary>Radicado; lo asigna el numerador de Cartera al enviar a análisis. Nulo en borrador.</summary>
    public string? Number { get; set; }

    public ApplicationKind Kind { get; set; } = ApplicationKind.Loan;

    public int ApplicantPersonId { get; set; }

    /// <summary>Asociado o cliente al radicar.</summary>
    public DebtorKind DebtorKind { get; set; }

    public int BranchId { get; set; }

    public int CreditLineId { get; set; }

    /// <summary>La versión vigente al radicar; se revalida al decidir y al desembolsar (FR-042).</summary>
    public int CreditLineVersionId { get; set; }

    /// <summary>En <see cref="ApplicationKind.RevolvingLimit"/>, el cupo pedido.</summary>
    public decimal RequestedAmount { get; set; }

    /// <summary>Nulo en <see cref="ApplicationKind.RevolvingLimit"/>.</summary>
    public int? TermMonths { get; set; }

    /// <summary>Nulo en <see cref="ApplicationKind.RevolvingLimit"/>.</summary>
    public int? Installments { get; set; }

    public RepaymentPeriodicity? Periodicity { get; set; }

    public AmortizationSystem? AmortizationSystem { get; set; }

    public DateOnly? FirstDueDate { get; set; }

    public DateOnly? ExpectedDisbursementDate { get; set; }

    public GraceKind? GraceKind { get; set; }

    public int? GraceInstallments { get; set; }

    /// <summary>JSON: meses y valor o fracción de las cuotas extraordinarias (FR-023).</summary>
    public string? ExtraordinaryInstallments { get; set; }

    /// <summary>JSON con los códigos de los seguros opcionales elegidos (§1.6).</summary>
    public string? SelectedInsurances { get; set; }

    /// <summary>Huella de la simulación que se mostró al radicar.</summary>
    public string? SimulationSha256 { get; set; }

    /// <summary>Destino del crédito.</summary>
    public string? Purpose { get; set; }

    /// <summary>Debe estar en la línea; <c>OwnPayroll</c> exige que la persona sea empleado en el maestro.</summary>
    public RepaymentChannel RepaymentChannel { get; set; }

    /// <summary>Preferencia para abonos a capital (FR-035); al enviar nace su fila en <c>LND_PrepaymentPreferences</c>.</summary>
    public PrepaymentChoice? PrepaymentChoice { get; set; }

    /// <summary>JSON con los créditos propios a recoger (<c>loanPublicId</c>, total o parcial, valor; FR-046).</summary>
    public string? PayoffRequest { get; set; }

    /// <summary>Excepción a la política pedida (FR-038).</summary>
    public bool PolicyExceptionRequested { get; set; }

    public string? PolicyExceptionReason { get; set; }

    public CreditApplicationStatus Status { get; set; } = CreditApplicationStatus.Draft;

    /// <summary>Quien la armó (segregación).</summary>
    public int CreatedByUserId { get; set; }

    public DateTime? SubmittedAt { get; set; }

    /// <summary>Quien radicó: no decide ni vota (FR-005).</summary>
    public int? SubmittedByUserId { get; set; }

    /// <summary>Matriz de atribuciones sellada al enrutar (T63).</summary>
    public int? AttributionMatrixId { get; set; }

    /// <summary>Nivel efectivo de atribución al enrutar (T63).</summary>
    public int? RequiredLevelId { get; set; }

    public DateTime? RoutedAt { get; set; }

    /// <summary>Huella de lo que decidió el enrutamiento; si cambia antes de decidir, se reenruta.</summary>
    public string? RoutingSha256 { get; set; }

    /// <summary>Endeudamiento consolidado con la entidad, como deudor y codeudor, al enrutar.</summary>
    public decimal? ConsolidatedExposure { get; set; }

    /// <summary>Solicitante o codeudor vinculado al enrutar (FR-040).</summary>
    public bool IsRelatedParty { get; set; }

    public decimal? ApprovedAmount { get; set; }

    /// <summary>JSON canónico con las condiciones aprobadas (pueden ser menores, FR-038).</summary>
    public string? ApprovedTerms { get; set; }

    /// <summary>La decisión que aprobó.</summary>
    public int? ApprovalDecisionId { get; set; }

    /// <summary>Fecha de la decisión + <c>Originacion.DiasVigenciaDeAprobacion</c> (FR-042).</summary>
    public DateOnly? ApprovalValidUntil { get; set; }

    /// <summary>El crédito que nació (primer tramo).</summary>
    public int? LoanId { get; set; }

    /// <summary>El cupo rotativo otorgado.</summary>
    public int? CreditLimitId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public int? CancelledByUserId { get; set; }

    public string? CancelReason { get; set; }
}
