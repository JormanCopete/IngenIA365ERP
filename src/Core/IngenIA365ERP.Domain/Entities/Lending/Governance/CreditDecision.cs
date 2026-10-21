using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_CreditDecisions</c> (feature 013, T0213; data-model §3.4; FR-038, FR-039, FR-042, FR-052, T63, T64): la decisión de un
/// nivel, individual (<see cref="DecidedByUserId"/>) o colegiada (<see cref="SessionItemId"/>; exactamente uno de los dos, CHECK
/// <c>CK_LND_CreditDecisions_Decider</c>), sobre cualquier sujeto. Hecho (<see cref="IHechoInmutable"/>): otra decisión la reemplaza
/// por <see cref="SupersedesDecisionId"/>.
///
/// <para>
/// T0283: nace sólo por <see cref="Individual"/> o <see cref="Colegiada"/>, que fijan el decisor (el CHECK no se puede violar desde el
/// dominio), exigen el motivo en <c>Deny</c>, <c>Return</c> y en toda excepción a la política, y sellan <see cref="DecisionSha256"/>
/// sobre el sujeto, el monto consolidado, la huella del enrutamiento y las condiciones (<see cref="Huella"/>). La individual exige la
/// persona de quien decide (<c>Lending.Security.UserWithoutPerson</c>, T66).
/// </para>
/// </summary>
public class CreditDecision : AuditableEntity, IHechoInmutable
{
    /// <summary>El motivo que falta es un error de forma del comando (400, <c>Validation.Invalid</c>, campo <c>reason</c>).</summary>
    public const string CodigoMotivoRequerido = "Validation.Invalid";
    public const string CodigoSinPersona = "Lending.Security.UserWithoutPerson";

    /// <summary>Sujeto de decisión (<c>LendingDecisionSubjects</c>).</summary>
    public string SubjectType { get; init; } = string.Empty;

    public Guid SubjectPublicId { get; init; }

    /// <summary>Expediente (data-model §3.13).</summary>
    public Guid CaseRootPublicId { get; init; }

    /// <summary>Nulo en lo que no enruta la matriz (aceptación de diferencias, política SARC).</summary>
    public int? AttributionMatrixId { get; init; }

    public int? LevelId { get; init; }

    public CreditDecisionOutcome Outcome { get; init; }

    /// <summary>JSON: monto o plazo menores, garantía adicional.</summary>
    public string? Conditions { get; init; }

    /// <summary>Obligatorio en <c>Deny</c>, <c>Return</c> y en toda excepción (lo valida el comando).</summary>
    public string? Reason { get; init; }

    public bool IsPolicyException { get; init; }

    /// <summary>Huella de lo decidido (sujeto, monto consolidado, <c>RoutingSha256</c>, condiciones).</summary>
    public string DecisionSha256 { get; init; } = string.Empty;

    /// <summary>Decisión individual.</summary>
    public int? DecidedByUserId { get; init; }

    public int? DecidedByPersonId { get; init; }

    /// <summary>Decisión colegiada: la escribe el cierre del acta.</summary>
    public int? SessionItemId { get; init; }

    public DateTime DecidedAt { get; init; }

    /// <summary>Vigencia de una aprobación (FR-042).</summary>
    public DateOnly? ValidUntil { get; init; }

    /// <summary>Decisión anterior que ésta reemplaza (vencimiento o reenrutamiento).</summary>
    public int? SupersedesDecisionId { get; init; }

    public bool EsColegiada => SessionItemId is not null;

    public bool Aprueba => Outcome is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions;

