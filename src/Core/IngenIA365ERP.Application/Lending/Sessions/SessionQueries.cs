using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Sessions;

// Feature 013, T0296 (US17; T64, FR-039, FR-040; api.md §5; data-model §3.8–§3.11): las consultas de sesiones y actas y sus DTO. (nuevo)

/// <summary>El órgano de una sesión: código y nombre.</summary>
public sealed record OrganoDeSesionDto(Guid PublicId, string Code, string Name);

/// <summary>El quórum sellado de la sesión.</summary>
public sealed record QuorumDto(int Required, int Present, bool Met, int Composition);

/// <summary>Una asistencia: la persona, su miembro (nulo = invitado), su papel y si asiste por un principal.</summary>
public sealed record AsistenteDto(PersonaRefDto Person, Guid? MemberPublicId, BodyMemberRole? Role, AttendanceStatus Status, Guid? SubstitutesForMemberPublicId);

/// <summary>Una persona excluida de un punto con su motivo (<c>Applicant</c>, <c>Filer</c>, <c>Interested</c>).</summary>
public sealed record ExcluidoDto(PersonaRefDto Person, string Reason);

/// <summary>Un voto, también el reemplazado (que lleva <see cref="ReplacedAt"/>).</summary>
public sealed record SessionVoteDto(
    Guid VotePublicId,
    PersonaRefDto Person,
    VoteValue Vote,
    VoteMethod Method,
    UsuarioRefDto? RecordedBy,
    DateTime At,
    Guid? ReplacesVotePublicId,
    string? ReplacementReason,
    DateTime? ReplacedAt);

/// <summary>El conteo de un punto: votos vigentes que cuentan, composición, elegibles y los favorables que se requieren.</summary>
public sealed record TallyDto(int For, int Against, int Abstain, int Composition, int Eligible, int Required);

/// <summary>Un punto del acta (api.md §5).</summary>
public sealed record SessionItemDto(
    Guid ItemPublicId,
    int Order,
    string Subject,
    Guid SubjectPublicId,
    string Title,
    string? Summary,
    SessionItemKind Kind,
    VotingRule Rule,
    IReadOnlyList<ExcluidoDto> Excluded,
    IReadOnlyList<SessionVoteDto> Votes,
    TallyDto? Tally,
    CreditDecisionOutcome? Outcome,
    string? Conditions,
    string? Explanation,
    Guid? DecisionPublicId);

/// <summary>
/// Una sesión con su acta (api.md §5, <c>SessionDto</c>). <see cref="ContentSha256"/> es la huella sellada al cerrar;
/// <see cref="CurrentContentSha256"/>, la huella del acta tal como está, la que se manda como <c>expectedContentSha256</c> al cerrarla.
/// </summary>
public sealed record SessionDto(
    Guid PublicId,
    OrganoDeSesionDto Body,
    string? Number,
    SessionKind Kind,
    SessionMode Mode,
    DateOnly HeldOn,
    string? Location,
    SessionStatus Status,
    QuorumDto? Quorum,
    IReadOnlyList<AsistenteDto> Attendees,
    IReadOnlyList<SessionItemDto> Items,
    string? Notes,
    string? ContentSha256,
    string? CurrentContentSha256,
    UsuarioRefDto? ClosedBy,
    DateTime? ClosedAt,
    Guid? MinutesAttachmentPublicId);

/// <summary>Una sesión en la lista (api.md §5).</summary>
public sealed record SessionListItemDto(
    Guid PublicId,
    OrganoDeSesionDto Body,
    string? Number,
    SessionKind Kind,
    SessionMode Mode,
    DateOnly HeldOn,
    SessionStatus Status,
    int Items,
    DateTime? ClosedAt);

/// <summary>El acta de un punto cerrado.</summary>
public sealed record ActaDelPuntoDto(Guid PublicId, string? Number, OrganoDeSesionDto Body, DateTime? ClosedAt);

/// <summary>Un punto de un acta cerrada: el selector de «punto de acta» de api.md §1.9.</summary>
public sealed record ClosedSessionItemDto(
    Guid SessionItemPublicId,
    ActaDelPuntoDto Session,
    string Subject,
    Guid SubjectPublicId,
    string Title,
    SessionItemKind Kind,
    CreditDecisionOutcome? Outcome,
    string? Conditions);

