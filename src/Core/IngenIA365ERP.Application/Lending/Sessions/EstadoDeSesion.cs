using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Sessions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Sessions;

/// <summary>
/// Una sesión con todo lo que la votación necesita (feature 013, T0296): el órgano, la regla de quórum y mayorías (la sellada al abrir o,
/// en borrador, la vigente en <c>HeldOn</c>), los miembros del órgano, la asistencia, los puntos vivos con sus votos (también los
/// reemplazados) y las personas de los excluidos. Sabe armar la entrada de <see cref="EvaluadorDeVotacion"/> y la huella actual del acta.
/// Los comandos la cargan con seguimiento para cambiarla; las consultas, sin seguimiento. (nuevo)
/// </summary>
internal sealed class EstadoDeSesion
{
    private EstadoDeSesion(Session sesion, GoverningBody organo, GoverningBodyRuleVersion? regla, List<GoverningBodyMember> miembros,
        List<SessionAttendance> asistencia, List<SessionItem> puntos, List<SessionVote> votos, Dictionary<Guid, int> personaPorPublicId)
    {
        Sesion = sesion;
        Organo = organo;
        Regla = regla;
        Miembros = miembros;
        Asistencia = asistencia;
        Puntos = puntos;
        Votos = votos;
        PersonaPorPublicId = personaPorPublicId;
    }

    public Session Sesion { get; }
    public GoverningBody Organo { get; }
    public GoverningBodyRuleVersion? Regla { get; private set; }
    public List<GoverningBodyMember> Miembros { get; }
    public List<SessionAttendance> Asistencia { get; }
    public List<SessionItem> Puntos { get; }
    public List<SessionVote> Votos { get; }

    /// <summary>Las personas de los excluidos de los puntos: <c>COR_People.PublicId</c> → <c>Id</c>.</summary>
    public Dictionary<Guid, int> PersonaPorPublicId { get; }

    public static async Task<EstadoDeSesion> CargarAsync(IApplicationDbContext db, Session sesion, DateTime ahora, bool seguimiento, CancellationToken ct)
    {
        IQueryable<T> Q<T>(IQueryable<T> q) where T : class => seguimiento ? q : q.AsNoTracking();

        var organo = await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters().FirstAsync(b => b.Id == sesion.GoverningBodyId, ct);
        var regla = sesion.RuleVersionId is { } sellada
            ? await db.GoverningBodyRuleVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == sellada, ct)
            : VigenciaBitemporal.VigenteDeCartera(
                await db.GoverningBodyRuleVersions.AsNoTracking().Where(v => v.GoverningBodyId == organo.Id && !v.IsDeleted).ToListAsync(ct),
                sesion.HeldOn, ahora);
        var miembros = await db.GoverningBodyMembers.AsNoTracking().Where(m => m.GoverningBodyId == organo.Id && !m.IsDeleted).ToListAsync(ct);
        var asistencia = await Q(db.SessionAttendances).Where(a => a.SessionId == sesion.Id && !a.IsDeleted).ToListAsync(ct);
        var puntos = await Q(db.SessionItems).Where(i => i.SessionId == sesion.Id && !i.IsDeleted).OrderBy(i => i.Order).ToListAsync(ct);
        var ids = puntos.Select(p => p.Id).ToList();
        var votos = await Q(db.SessionVotes).Where(v => ids.Contains(v.ItemId) && !v.IsDeleted).OrderBy(v => v.Id).ToListAsync(ct);

