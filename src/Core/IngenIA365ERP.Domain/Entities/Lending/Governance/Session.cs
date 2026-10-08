using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Sessions;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_Sessions</c> (feature 013, T0213; data-model §3.8; FR-039, FR-040, FR-095, FR-102, FR-105, FR-141, D17, T64): la sesión de
/// un órgano con su acta, <c>Draft → Open → Closed</c>. Documento con estado, no hecho; cerrada no cambia
/// (<c>Lending.Session.Closed</c>) y se corrige con otra sesión.
///
/// <para>
/// T0282: <see cref="Crear"/> la deja en <c>Draft</c>; registrar la asistencia la abre (<see cref="Abrir"/>: sella la versión de
/// reglas vigente en <see cref="HeldOn"/>, la composición, el quórum requerido y los presentes elegibles, y la pasa a <c>Open</c>);
/// mientras está abierta el quórum se vuelve a sellar con cada asistencia (<see cref="ActualizarQuorum"/>); <see cref="Cerrar"/>
/// exige quórum, sella número, huella y PDF y la deja <c>Closed</c>. Ninguna otra transición existe; cerrada, nada de la sesión ni de
/// sus hijas cambia (<see cref="ExigirEditable"/>).
/// </para>
/// </summary>
public class Session : AuditableEntity
{
    public int GoverningBodyId { get; set; }

    /// <summary>Prefijo del órgano + consecutivo, asignado al cerrar; nulo mientras no esté cerrada.</summary>
    public string? Number { get; set; }

    public SessionKind Kind { get; set; }

    public SessionMode Mode { get; set; }

    /// <summary>Fecha de la sesión; puede ser anterior al registro (acta digitada después).</summary>
    public DateOnly HeldOn { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string? Location { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Draft;

    /// <summary>Regla de quórum y mayorías sellada al abrir: la vigente en <see cref="HeldOn"/>.</summary>
    public int? RuleVersionId { get; set; }

    /// <summary>Sellado al abrir y al cerrar.</summary>
    public int? CompositionCount { get; set; }

    public int? QuorumRequired { get; set; }

    public int? PresentEligibleCount { get; set; }

    public bool? QuorumMet { get; set; }

    /// <summary>Desarrollo de la sesión.</summary>
    public string? Notes { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int? ClosedByUserId { get; set; }

    /// <summary>Huella canónica del acta al cerrar.</summary>
    public string? ContentSha256 { get; set; }

    /// <summary>PDF del acta emitido al cerrar.</summary>
    public Guid? MinutesAttachmentPublicId { get; set; }

    public const string CodigoCerrada = "Lending.Session.Closed";
    public const string CodigoNoAbierta = "Lending.Session.NotOpen";
    public const string CodigoSinQuorum = "Lending.Session.QuorumNotMet";
    public const string CodigoReglaAjena = "Lending.GoverningBody.RulesInvalid";

    /// <summary>Una sesión nueva del órgano, en <c>Draft</c>.</summary>
    public static Session Crear(int governingBodyId, SessionKind kind, SessionMode mode, DateOnly heldOn, string? location = null, string? notes = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(governingBodyId, 1);
        return new Session
        {
            GoverningBodyId = governingBodyId,
            Kind = kind,
            Mode = mode,
            HeldOn = heldOn,
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            Status = SessionStatus.Draft,
        };
    }

    public bool EstaCerrada => Status == SessionStatus.Closed;

    /// <summary>Cerrada, nada de la sesión cambia: ni asistencia, ni puntos, ni votos, ni notas.</summary>
    public ProblemaDeCartera? ExigirEditable() =>
        EstaCerrada ? new ProblemaDeCartera(CodigoCerrada, "El acta ya está cerrada y no cambia. Corríjala con otra sesión.") : null;

    /// <summary>Votar exige la sesión abierta: en borrador todavía no se registró la asistencia; cerrada, ya no se vota.</summary>
    public ProblemaDeCartera? ExigirAbierta() => Status switch
    {
        SessionStatus.Open => null,
        SessionStatus.Closed => ExigirEditable(),
        _ => new ProblemaDeCartera(CodigoNoAbierta, "La sesión no está abierta: registre primero la asistencia."),
    };

    /// <summary>
    /// <c>Draft → Open</c> al registrar la asistencia: sella la regla vigente en <see cref="HeldOn"/> (del mismo órgano) y el quórum.
    /// Si ya estaba abierta, sólo vuelve a sellar el quórum (la regla sellada no cambia).
    /// </summary>
    public void Abrir(GoverningBodyRuleVersion regla, QuorumDeSesion quorum, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(regla);
        ReglaDeCarteraException.SiHay(ExigirEditable());
        if (Status == SessionStatus.Open)
        {
            ActualizarQuorum(quorum);
            return;
        }
        if (regla.GoverningBodyId != GoverningBodyId || regla.ValidFrom > HeldOn || regla.Id <= 0)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoReglaAjena,
                "La regla de quórum que se sella es la del mismo órgano, vigente en la fecha de la sesión y ya guardada."));

        RuleVersionId = regla.Id;
        StartedAt ??= at;
        Status = SessionStatus.Open;
        SellarQuorum(quorum);
    }

    /// <summary>Con la sesión abierta, el quórum se vuelve a sellar con cada cambio de asistencia.</summary>
    public void ActualizarQuorum(QuorumDeSesion quorum)
    {
        ReglaDeCarteraException.SiHay(ExigirAbierta());
        SellarQuorum(quorum);
    }

    /// <summary>Lo que impide cerrar, o nulo: la sesión tiene que estar abierta y con quórum (<c>data: { required, present }</c>).</summary>
    public ProblemaDeCartera? PuedeCerrar(QuorumDeSesion quorum)
    {
        ArgumentNullException.ThrowIfNull(quorum);
        if (ExigirAbierta() is { } noAbierta) return noAbierta;
        return quorum.Cumple
            ? null
            : new ProblemaDeCartera(CodigoSinQuorum,
                $"La sesión no tiene quórum: se requieren {quorum.Requerido} y hay {quorum.PresentesElegibles}. Sin quórum no se vota ni se cierra.",
                new Dictionary<string, object?> { ["required"] = quorum.Requerido, ["present"] = quorum.PresentesElegibles });
    }

    /// <summary><c>Open → Closed</c>: sella el quórum final, el número del acta, quién y cuándo, su huella y el PDF emitido.</summary>
    public void Cerrar(QuorumDeSesion quorum, string number, DateTime closedAt, int closedByUserId, string contentSha256, Guid? minutesAttachmentPublicId)
    {
        ReglaDeCarteraException.SiHay(PuedeCerrar(quorum));
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentSha256);
        SellarQuorum(quorum);
        Number = number;
        ClosedAt = closedAt;
        ClosedByUserId = closedByUserId;
        EndedAt ??= closedAt;
        ContentSha256 = contentSha256;
        MinutesAttachmentPublicId = minutesAttachmentPublicId;
        Status = SessionStatus.Closed;
    }

    /// <summary>Las notas (desarrollo de la sesión) se escriben mientras no esté cerrada.</summary>
    public void EscribirNotas(string? notas)
    {
        ReglaDeCarteraException.SiHay(ExigirEditable());
        Notes = string.IsNullOrWhiteSpace(notas) ? null : notas;
    }

    private void SellarQuorum(QuorumDeSesion quorum)
    {
        ArgumentNullException.ThrowIfNull(quorum);
        CompositionCount = quorum.Composicion;
        QuorumRequired = quorum.Requerido;
        PresentEligibleCount = quorum.PresentesElegibles;
        QuorumMet = quorum.Cumple;
    }
}