/// <summary>
/// Las sesiones (api.md §5, <c>GET /api/lending/sessions?body=&amp;status=&amp;from=&amp;to=&amp;page=</c>, <c>Lending.Sessions.Read</c>), la más
/// reciente primero. El acta es del órgano, no de una oficina: el alcance de oficina y línea lo aplica cada historia sobre sus sujetos. (nuevo)
/// </summary>
public sealed record ListSessionsQuery(
    Guid? Body = null,
    SessionStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<SessionListItemDto>>>;

public sealed class ListSessionsQueryValidator : AbstractValidator<ListSessionsQuery>;

public sealed class ListSessionsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListSessionsQuery, Result<PagedResult<SessionListItemDto>>>
{
    public async Task<Result<PagedResult<SessionListItemDto>>> Handle(ListSessionsQuery request, CancellationToken ct)
    {
        var consulta = from s in db.LendingSessions.AsNoTracking()
                       join b in db.GoverningBodies.AsNoTracking().IgnoreQueryFilters() on s.GoverningBodyId equals b.Id
                       where !s.IsDeleted
                       select new { s, b };
        if (request.Body is { } organo) consulta = consulta.Where(x => x.b.PublicId == organo);
        if (request.Status is { } estado) consulta = consulta.Where(x => x.s.Status == estado);
        if (request.From is { } desde) consulta = consulta.Where(x => x.s.HeldOn >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(x => x.s.HeldOn <= hasta);

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.s.HeldOn).ThenByDescending(x => x.s.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var ids = filas.Select(f => f.s.Id).ToList();
        var puntos = await db.SessionItems.AsNoTracking().Where(i => ids.Contains(i.SessionId) && !i.IsDeleted)
            .GroupBy(i => i.SessionId).Select(g => new { g.Key, Cuantos = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Cuantos, ct);

        return Result.Success(new PagedResult<SessionListItemDto>(filas.Select(f => new SessionListItemDto(f.s.PublicId,
            new OrganoDeSesionDto(f.b.PublicId, f.b.Code, f.b.Name), f.s.Number, f.s.Kind, f.s.Mode, f.s.HeldOn, f.s.Status,
            puntos.GetValueOrDefault(f.s.Id), f.s.ClosedAt)).ToList(), pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>
/// Una sesión con su acta (api.md §5, <c>GET /api/lending/sessions/{id}</c>, <c>Lending.Sessions.Read</c>). Es <b>lectura sensible</b>:
/// deja <c>Lending.Session.Viewed</c> por <see cref="LendingAuditEmitter"/> (C89). En una sesión no cerrada trae la huella actual del acta
/// (con la de cada asunto) para cerrarla. (nuevo)
/// </summary>
public sealed record GetSessionQuery(Guid SessionPublicId) : IRequest<Result<SessionDto>>;

public sealed class GetSessionQueryValidator : AbstractValidator<GetSessionQuery>
{
    public GetSessionQueryValidator() => RuleFor(x => x.SessionPublicId).NotEmpty();
}

public sealed class GetSessionQueryHandler(IApplicationDbContext db, AsuntosDeActa asuntos, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<GetSessionQuery, Result<SessionDto>>
{
    public async Task<Result<SessionDto>> Handle(GetSessionQuery request, CancellationToken ct)
    {
        var sesion = await db.LendingSessions.AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == request.SessionPublicId && !s.IsDeleted, ct);
        if (sesion is null) return Result.Failure<SessionDto>(ErroresDeGobierno.SesionInexistente());

        var estado = await EstadoDeSesion.CargarAsync(db, sesion, reloj.UtcNow, seguimiento: false, ct);
        string? actual = null;
        if (sesion.Status != SessionStatus.Closed)
        {
            var descritos = await ActasDeSesion.DescribirAsync(estado, asuntos, ct);
            if (descritos.IsSuccess) actual = estado.HuellaActual(descritos.Value.ToDictionary(d => d.Key, d => d.Value.ContentSha256));
        }

        var dto = await ActasDeSesion.DtoAsync(db, estado, actual, ct);
        await auditoria.EmitirActaVistaAsync(sesion.PublicId, sesion.Number, comoPdf: false, ct);
        return Result.Success(dto);
    }
}

/// <summary>
/// Los puntos de actas cerradas (api.md §5, <c>GET /api/lending/sessions/items?subject=&amp;outcome=&amp;body=&amp;closedFrom=&amp;page=</c>): el
/// selector del «punto de acta» que piden los actos «con acta» (§1.9). (nuevo)
/// </summary>
public sealed record ListSessionItemsQuery(
    string? Subject = null,
    CreditDecisionOutcome? Outcome = null,
    Guid? Body = null,
    DateOnly? ClosedFrom = null,
    Guid? SubjectPublicId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ClosedSessionItemDto>>>;

public sealed class ListSessionItemsQueryValidator : AbstractValidator<ListSessionItemsQuery>;

public sealed class ListSessionItemsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListSessionItemsQuery, Result<PagedResult<ClosedSessionItemDto>>>
{
    public async Task<Result<PagedResult<ClosedSessionItemDto>>> Handle(ListSessionItemsQuery request, CancellationToken ct)
    {
        var consulta = from i in db.SessionItems.AsNoTracking()
                       join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                       join b in db.GoverningBodies.AsNoTracking().IgnoreQueryFilters() on s.GoverningBodyId equals b.Id
                       where !i.IsDeleted && s.Status == SessionStatus.Closed
                       select new { i, s, b };
        if (!string.IsNullOrWhiteSpace(request.Subject)) consulta = consulta.Where(x => x.i.SubjectType == request.Subject);
        if (request.SubjectPublicId is { } sujeto) consulta = consulta.Where(x => x.i.SubjectPublicId == sujeto);
        if (request.Outcome is { } resultado) consulta = consulta.Where(x => x.i.Outcome == resultado);
        if (request.Body is { } organo) consulta = consulta.Where(x => x.b.PublicId == organo);
        if (request.ClosedFrom is { } desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(x => x.s.ClosedAt >= inicio);
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.s.ClosedAt).ThenBy(x => x.i.Order)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<ClosedSessionItemDto>(filas.Select(f => new ClosedSessionItemDto(f.i.PublicId,
            new ActaDelPuntoDto(f.s.PublicId, f.s.Number, new OrganoDeSesionDto(f.b.PublicId, f.b.Code, f.b.Name), f.s.ClosedAt),
            f.i.SubjectType, f.i.SubjectPublicId, f.i.Title, f.i.Kind, f.i.Outcome, f.i.Conditions)).ToList(), pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Lo que comparten los comandos y las consultas de sesiones. (nuevo)</summary>
internal static class ActasDeSesion
{
    /// <summary>Cada punto vivo descrito por su asunto a la fecha de la sesión (por Id del punto); la primera falla corta.</summary>
    public static async Task<Result<IReadOnlyDictionary<int, AsuntoDeActa>>> DescribirAsync(EstadoDeSesion estado, AsuntosDeActa asuntos, CancellationToken ct)
    {
        var descritos = new Dictionary<int, AsuntoDeActa>();
        foreach (var punto in estado.Puntos)
        {
            var asunto = asuntos.Buscar(punto.SubjectType);
            if (asunto is null) return Result.Failure<IReadOnlyDictionary<int, AsuntoDeActa>>(ErroresDeGobierno.SujetoNoDisponible(punto.SubjectType));
            var descrito = await asunto.DescribirAsync(punto.SubjectPublicId, estado.Sesion.HeldOn, ct);
            if (descrito.IsFailure) return Result.Failure<IReadOnlyDictionary<int, AsuntoDeActa>>(descrito.Error);
            descritos[punto.Id] = descrito.Value;
        }
        return Result.Success<IReadOnlyDictionary<int, AsuntoDeActa>>(descritos);
    }

    public static async Task<SessionDto> DtoAsync(IApplicationDbContext db, EstadoDeSesion estado, string? huellaActual, CancellationToken ct)
    {
        var s = estado.Sesion;
        var personas = await ReferenciasDeGobierno.PersonasAsync(db,
            estado.Asistencia.Select(a => a.PersonId).Concat(estado.Votos.Select(v => v.PersonId)).Concat(estado.PersonaPorPublicId.Values), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db,
            estado.Votos.Select(v => v.RecordedByUserId).Concat(s.ClosedByUserId is { } c ? new[] { c } : Array.Empty<int>()), ct);
        var publicoDeMiembro = estado.Miembros.ToDictionary(m => m.Id, m => m);
        var publicoDeVoto = estado.Votos.ToDictionary(v => v.Id, v => v.PublicId);
        var decisiones = estado.Puntos.Where(p => p.DecisionId is not null).Select(p => p.DecisionId!.Value).ToList();
        var publicoDeDecision = await db.CreditDecisions.AsNoTracking().Where(d => decisiones.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.PublicId, ct);
        var quorum = s.QuorumRequired is { } requerido
            ? new QuorumDto(requerido, s.PresentEligibleCount ?? 0, s.QuorumMet ?? false, s.CompositionCount ?? 0)
            : null;

        PersonaRefDto Persona(int id) => personas.TryGetValue(id, out var p) ? p : new PersonaRefDto(Guid.Empty, string.Empty);

        var puntos = estado.Puntos.Select(p =>
        {
            var evaluacion = s.Status == SessionStatus.Closed || estado.Regla is null ? null : estado.Evaluar(p);
            var tally = evaluacion is not null
                ? new TallyDto(evaluacion.AFavor, evaluacion.EnContra, evaluacion.Abstenciones, evaluacion.Quorum.Composicion, evaluacion.Elegibles, evaluacion.Requeridos)
                : p.VotesFor is { } aFavor ? new TallyDto(aFavor, p.VotesAgainst ?? 0, p.Abstentions ?? 0, s.CompositionCount ?? 0, 0, 0) : null;
            return new SessionItemDto(p.PublicId, p.Order, p.SubjectType, p.SubjectPublicId, p.Title, p.Summary, p.Kind, p.Rule,
                p.Excluidos().Select(e => new ExcluidoDto(
                    estado.PersonaPorPublicId.TryGetValue(e.PersonPublicId, out var id) ? Persona(id) : new PersonaRefDto(e.PersonPublicId, string.Empty),
                    e.Motivo)).ToList(),
                estado.Votos.Where(v => v.ItemId == p.Id).Select(v => new SessionVoteDto(v.PublicId, Persona(v.PersonId), v.Value, v.Method,
                    usuarios.GetValueOrDefault(v.RecordedByUserId), v.RecordedAt, v.ReplacesVoteId is { } r ? publicoDeVoto.GetValueOrDefault(r) : null,
                    v.ReplacementReason, v.ReplacedAt)).ToList(),
                tally, p.Outcome, p.Conditions, p.EvaluationExplanation ?? evaluacion?.TextoDeLaExplicacion,
                p.DecisionId is { } d ? publicoDeDecision.GetValueOrDefault(d) : null);
        }).ToList();

        return new SessionDto(s.PublicId, new OrganoDeSesionDto(estado.Organo.PublicId, estado.Organo.Code, estado.Organo.Name), s.Number, s.Kind, s.Mode,
            s.HeldOn, s.Location, s.Status, quorum,
            estado.Asistencia.OrderBy(a => a.MemberId is null).ThenBy(a => a.PersonId).Select(a =>
            {
                var miembro = a.MemberId is { } m ? publicoDeMiembro.GetValueOrDefault(m) : null;
                var principal = a.SubstitutesMemberId is { } sm ? publicoDeMiembro.GetValueOrDefault(sm) : null;
                return new AsistenteDto(Persona(a.PersonId), miembro?.PublicId, miembro?.Role, a.Status, principal?.PublicId);
            }).ToList(),
            puntos, s.Notes, s.ContentSha256, huellaActual,
            s.ClosedByUserId is { } cerro ? usuarios.GetValueOrDefault(cerro) : null, s.ClosedAt, s.MinutesAttachmentPublicId);
    }
}
