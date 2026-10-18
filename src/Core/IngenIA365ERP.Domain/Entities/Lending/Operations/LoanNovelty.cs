using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_LoanNovelties</c> (feature 013, K3, T0871; data-model §25.1; FR-090–FR-093, FR-098): una novedad de un crédito —prórroga, cambio
/// de fecha, cambio de tasa, novación o refinanciación— con su <b>causa</b> calculada por <c>ClasificadorDeNovedad</c> (reestructuración o
/// modificación). El ciclo (api.md §11.1):
/// <code>
/// Draft ──radicar──▶ Submitted ──decisión──▶ Approved ──aplicar──▶ Applied
///                              └──────────▶ Rejected
/// Draft · Submitted · Approved ──anular con motivo──▶ Cancelled
/// </code>
/// Se edita sólo en <c>Draft</c> (motivo, condiciones propuestas, causa y simulación); al aplicarse queda <b>congelada</b>: las columnas de la
/// aplicación se escriben una vez. No es hecho: el estado avanza. Los créditos que toca van en <see cref="LoanNoveltyLoan"/> y los
/// consentimientos de codeudores y garantes en <see cref="LoanNoveltyConsent"/>. (nuevo)
/// </summary>
public class LoanNovelty : AuditableEntity
{
    public const string CodigoNoEditable = "Lending.Novelty.NotEditable";
    public const string CodigoEstadoInvalido = "Lending.Novelty.InvalidStatus";
    public const string CodigoNoAprobada = "Lending.Novelty.NotApproved";
    public const string CodigoYaAplicada = "Lending.Novelty.AlreadyApplied";

    /// <summary>Numerador de Cartera; único.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>El crédito principal; los demás van en <see cref="LoanNoveltyLoan"/>.</summary>
    public int LoanId { get; init; }

    public NoveltyMechanism Mechanism { get; private set; }

    /// <summary>Por la causa y no por el mecanismo (FR-090).</summary>
    public NoveltyCause Cause { get; private set; }

    /// <summary>Por qué: la clasificación canónica (mora, motivo, historial reciente frente al límite de la norma).</summary>
    public string CauseDeterminationJson { get; private set; } = "{}";

    /// <summary>El acuerdo de insolvencia es una reestructuración (FR-098).</summary>
    public int? InsolvencyProceedingId { get; init; }

    public LoanNoveltyStatus Status { get; private set; } = LoanNoveltyStatus.Draft;

    public string RequestReason { get; private set; } = string.Empty;

    /// <summary><c>Lending.Novelties.Request</c>; radicar no permite decidir (FR-005).</summary>
    public int RequestedByUserId { get; init; }

    public DateTime RequestedAt { get; init; }

    /// <summary>Las condiciones nuevas (JSON).</summary>
    public string ProposedTermsJson { get; private set; } = "{}";

    /// <summary>El plan nuevo y la calificación resultante (FR-091).</summary>
    public string SimulationJson { get; private set; } = "{}";

    /// <summary><c>HuellaDeCartera</c> de la simulación; vacía hasta simular.</summary>
    public string SimulationSha256 { get; private set; } = string.Empty;

    /// <summary>Piso: un reestructurado nunca queda mejor (FR-093).</summary>
    public RatingCategory RatingAtRequest { get; init; }

    public int? RestructuringCountAfter { get; private set; }

    public int? FinalDecisionId { get; private set; }

    /// <summary>Condiciones de la aprobación.</summary>
    public string? ConditionsJson { get; private set; }

    /// <summary>Nuevo pagaré u otrosí (FR-092).</summary>
    public int? PromissoryNoteId { get; private set; }

    /// <summary>La operación <c>TermsChange</c> (bigint).</summary>
    public long? AppliedOperationId { get; private set; }

    /// <summary>Navegación para enlazar en el mismo guardado la operación todavía sin Id.</summary>
    public LoanOperation? AppliedOperation { get; private set; }

    public int? NewTermsVersionId { get; private set; }

    /// <summary>Navegación para enlazar en el mismo guardado la versión de condiciones todavía sin Id.</summary>
    public LoanTermsVersion? NewTermsVersion { get; private set; }