    /// <summary>Lo que falta en lo decidido, o nulo: sujeto conocido, resultado definido y motivo donde se exige.</summary>
    public static ProblemaDeCartera? Validar(LoDecidido d)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (!LendingDecisionSubjects.Existe(d.SubjectType))
            throw new ArgumentException($"«{d.SubjectType}» no es un sujeto de decisión de Cartera.", nameof(d));
        if (!Enum.IsDefined(d.Outcome)) throw new ArgumentException("Resultado de decisión desconocido.", nameof(d));
        var exigeMotivo = d.Outcome is CreditDecisionOutcome.Deny or CreditDecisionOutcome.Return || d.IsPolicyException;
        return exigeMotivo && string.IsNullOrWhiteSpace(d.Reason)
            ? ProblemaDeCartera.Con(CodigoMotivoRequerido,
                "Negar, devolver o aprobar por excepción exige el motivo de la decisión.", "field", "reason")
            : null;
    }

    /// <summary>La decisión de un usuario con el permiso de su nivel; la persona es obligatoria (T66).</summary>
    public static CreditDecision Individual(LoDecidido d, int decidedByUserId, int? decidedByPersonId, DateTime decidedAt)
    {
        ReglaDeCarteraException.SiHay(Validar(d));
        ArgumentOutOfRangeException.ThrowIfLessThan(decidedByUserId, 1);
        if (decidedByPersonId is null)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoSinPersona,
                "Quien decide no tiene persona asociada a su usuario.", "howToFix",
                "Asocie el usuario a su persona del maestro en Seguridad (usuarios) antes de decidir."));
        return Nueva(d, decidedByUserId, decidedByPersonId, null, decidedAt);
    }

    /// <summary>La decisión de un órgano: la escribe el cierre del acta, con el punto que la votó.</summary>
    public static CreditDecision Colegiada(LoDecidido d, SessionItem punto, DateTime decidedAt)
    {
        ArgumentNullException.ThrowIfNull(punto);
        ReglaDeCarteraException.SiHay(Validar(d));
        if (punto.Id <= 0 || punto.Kind != SessionItemKind.Decision)
            throw new ArgumentException("Una decisión colegiada sale de un punto que decide, ya guardado.", nameof(punto));
        if (punto.SubjectType != d.SubjectType || punto.SubjectPublicId != d.SubjectPublicId)
            throw new ArgumentException("La decisión y el punto de acta son de sujetos distintos.", nameof(punto));
        return Nueva(d, null, null, punto.Id, decidedAt);
    }

    /// <summary>
    /// La huella de lo decidido (data-model §3.4): sujeto, resultado, monto consolidado, huella del enrutamiento, condiciones y
    /// excepción. La consulta la compara antes de decidir para detectar que el contenido cambió (<c>Lending.Decision.ContentChanged</c>).
    /// </summary>
    public static string Huella(LoDecidido d) =>
        new HuellaDeCartera()
            .Texto("subjectType", d.SubjectType)
            .Identificador("subjectPublicId", d.SubjectPublicId)
            .Identificador("caseRootPublicId", d.CaseRootPublicId)
            .Enumeracion("outcome", (CreditDecisionOutcome?)d.Outcome)
            .Decimal("consolidatedAmount", d.ConsolidatedAmount)
            .Texto("routingSha256", d.RoutingSha256)
            .Texto("conditions", d.Conditions)
            .Logico("isPolicyException", d.IsPolicyException)
            .Entero("attributionMatrixId", d.AttributionMatrixId)
            .Entero("levelId", d.LevelId)
            .Calcular();

    private static CreditDecision Nueva(LoDecidido d, int? userId, int? personId, int? sessionItemId, DateTime decidedAt) => new()
    {
        SubjectType = d.SubjectType,
        SubjectPublicId = d.SubjectPublicId,
        CaseRootPublicId = d.CaseRootPublicId,
        AttributionMatrixId = d.AttributionMatrixId,
        LevelId = d.LevelId,
        Outcome = d.Outcome,
        Conditions = d.Conditions,
        Reason = string.IsNullOrWhiteSpace(d.Reason) ? null : d.Reason.Trim(),
        IsPolicyException = d.IsPolicyException,
        DecisionSha256 = Huella(d),
        DecidedByUserId = userId,
        DecidedByPersonId = personId,
        SessionItemId = sessionItemId,
        DecidedAt = decidedAt,
        ValidUntil = d.ValidUntil,
        SupersedesDecisionId = d.SupersedesDecisionId,
    };
}
