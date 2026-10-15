using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

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

    /// <summary>K2 (<c>CarteraLibranza</c>; FR-109): el convenio de la pagaduría externa en una solicitud por libranza.</summary>
    public int? PayrollAgreementId { get; set; }

    /// <summary>K2: la identificación del empleado ante la pagaduría (pasa al vínculo como <c>EmployeeIdAtPaymaster</c>).</summary>
    public string? PayrollEmployeeIdentifier { get; set; }

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

    // ------------------------------------------------------------------------------------------- comportamiento (T0388) --
    // US2 (T0388; data-model §2.1 y §8.2): los estados y sus transiciones. Las propiedades conservan su setter (la forma la dejó la fase 3 y
    // la migración la llena), pero la originación sólo las mueve por estos métodos, que dicen si se puede antes de mover.

    public const string CodigoNoEditable = "Lending.Application.NotEditable";
    public const string CodigoEstadoInvalido = "Lending.Application.InvalidStatus";
    public const string CodigoAprobacionVencida = "Lending.Application.ApprovalExpired";

    /// <summary>Las transiciones de data-model §8.2. «Vencida» y «bloqueada por listas» no son estados (C53).</summary>
    private static readonly IReadOnlyDictionary<CreditApplicationStatus, CreditApplicationStatus[]> Transiciones =
        new Dictionary<CreditApplicationStatus, CreditApplicationStatus[]>
        {
            [CreditApplicationStatus.Draft] = [CreditApplicationStatus.Submitted, CreditApplicationStatus.Cancelled],
            [CreditApplicationStatus.Submitted] = [CreditApplicationStatus.PendingDecision, CreditApplicationStatus.Cancelled],
            [CreditApplicationStatus.PendingDecision] =
            [
                CreditApplicationStatus.PendingDecision, CreditApplicationStatus.Approved, CreditApplicationStatus.Denied,
                CreditApplicationStatus.Returned, CreditApplicationStatus.Cancelled,
            ],
            [CreditApplicationStatus.Returned] = [CreditApplicationStatus.Submitted, CreditApplicationStatus.Cancelled],
            [CreditApplicationStatus.Approved] =
            [
                CreditApplicationStatus.PendingDecision, CreditApplicationStatus.Disbursed, CreditApplicationStatus.Granted,
                CreditApplicationStatus.Cancelled,
            ],
        };

    /// <summary>¿Se puede pasar de <paramref name="desde"/> a <paramref name="hacia"/>? (data-model §8.2).</summary>
    public static bool PuedePasar(CreditApplicationStatus desde, CreditApplicationStatus hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    /// <summary>Se edita (datos, partes y documentos propios del borrador) sólo en <c>Draft</c> y <c>Returned</c>.</summary>
    public bool Editable => Status is CreditApplicationStatus.Draft or CreditApplicationStatus.Returned;

    /// <summary>
    /// Ya decidida (o terminada): aprobada, negada, desembolsada, otorgada o anulada. Desde ahí los documentos quedan bloqueados (T81,
    /// <c>Attachments.OwnerLocked</c>).
    /// </summary>
    public bool Decidida => Status is CreditApplicationStatus.Approved or CreditApplicationStatus.Denied or CreditApplicationStatus.Disbursed
        or CreditApplicationStatus.Granted or CreditApplicationStatus.Cancelled;

    /// <summary>«Aprobación vencida» (FR-042, C53): aprobada con <see cref="ApprovalValidUntil"/> antes de <paramref name="hoy"/>.</summary>
    public bool AprobacionVencida(DateOnly hoy) => Status == CreditApplicationStatus.Approved && ApprovalValidUntil is { } hasta && hasta < hoy;

    /// <summary>Nulo si se puede editar; si no, <c>Lending.Application.NotEditable</c> con el estado.</summary>
    public ProblemaDeCartera? ExigirEditable() => Editable
        ? null
        : ProblemaDeCartera.Con(CodigoNoEditable,
            "La solicitud ya no se puede modificar: sólo se edita en borrador o cuando se devolvió para corregir.", "status", Status.ToString());

    /// <summary>Nulo si la solicitud puede ir a <paramref name="hacia"/>; si no, <c>Lending.Application.InvalidStatus</c>.</summary>
    public ProblemaDeCartera? PuedeIrA(CreditApplicationStatus hacia) => PuedePasar(Status, hacia)
        ? null
        : new ProblemaDeCartera(CodigoEstadoInvalido,
            $"La solicitud está en «{Status}» y no puede pasar a «{hacia}».",
            new Dictionary<string, object?> { ["status"] = Status.ToString(), ["requested"] = hacia.ToString() });

    /// <summary><c>Draft</c>/<c>Returned</c> → <c>Submitted</c>: quien radica y cuándo (FR-005: no decide ni vota).</summary>
    public void Radicar(int usuarioId, DateTime instante)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(CreditApplicationStatus.Submitted));
        Status = CreditApplicationStatus.Submitted;
        SubmittedByUserId = usuarioId;
        SubmittedAt = instante;
    }

    /// <summary>
    /// <c>Submitted</c> → <c>PendingDecision</c>, o el reenrutamiento de una pendiente o de una aprobada (cambió el consolidado o venció):
    /// sella la matriz, el nivel efectivo, la huella del enrutamiento, el consolidado y si hay un vinculado (T63, FR-040). Una aprobada que
    /// se reenruta pierde su aprobación: hay que decidir otra vez.
    /// </summary>
    public void Enrutar(int matrizId, int nivelId, string huella, decimal consolidado, bool vinculado, DateTime instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);
        ReglaDeCarteraException.SiHay(PuedeIrA(CreditApplicationStatus.PendingDecision));
        if (Status == CreditApplicationStatus.Approved)
        {
            ApprovalDecisionId = null;
            ApprovalValidUntil = null;
            ApprovedAmount = null;
            ApprovedTerms = null;
        }
        Status = CreditApplicationStatus.PendingDecision;
        AttributionMatrixId = matrizId;
        RequiredLevelId = nivelId;
        RoutingSha256 = huella;
        ConsolidatedExposure = consolidado;
        IsRelatedParty = vinculado;
        RoutedAt = instante;
    }

    /// <summary><c>PendingDecision</c> → <c>Approved</c> con las condiciones aprobadas (pueden ser menores, FR-038) y su vigencia (FR-042).</summary>
    public void Aprobar(int decisionId, decimal montoAprobado, string? condiciones, DateOnly vigenteHasta)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(CreditApplicationStatus.Approved));
        if (montoAprobado <= 0m || montoAprobado > RequestedAmount)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido,
                "El monto aprobado es mayor que cero y no pasa del pedido.", "approvedAmount", montoAprobado));
        Status = CreditApplicationStatus.Approved;
        ApprovalDecisionId = decisionId;
        ApprovedAmount = montoAprobado;
        ApprovedTerms = condiciones;
        ApprovalValidUntil = vigenteHasta;
    }

    /// <summary><c>PendingDecision</c> → <c>Denied</c>.</summary>
    public void Negar()
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(CreditApplicationStatus.Denied));
        Status = CreditApplicationStatus.Denied;
    }

    /// <summary><c>PendingDecision</c> → <c>Returned</c>: vuelve a quien la armó para corregirla; al reenviarla se radica otra vez.</summary>
    public void Devolver()
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(CreditApplicationStatus.Returned));
        Status = CreditApplicationStatus.Returned;
    }

    /// <summary>Anular o desistir con motivo, antes de desembolsar u otorgar (FR-047): no genera cartera.</summary>
    public void Cancelar(int usuarioId, DateTime instante, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ReglaDeCarteraException.SiHay(PuedeIrA(CreditApplicationStatus.Cancelled));
        Status = CreditApplicationStatus.Cancelled;
        CancelledByUserId = usuarioId;
        CancelledAt = instante;
        CancelReason = motivo.Trim();
    }

    /// <summary><c>Approved</c> → <c>Disbursed</c>: el primer tramo creó el crédito (US3).</summary>
    public void Desembolsar(int creditoId)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(CreditApplicationStatus.Disbursed));
        if (Kind != ApplicationKind.Loan)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido, "Una solicitud de cupo no se desembolsa: se otorga.", "kind", Kind.ToString()));
        Status = CreditApplicationStatus.Disbursed;
        LoanId = creditoId;
    }

    /// <summary><c>Approved</c> → <c>Granted</c>: el cupo rotativo quedó otorgado (FR-049).</summary>
    public void Otorgar(int cupoId)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(CreditApplicationStatus.Granted));
        if (Kind != ApplicationKind.RevolvingLimit)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido, "Una solicitud de crédito no se otorga: se desembolsa.", "kind", Kind.ToString()));
        Status = CreditApplicationStatus.Granted;
        CreditLimitId = cupoId;
    }
}