    public int? NewScheduleVersionId { get; private set; }

    /// <summary>Navegación para enlazar en el mismo guardado la versión del plan todavía sin Id.</summary>
    public RepaymentScheduleVersion? NewScheduleVersion { get; private set; }

    /// <summary>Novación o refinanciación: el crédito enlazado que hereda marca, conteo, calificación e historial.</summary>
    public int? ResultingLoanId { get; private set; }

    /// <summary>Navegación para enlazar en el mismo guardado el crédito enlazado todavía sin Id.</summary>
    public Loan? ResultingLoan { get; private set; }

    /// <summary><c>Lending.Novelties.Apply</c>.</summary>
    public int? AppliedByUserId { get; private set; }

    public DateTime? AppliedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public string? CancelReason { get; private set; }

    /// <summary>
    /// K4 (T0939; data-model §25.1; FR-120): el acuerdo de pago que, por cambiar las condiciones, se tramita como esta novedad
    /// (<c>Lending.PaymentAgreement.ChangesTerms</c>). Se vincula en borrador (<see cref="VincularAcuerdo"/>).
    /// </summary>
    public int? PaymentAgreementId { get; private set; }

    /// <summary>Vincula el acuerdo que originó la novedad; sólo en borrador y una vez.</summary>
    public void VincularAcuerdo(int acuerdoId)
    {
        if (Status != LoanNoveltyStatus.Draft || PaymentAgreementId is not null)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoNoEditable,
                "El acuerdo se vincula a la novedad en borrador y una sola vez.", "status", Status.ToString()));
        PaymentAgreementId = acuerdoId;
    }

    // ------------------------------------------------------------------------------------------- comportamiento (T0871) --

    private static readonly IReadOnlyDictionary<LoanNoveltyStatus, LoanNoveltyStatus[]> Transiciones =
        new Dictionary<LoanNoveltyStatus, LoanNoveltyStatus[]>
        {
            [LoanNoveltyStatus.Draft] = [LoanNoveltyStatus.Submitted, LoanNoveltyStatus.Cancelled],
            [LoanNoveltyStatus.Submitted] = [LoanNoveltyStatus.Approved, LoanNoveltyStatus.Rejected, LoanNoveltyStatus.Cancelled, LoanNoveltyStatus.Draft],
            [LoanNoveltyStatus.Approved] = [LoanNoveltyStatus.Applied, LoanNoveltyStatus.Cancelled],
        };

    /// <summary>¿Se puede pasar de <paramref name="desde"/> a <paramref name="hacia"/>? (api.md §11.1).</summary>
    public static bool PuedePasar(LoanNoveltyStatus desde, LoanNoveltyStatus hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    /// <summary>Se edita sólo en borrador.</summary>
    public bool Editable => Status == LoanNoveltyStatus.Draft;

    /// <summary>Ya decidida (aprobada, rechazada, aplicada o anulada): los consentimientos y sus adjuntos quedan bloqueados.</summary>
    public bool Decidida => Status is LoanNoveltyStatus.Approved or LoanNoveltyStatus.Rejected or LoanNoveltyStatus.Applied or LoanNoveltyStatus.Cancelled;

    /// <summary>¿Crea un crédito enlazado? Novación y refinanciación; las demás conservan el número del crédito (FR-090).</summary>
    public bool CreaCreditoEnlazado => Mechanism is NoveltyMechanism.Novation or NoveltyMechanism.Refinancing;

    /// <summary>Crea la novedad en borrador con su causa calculada.</summary>
    public static LoanNovelty Nueva(string numero, int creditoId, NoveltyMechanism mecanismo, NoveltyCause causa, string determinacionJson,
        string motivo, string condicionesJson, RatingCategory calificacionAlPedir, int usuarioId, DateTime instante, int? procesoDeInsolvenciaId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentException.ThrowIfNullOrWhiteSpace(determinacionJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(condicionesJson);
        if (procesoDeInsolvenciaId is not null && causa != NoveltyCause.Restructuring)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido,
                "El acuerdo de insolvencia es una reestructuración (FR-098).", "cause", causa.ToString()));
        return new LoanNovelty
        {
            Number = numero,
            LoanId = creditoId,
            Mechanism = mecanismo,
            Cause = causa,
            CauseDeterminationJson = determinacionJson,
            RequestReason = motivo.Trim(),
            ProposedTermsJson = condicionesJson,
            RatingAtRequest = calificacionAlPedir,
            RequestedByUserId = usuarioId,
            RequestedAt = instante,
            InsolvencyProceedingId = procesoDeInsolvenciaId,
        };
    }

    /// <summary>Nulo si se puede editar; si no, <see cref="CodigoNoEditable"/> con el estado.</summary>
    public ProblemaDeCartera? ExigirEditable() => Editable
        ? null
        : ProblemaDeCartera.Con(CodigoNoEditable, "La novedad ya no se puede modificar: sólo se edita en borrador.", "status", Status.ToString());

    /// <summary>Nulo si puede ir a <paramref name="hacia"/>; si no, el código del caso (aplicada, no aprobada o estado inválido).</summary>
    public ProblemaDeCartera? PuedeIrA(LoanNoveltyStatus hacia)
    {
        if (PuedePasar(Status, hacia)) return null;
        if (Status == LoanNoveltyStatus.Applied)
            return ProblemaDeCartera.Con(CodigoYaAplicada, "La novedad ya se aplicó: no se modifica ni se anula.", "status", Status.ToString());
        if (hacia == LoanNoveltyStatus.Applied)
            return ProblemaDeCartera.Con(CodigoNoAprobada, "Sólo se aplica una novedad aprobada.", "status", Status.ToString());
        return new ProblemaDeCartera(CodigoEstadoInvalido, $"La novedad está en «{Status}» y no puede pasar a «{hacia}».",
            new Dictionary<string, object?> { ["status"] = Status.ToString(), ["requested"] = hacia.ToString() });
    }

    /// <summary>Cambia el motivo, las condiciones propuestas y, con ellas, el mecanismo y la causa recalculada; borra la simulación anterior.</summary>
    public void Editar(NoveltyMechanism mecanismo, NoveltyCause causa, string determinacionJson, string motivo, string condicionesJson)
    {
        ReglaDeCarteraException.SiHay(ExigirEditable());
        ArgumentException.ThrowIfNullOrWhiteSpace(determinacionJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentException.ThrowIfNullOrWhiteSpace(condicionesJson);
        if (InsolvencyProceedingId is not null && causa != NoveltyCause.Restructuring)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido,
                "El acuerdo de insolvencia es una reestructuración (FR-098).", "cause", causa.ToString()));
        Mechanism = mecanismo;
        Cause = causa;
        CauseDeterminationJson = determinacionJson;
        RequestReason = motivo.Trim();
        ProposedTermsJson = condicionesJson;
        SimulationJson = "{}";
        SimulationSha256 = string.Empty;
    }

    /// <summary>Guarda la simulación (plan nuevo y calificación) con su huella; sólo en borrador.</summary>
    public void RegistrarSimulacion(string simulacionJson, string huella)
    {
        ReglaDeCarteraException.SiHay(ExigirEditable());
        ArgumentException.ThrowIfNullOrWhiteSpace(simulacionJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);
        SimulationJson = simulacionJson;
        SimulationSha256 = huella;
    }

    /// <summary><c>Draft</c> → <c>Submitted</c>: se lleva a decisión con la simulación hecha.</summary>
    public void Radicar()
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(LoanNoveltyStatus.Submitted));
        if (string.IsNullOrEmpty(SimulationSha256))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido,
                "Antes de radicarla hay que simular la novedad: el plan nuevo y la calificación resultante.", "simulation", null));
        Status = LoanNoveltyStatus.Submitted;
    }

    /// <summary><c>Submitted</c> → <c>Approved</c> con la decisión final y sus condiciones.</summary>
    public void Aprobar(int decisionId, string? condicionesJson)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(LoanNoveltyStatus.Approved));
        Status = LoanNoveltyStatus.Approved;
        FinalDecisionId = decisionId;
        ConditionsJson = condicionesJson;
    }

    /// <summary><c>Submitted</c> → <c>Rejected</c> con la decisión final.</summary>
    public void Rechazar(int decisionId)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(LoanNoveltyStatus.Rejected));
        Status = LoanNoveltyStatus.Rejected;
        FinalDecisionId = decisionId;
    }

    /// <summary>
    /// K3 (T0879): <c>Submitted</c> → <c>Draft</c> con la decisión <c>Return</c> (api.md §4.3: «vuelve a borrador para corregir»): la simulación se
    /// borra y hay que volver a simular y radicar.
    /// </summary>
    public void Devolver()
    {
        if (Status != LoanNoveltyStatus.Submitted)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido, "Sólo se devuelve una novedad radicada.", "status", Status.ToString()));
        Status = LoanNoveltyStatus.Draft;
        SimulationJson = "{}";
        SimulationSha256 = string.Empty;
    }

    /// <summary>El nuevo pagaré u otrosí (FR-092), antes de aplicar.</summary>
    public void AnotarPagare(int pagareId)
    {
        if (Status is not (LoanNoveltyStatus.Draft or LoanNoveltyStatus.Submitted or LoanNoveltyStatus.Approved))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoNoEditable,
                "El pagaré u otrosí se anota antes de aplicar la novedad.", "status", Status.ToString()));
        PromissoryNoteId = pagareId;
    }

    /// <summary>
    /// <c>Approved</c> → <c>Applied</c>: la operación <c>TermsChange</c>, las versiones de condiciones y de plan que creó (o el crédito
    /// enlazado de una novación o refinanciación) y el conteo de reestructuraciones resultante. Desde aquí queda congelada.
    /// </summary>
    public void Aplicar(LoanOperation operacion, LoanTermsVersion? condiciones, RepaymentScheduleVersion? plan, Loan? enlazado,
        int? reestructuracionesDespues, int usuarioId, DateTime instante)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        ReglaDeCarteraException.SiHay(PuedeIrA(LoanNoveltyStatus.Applied));
        if (operacion.Kind != LoanOperationKind.TermsChange)
            throw new ArgumentException("Una novedad se aplica con una operación TermsChange.", nameof(operacion));
        if (CreaCreditoEnlazado && enlazado is null)
            throw new ArgumentException("Una novación o refinanciación crea el crédito enlazado.", nameof(enlazado));
        if (!CreaCreditoEnlazado && (condiciones is null || plan is null))
            throw new ArgumentException("Una prórroga, un cambio de fecha o de tasa crea la versión de condiciones y la del plan.", nameof(plan));
        if (Cause == NoveltyCause.Restructuring && reestructuracionesDespues is null)
            throw new ArgumentException("Una reestructuración deja el conteo de reestructuraciones.", nameof(reestructuracionesDespues));
        Status = LoanNoveltyStatus.Applied;
        AppliedOperation = operacion;
        if (operacion.Id > 0) AppliedOperationId = operacion.Id;
        NewTermsVersion = condiciones;
        if (condiciones is { Id: > 0 }) NewTermsVersionId = condiciones.Id;
        NewScheduleVersion = plan;
        if (plan is { Id: > 0 }) NewScheduleVersionId = plan.Id;
        ResultingLoan = enlazado;
        if (enlazado is { Id: > 0 }) ResultingLoanId = enlazado.Id;
        RestructuringCountAfter = reestructuracionesDespues;
        AppliedByUserId = usuarioId;
        AppliedAt = instante;
    }

    /// <summary>Anular con motivo, antes de aplicar (<c>Lending.Novelty.AlreadyApplied</c> después).</summary>
    public void Cancelar(DateTime instante, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ReglaDeCarteraException.SiHay(PuedeIrA(LoanNoveltyStatus.Cancelled));
        Status = LoanNoveltyStatus.Cancelled;
        CancelledAt = instante;
        CancelReason = motivo.Trim();
    }
}