        var excluidos = puntos.SelectMany(p => p.Excluidos()).Select(e => e.PersonPublicId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => excluidos.Contains(p.PublicId))
            .ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        return new EstadoDeSesion(sesion, organo, regla, miembros, asistencia, puntos, votos, personas);
    }

    /// <summary>Los miembros vigentes en la fecha de la sesión como los cuenta el motor (el suplente, atado a la persona de su principal).</summary>
    public IReadOnlyList<MiembroDelOrgano> MiembrosVigentes()
    {
        var vigentes = Miembros.Where(m => m.VigenteEn(Sesion.HeldOn)).ToList();
        var personaDe = Miembros.ToDictionary(m => m.Id, m => m.PersonId);
        return vigentes.Select(m => m.ComoMiembro(m.AlternateForMemberId is { } p ? personaDe.GetValueOrDefault(p) : null)).ToList();
    }

    /// <summary>El miembro vigente de una persona en la fecha de la sesión, si lo es.</summary>
    public GoverningBodyMember? MiembroDe(int personId) => Miembros.FirstOrDefault(m => m.PersonId == personId && m.VigenteEn(Sesion.HeldOn));

    public IReadOnlyList<AsistenciaASesion> AsistenciaParaElMotor() => Asistencia.Select(a => a.ComoAsistencia()).ToList();

    /// <summary>El quórum de la sesión con su regla; nulo si el órgano no tiene regla vigente en la fecha.</summary>
    public QuorumDeSesion? Quorum() =>
        Regla is null ? null : EvaluadorDeVotacion.Quorum(Regla.ReglaSellada(), MiembrosVigentes(), AsistenciaParaElMotor());

    /// <summary>Los votos de un punto (todos, también los reemplazados) como los cuenta el motor.</summary>
    public IReadOnlyList<VotoRegistrado> VotosDe(SessionItem punto) => Votos.Where(v => v.ItemId == punto.Id).Select(v => v.ComoVoto()).ToList();

    /// <summary>Los excluidos de un punto como los cuenta el motor.</summary>
    public IReadOnlyList<PersonaExcluida> ExcluidosDe(SessionItem punto) =>
        punto.Excluidos().Where(e => PersonaPorPublicId.ContainsKey(e.PersonPublicId))
            .Select(e => new PersonaExcluida(PersonaPorPublicId[e.PersonPublicId], e.Motivo)).ToList();

    /// <summary>La evaluación de un punto con sus votos (o con otros, para preguntar si un voto contaría); nula sin regla.</summary>
    public EvaluacionDeVotacion? Evaluar(SessionItem punto, IReadOnlyList<VotoRegistrado>? votos = null) =>
        Regla is null
            ? null
            : EvaluadorDeVotacion.Evaluar(Regla.ReglaSellada(), MiembrosVigentes(), AsistenciaParaElMotor(),
                new PuntoAVotar(punto.Rule, ExcluidosDe(punto), votos ?? VotosDe(punto)));

    /// <summary>
    /// La huella del acta tal como está (data-model §0.13): sesión, regla sellada, asistencia, puntos —con la huella del contenido de cada
    /// asunto, <paramref name="contenidoDeAsuntos"/>— y votos vigentes. El cierre la compara con la que vio quien cierra
    /// (<c>expectedContentSha256</c>): si algo cambió, <c>Lending.Decision.ContentChanged</c>.
    /// </summary>
    public string HuellaActual(IReadOnlyDictionary<int, string> contenidoDeAsuntos) =>
        new HuellaDeCartera()
            .Identificador("session", Sesion.PublicId)
            .Fecha("heldOn", Sesion.HeldOn)
            .Enumeracion("status", (SessionStatus?)Sesion.Status)
            .Entero("ruleVersionId", Sesion.RuleVersionId)
            .Lista("attendance", Asistencia.OrderBy(a => a.PersonId), (h, a) => h
                .Entero("person", a.PersonId)
                .Entero("member", a.MemberId)
                .Enumeracion("status", (AttendanceStatus?)a.Status)
                .Entero("substitutes", a.SubstitutesMemberId))
            .Lista("items", Puntos.OrderBy(p => p.Order), (h, p) => h
                .Identificador("item", p.PublicId)
                .Entero("order", p.Order)
                .Texto("subject", p.SubjectType)
                .Identificador("subjectPublicId", p.SubjectPublicId)
                .Enumeracion("kind", (SessionItemKind?)p.Kind)
                .Enumeracion("rule", (VotingRule?)p.Rule)
                .Texto("excluded", p.ExcludedPersons)
                .Enumeracion("proposed", p.Outcome)
                .Texto("conditions", p.Conditions)
                .Texto("subjectSha256", contenidoDeAsuntos.GetValueOrDefault(p.Id)))
            .Lista("votes", Votos.Where(v => v.EsVigente).OrderBy(v => v.ItemId).ThenBy(v => v.PersonId), (h, v) => h
                .Entero("item", v.ItemId)
                .Entero("person", v.PersonId)
                .Enumeracion("value", (VoteValue?)v.Value)
                .Enumeracion("method", (VoteMethod?)v.Method))
            .Calcular();
}
