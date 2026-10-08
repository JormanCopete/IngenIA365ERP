using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Segregation;
using IngenIA365ERP.Domain.Lending.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Sessions;

// Feature 013, T0296 (US17; T64, C75, F7, F8, FR-039, FR-040; api.md §5; data-model §3.8–§3.11): las sesiones de los órganos y sus actas
// —convocar, registrar la asistencia, llevar puntos, votar (de secretaría, propio o presente con passkey o TOTP) y cerrar—. (nuevo)

/// <summary>Los permisos que dependen del cuerpo en las sesiones (api.md §1.4, §5).</summary>
public static class PermisosDeSesion
{
    /// <summary>Registrar votos de otros miembros (secretaría).</summary>
    public const string RegistrarVotos = "Lending.Sessions.RecordVotes";

    /// <summary>Votar el propio.</summary>
    public const string Votar = "Lending.Sessions.Vote";
}

/// <summary>Convoca una sesión del órgano, en borrador (api.md §5, <c>POST /api/lending/sessions</c>, <c>Lending.Sessions.Manage</c>). (nuevo)</summary>
public sealed record CreateSessionCommand(
    Guid GoverningBodyPublicId,
    SessionKind Kind,
    SessionMode Mode,
    DateOnly HeldOn,
    string? Location = null,
    string? Notes = null) : IRequest<Result<SessionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(x => x.GoverningBodyPublicId).NotEmpty().WithMessage("Elegí el órgano.");
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.HeldOn).NotEqual(default(DateOnly)).WithMessage("Indicá la fecha de la sesión.");
        RuleFor(x => x.Location).MaximumLength(200);
    }
}

/// <summary>El órgano tiene que existir y estar activo (<c>Lending.GoverningBody.NotFound</c>). La fecha puede ser anterior al registro.</summary>
public sealed class CreateSessionCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<CreateSessionCommand, Result<SessionDto>>
{
    public async Task<Result<SessionDto>> Handle(CreateSessionCommand request, CancellationToken ct)
    {
        var organo = await db.GoverningBodies.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PublicId == request.GoverningBodyPublicId && b.IsActive && !b.IsDeleted, ct);
        if (organo is null) return Result.Failure<SessionDto>(ErroresDeGobierno.OrganoInexistente());

        var sesion = Session.Crear(organo.Id, request.Kind, request.Mode, request.HeldOn, request.Location, request.Notes);
        db.LendingSessions.Add(sesion);
        await db.SaveChangesAsync(ct);
        var estado = await EstadoDeSesion.CargarAsync(db, sesion, reloj.UtcNow, seguimiento: false, ct);
        return Result.Success(await ActasDeSesion.DtoAsync(db, estado, null, ct));
    }
}

/// <summary>La asistencia de un miembro; <see cref="SubstitutesForMemberPublicId"/> es el principal por el que asiste un suplente.</summary>
public sealed record AttendeeInput(Guid MemberPublicId, bool Present, Guid? SubstitutesForMemberPublicId = null);

/// <summary>
/// Registra la asistencia (api.md §5, <c>POST /{id}/attendance</c>, <c>Lending.Sessions.Manage</c>): la primera vez la sesión pasa
/// <c>Draft → Open</c> y sella la versión de reglas vigente en <c>HeldOn</c>, la composición, el quórum requerido y los presentes elegibles;
/// abierta, cada cambio vuelve a sellar el quórum. Un miembro que no es del órgano en la fecha → <c>Lending.GoverningBody.MemberNotFound</c>;
/// un suplente sólo asiste por su principal (<c>Lending.Session.SubstituteInvalid</c>); cerrada → <c>Lending.Session.Closed</c>. Los invitados
/// van por su persona y no cuentan para el quórum. (nuevo)
/// </summary>
public sealed record RecordSessionAttendanceCommand(Guid SessionPublicId, IReadOnlyList<AttendeeInput> Attendees, IReadOnlyList<Guid>? Guests = null)
    : IRequest<Result<SessionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RecordSessionAttendanceCommandValidator : AbstractValidator<RecordSessionAttendanceCommand>
{
    public RecordSessionAttendanceCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.Attendees).NotNull().WithMessage("Mandá la asistencia de los miembros.");
        RuleForEach(x => x.Attendees).ChildRules(a => a.RuleFor(x => x.MemberPublicId).NotEmpty());
    }
}

public sealed class RecordSessionAttendanceCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<RecordSessionAttendanceCommand, Result<SessionDto>>
{
    public async Task<Result<SessionDto>> Handle(RecordSessionAttendanceCommand request, CancellationToken ct)
    {
        var sesion = await db.LendingSessions.FirstOrDefaultAsync(s => s.PublicId == request.SessionPublicId && !s.IsDeleted, ct);
        if (sesion is null) return Result.Failure<SessionDto>(ErroresDeGobierno.SesionInexistente());
        if (sesion.ExigirEditable() is { } cerrada) return Result.Failure<SessionDto>(ErroresDeGobierno.De(cerrada));

        var ahora = reloj.UtcNow;
        var estado = await EstadoDeSesion.CargarAsync(db, sesion, ahora, seguimiento: true, ct);
        if (estado.Regla is null)
            return Result.Failure<SessionDto>(ErroresDeGobierno.ReglasInvalidas("NoRulesForDate",
                "El órgano no tiene reglas de quórum y mayorías vigentes en la fecha de la sesión. Regístrelas antes de instalarla."));

        foreach (var a in request.Attendees)
        {
            var miembro = estado.Miembros.FirstOrDefault(m => m.PublicId == a.MemberPublicId && m.VigenteEn(sesion.HeldOn));
            if (miembro is null) return Result.Failure<SessionDto>(ErroresDeGobierno.MiembroInexistente());

            int? principal = null;
            if (a.SubstitutesForMemberPublicId is { } publicoDelPrincipal)
            {
                var p = estado.Miembros.FirstOrDefault(m => m.PublicId == publicoDelPrincipal);
                if (p is null) return Result.Failure<SessionDto>(ErroresDeGobierno.MiembroInexistente());
                if (miembro.AlternateForMemberId != p.Id)
                    return Result.Failure<SessionDto>(new Error(SessionAttendance.CodigoSuplenteInvalido,
                        "Un suplente sólo asiste por el principal del que es suplente."));
                principal = p.Id;
            }

            var estadoDeAsistencia = a.Present ? AttendanceStatus.Present : AttendanceStatus.Absent;
            var existente = estado.Asistencia.FirstOrDefault(x => x.PersonId == miembro.PersonId);
            if (existente is null)
            {
                var nueva = SessionAttendance.Registrar(sesion, miembro.PersonId, miembro.Id, estadoDeAsistencia, principal, ahora);
                db.SessionAttendances.Add(nueva);
                estado.Asistencia.Add(nueva);
            }
            else existente.Cambiar(sesion, estadoDeAsistencia, principal, ahora);
        }

        foreach (var invitado in request.Guests ?? [])
        {
            var persona = await db.People.AsNoTracking().Where(p => p.PublicId == invitado && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (persona is null) return Result.Failure<SessionDto>(ErroresDeGobierno.PersonaInexistente());
            if (estado.Asistencia.Any(x => x.PersonId == persona)) continue;
            var nueva = SessionAttendance.Registrar(sesion, persona.Value, null, AttendanceStatus.Present, null, ahora);
            db.SessionAttendances.Add(nueva);
            estado.Asistencia.Add(nueva);
        }

        var quorum = estado.Quorum()!;
        if (sesion.Status == SessionStatus.Draft) sesion.Abrir(estado.Regla, quorum, ahora);
        else sesion.ActualizarQuorum(quorum);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ActasDeSesion.DtoAsync(db, estado, null, ct));
    }
}

/// <summary>
/// Lleva un asunto a la sesión (api.md §5, <c>POST /{id}/items</c>, <c>Lending.Sessions.Manage</c>). El asunto lo describe su
/// <see cref="IAsuntoDeActa"/>: sin asunto registrado → <c>Lending.Session.SubjectNotSupported</c>; no pendiente →
/// <c>Lending.Session.SubjectNotPending</c> con <c>data.subjectStatus</c>; un punto que decide sobre un asunto cuyo nivel efectivo es de otro
/// órgano → <c>Lending.Session.SubjectNotForBody</c>. El sistema calcula la regla —<c>RelatedPartyQualified</c> si un solicitante es
/// vinculado y la cooperativa tiene actividad financiera, si no <c>Majority</c>— y los excluidos: el radicador, los solicitantes y la persona
/// por la que existe el vínculo (FR-040, US2-6). (nuevo)
/// </summary>
public sealed record AddSessionItemCommand(
    Guid SessionPublicId,
    string Subject,
    Guid SubjectPublicId,
    SessionItemKind Kind,
    string? Notes = null,
    Guid? RecommendsToBodyPublicId = null) : IRequest<Result<SessionItemDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AddSessionItemCommandValidator : AbstractValidator<AddSessionItemCommand>
{
    public AddSessionItemCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.Subject).Must(LendingDecisionSubjects.Existe).WithMessage("Elegí un asunto que decida un órgano de Cartera.");
        RuleFor(x => x.SubjectPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.RecommendsToBodyPublicId).Null().When(x => x.Kind == SessionItemKind.Decision)
            .WithMessage("Un punto que decide no recomienda a otro órgano.");
    }
}

public sealed class AddSessionItemCommandHandler(
    IApplicationDbContext db,
    AsuntosDeActa asuntos,
    VinculadosVigentes vinculados,
    ReglasDeVinculados ley,
    IDateTimeService reloj) : IRequestHandler<AddSessionItemCommand, Result<SessionItemDto>>
{
    public async Task<Result<SessionItemDto>> Handle(AddSessionItemCommand request, CancellationToken ct)
    {
        var sesion = await db.LendingSessions.FirstOrDefaultAsync(s => s.PublicId == request.SessionPublicId && !s.IsDeleted, ct);
        if (sesion is null) return Result.Failure<SessionItemDto>(ErroresDeGobierno.SesionInexistente());
        if (sesion.ExigirEditable() is { } cerrada) return Result.Failure<SessionItemDto>(ErroresDeGobierno.De(cerrada));

        var asunto = asuntos.Buscar(request.Subject);
        if (asunto is null) return Result.Failure<SessionItemDto>(ErroresDeGobierno.SujetoNoDisponible(request.Subject));
        var descrito = await asunto.DescribirAsync(request.SubjectPublicId, sesion.HeldOn, ct);
        if (descrito.IsFailure) return Result.Failure<SessionItemDto>(descrito.Error);
        var d = descrito.Value;
        if (!d.Pendiente) return Result.Failure<SessionItemDto>(ErroresDeGobierno.SujetoNoPendiente(d.Estado));
        if (request.Kind == SessionItemKind.Decision && d.GoverningBodyId is { } decide && decide != sesion.GoverningBodyId)
        {
            var otro = await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == decide).Select(b => b.Name).FirstOrDefaultAsync(ct);
            return Result.Failure<SessionItemDto>(ErroresDeGobierno.SujetoDeOtroOrgano(otro));
        }

        int? recomiendaA = null;
        if (request.RecommendsToBodyPublicId is { } destino)
        {
            recomiendaA = await db.GoverningBodies.AsNoTracking().Where(b => b.PublicId == destino && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            if (recomiendaA is null) return Result.Failure<SessionItemDto>(ErroresDeGobierno.OrganoInexistente());
        }

        // Los excluidos: radicador y solicitantes, y la persona por la que existe el vínculo de un solicitante vinculado (FR-040).
        var solicitantes = d.Personas.Where(p => p.Motivo == MotivosDeExclusion.Solicitante).Select(p => p.PersonId).ToList();
        var vinculos = await vinculados.DeAsync(solicitantes, sesion.HeldOn, ct);
        var excluidos = d.Personas.Select(p => (p.PersonId, p.Motivo))
            .Concat(vinculos.Where(v => v.RelatedThroughPersonId is not null).Select(v => (v.RelatedThroughPersonId!.Value, MotivosDeExclusion.Interesado)))
            .ToList();
        var ids = excluidos.Select(e => e.Item1).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);

        var regla = VotingRule.Majority;
        if (vinculos.Count > 0 && await ley.ConActividadFinancieraAsync(sesion.HeldOn, ct))
        {
            var estado = await EstadoDeSesion.CargarAsync(db, sesion, reloj.UtcNow, seguimiento: false, ct);
            if (estado.Regla?.RelatedPartyNumerator is null)
                return Result.Failure<SessionItemDto>(ErroresDeGobierno.ReglasInvalidas("RelatedPartyFractionMissing",
                    "El asunto tiene un vinculado y la regla del órgano no trae la mayoría calificada de vinculados. Llévelo al consejo de administración."));
            regla = VotingRule.RelatedPartyQualified;
        }

        var orden = (await db.SessionItems.IgnoreQueryFilters().Where(i => i.SessionId == sesion.Id).MaxAsync(i => (int?)i.Order, ct) ?? 0) + 1;
        var punto = SessionItem.Agregar(sesion, orden, request.Kind, regla, request.Subject, request.SubjectPublicId, d.Titulo,
            excluidos.Where(e => publicos.ContainsKey(e.Item1)).Select(e => new ExclusionDePunto(publicos[e.Item1], e.Item2)).ToList(),
            string.IsNullOrWhiteSpace(request.Notes) ? d.Resumen : request.Notes, recomiendaA);
        db.SessionItems.Add(punto);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDePuntos.DtoAsync(db, sesion, punto.PublicId, reloj.UtcNow, ct));
    }
}

/// <summary>
/// La propuesta que se vota en un punto —aprobar o aprobar con condiciones— y sus notas (api.md §5, <c>PUT /{id}/items/{itemId}</c>), mientras
/// la sesión no esté cerrada (<c>Lending.Session.Closed</c>). (nuevo)
/// </summary>
public sealed record UpdateSessionItemCommand(
    Guid SessionPublicId,
    Guid ItemPublicId,
    CreditDecisionOutcome? ProposedOutcome = null,
    string? Conditions = null,
    string? Notes = null) : IRequest<Result<SessionItemDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateSessionItemCommandValidator : AbstractValidator<UpdateSessionItemCommand>
{
    public UpdateSessionItemCommandValidator()
    {
        RuleFor(x => x.ProposedOutcome).Must(o => o is null or CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions)
            .WithMessage("Lo que se propone es aprobar o aprobar con condiciones: negar resulta de los votos.");
        RuleFor(x => x.Conditions).NotEmpty().When(x => x.ProposedOutcome == CreditDecisionOutcome.ApproveWithConditions)
            .WithMessage("Aprobar con condiciones lleva las condiciones.");
    }
}

public sealed class UpdateSessionItemCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<UpdateSessionItemCommand, Result<SessionItemDto>>
{
    public async Task<Result<SessionItemDto>> Handle(UpdateSessionItemCommand request, CancellationToken ct)
    {
        var (sesion, punto) = await ConsultasDePuntos.CargarAsync(db, request.SessionPublicId, request.ItemPublicId, ct);
        if (sesion is null) return Result.Failure<SessionItemDto>(ErroresDeGobierno.SesionInexistente());
        if (punto is null) return Result.Failure<SessionItemDto>(ErroresDeGobierno.PuntoInexistente());
        if (sesion.ExigirEditable() is { } cerrada) return Result.Failure<SessionItemDto>(ErroresDeGobierno.De(cerrada));

        punto.Proponer(sesion, request.ProposedOutcome, request.Conditions);
        if (request.Notes is not null) punto.Corregir(sesion, punto.Title, request.Notes);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDePuntos.DtoAsync(db, sesion, punto.PublicId, reloj.UtcNow, ct));
    }
}

/// <summary>Retira un punto sin votos (api.md §5, <c>DELETE /{id}/items/{itemId}</c>, 204): baja lógica con motivo. Con votos → <c>Lending.Session.ItemHasVotes</c>. (nuevo)</summary>
public sealed record RemoveSessionItemCommand(Guid SessionPublicId, Guid ItemPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RemoveSessionItemCommandValidator : ValidadorConMotivo<RemoveSessionItemCommand>
{
    public RemoveSessionItemCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.ItemPublicId).NotEmpty();
    }
}

public sealed class RemoveSessionItemCommandHandler(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj) : IRequestHandler<RemoveSessionItemCommand, Result>
{
    public async Task<Result> Handle(RemoveSessionItemCommand request, CancellationToken ct)
    {
        var (sesion, punto) = await ConsultasDePuntos.CargarAsync(db, request.SessionPublicId, request.ItemPublicId, ct);
        if (sesion is null) return Result.Failure(ErroresDeGobierno.SesionInexistente());
        if (punto is null) return Result.Failure(ErroresDeGobierno.PuntoInexistente());
        if (sesion.ExigirEditable() is { } cerrada) return Result.Failure(ErroresDeGobierno.De(cerrada));
        if (await db.SessionVotes.AnyAsync(v => v.ItemId == punto.Id, ct)) return Result.Failure(ErroresDeGobierno.PuntoConVotos());

        var actor = await actorActual.ObtenerAsync(ct);
        punto.IsDeleted = true;
        punto.DeletedAt = reloj.UtcNow;
        punto.DeletedBy = actor.Name;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// El desafío para que un miembro vote presente con su passkey o su TOTP (api.md §5, <c>POST /{id}/presence-challenge</c>,
/// <c>Lending.Sessions.Read</c>): la sesión abierta y el miembro con usuario (<c>Lending.Session.MemberNotUser</c>). (nuevo)
/// </summary>
public sealed record CreateSessionPresenceChallengeCommand(Guid SessionPublicId, Guid PersonPublicId)
    : IRequest<Result<DesafioDePresenciaDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateSessionPresenceChallengeCommandValidator : AbstractValidator<CreateSessionPresenceChallengeCommand>
{
    public CreateSessionPresenceChallengeCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elegí el miembro que va a votar.");
    }
}

public sealed class CreateSessionPresenceChallengeCommandHandler(IApplicationDbContext db, PresenciaEnSesion presencia, IActorActual actorActual)
    : IRequestHandler<CreateSessionPresenceChallengeCommand, Result<DesafioDePresenciaDto>>
{
    public async Task<Result<DesafioDePresenciaDto>> Handle(CreateSessionPresenceChallengeCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DesafioDePresenciaDto>(ErroresDeGobierno.SinUsuario());
        var sesion = await db.LendingSessions.AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == request.SessionPublicId && !s.IsDeleted, ct);
        if (sesion is null) return Result.Failure<DesafioDePresenciaDto>(ErroresDeGobierno.SesionInexistente());
        if (sesion.ExigirAbierta() is { } noAbierta) return Result.Failure<DesafioDePresenciaDto>(ErroresDeGobierno.De(noAbierta));
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<DesafioDePresenciaDto>(ErroresDeGobierno.PersonaInexistente());

        return await presencia.CrearAsync(sesion, usuario, persona.Value, ct);
    }
}

/// <summary>
/// Registra un voto (api.md §5, <c>POST /{id}/items/{itemId}/votes</c>, <c>Lending.Sessions.Read</c> + <c>RecordVotes</c> o <c>Vote</c>; F8):
/// <list type="bullet">
/// <item>con <c>Lending.Sessions.RecordVotes</c> (secretaría), el de cualquier miembro (<c>RecordedBySecretary</c>); con
/// <c>Lending.Sessions.Vote</c>, sólo el propio (<c>OwnSession</c>: la persona es la del usuario); presente (<c>InPersonPasskey</c>,
/// <c>InPersonTotp</c>) con cualquiera de los dos y el desafío del miembro (<see cref="PresenciaEnSesion"/>). Sin el que corresponde →
/// <c>Lending.Permission.Required</c> con <c>data.permissionCodes</c>;</item>
/// <item>la sesión abierta (<c>NotOpen</c>, <c>Closed</c>) y con quórum (<c>QuorumNotMet</c>);</item>
/// <item>vota un miembro con voto presente, o el suplente por un principal ausente; el excluido, el no miembro o el ausente →
/// <c>Lending.Session.VoterExcluded</c> con <c>data.reason</c> (<c>Applicant</c>, <c>Filer</c>, <c>Interested</c>, <c>NotMember</c>,
/// <c>Absent</c>);</item>
/// <item>un segundo voto de la misma persona → <c>VoteDuplicate</c>, salvo que lleve <see cref="ReplacementReason"/>: entonces es el voto de
/// reemplazo, que marca <c>ReplacedAt</c> en el anterior en el mismo guardado (C75);</item>
/// <item>cada voto escribe su intervención <c>Votar</c> por <see cref="RegistroDeIntervenciones"/> (segregación por persona).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record RecordSessionVoteCommand(
    Guid SessionPublicId,
    Guid ItemPublicId,
    Guid PersonPublicId,
    VoteValue Vote,
    VoteMethod Method,
    PresenciaDto? Presence = null,
    string? ReplacementReason = null) : IRequest<Result<SessionVoteDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RecordSessionVoteCommandValidator : AbstractValidator<RecordSessionVoteCommand>
{
    public RecordSessionVoteCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.ItemPublicId).NotEmpty();
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indicá quién vota.");
        RuleFor(x => x.Vote).IsInEnum();
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Presence).NotNull().WithMessage("El voto presencial lleva el desafío (presence).")
            .When(x => x.Method is VoteMethod.InPersonPasskey or VoteMethod.InPersonTotp);
        RuleFor(x => x.Presence!.Assertion).NotEmpty().WithMessage("Falta la aserción de la llave del miembro.")
            .When(x => x.Method == VoteMethod.InPersonPasskey && x.Presence is not null);
        RuleFor(x => x.Presence!.TotpCode).NotEmpty().WithMessage("Falta el código del miembro.")
            .When(x => x.Method == VoteMethod.InPersonTotp && x.Presence is not null);
        RuleFor(x => x.ReplacementReason).MaximumLength(300).WithMessage("El motivo del reemplazo admite hasta 300 caracteres.");
    }
}

public sealed class RecordSessionVoteCommandHandler(
    IApplicationDbContext db,
    AsuntosDeActa asuntos,
    RegistroDeIntervenciones intervenciones,
    PresenciaEnSesion presencia,
    ICurrentUserPermissions permisos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RecordSessionVoteCommand, Result<SessionVoteDto>>
{
    public async Task<Result<SessionVoteDto>> Handle(RecordSessionVoteCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<SessionVoteDto>(ErroresDeGobierno.SinUsuario());

        var (sesion, punto) = await ConsultasDePuntos.CargarAsync(db, request.SessionPublicId, request.ItemPublicId, ct);
        if (sesion is null) return Result.Failure<SessionVoteDto>(ErroresDeGobierno.SesionInexistente());
        if (punto is null) return Result.Failure<SessionVoteDto>(ErroresDeGobierno.PuntoInexistente());
        if (sesion.ExigirAbierta() is { } noAbierta) return Result.Failure<SessionVoteDto>(ErroresDeGobierno.De(noAbierta));

        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Result.Failure<SessionVoteDto>(ErroresDeGobierno.PersonaInexistente());

        var permiso = await PermisoAsync(request.Method, usuario, persona.Id, ct);
        if (permiso.IsFailure) return Result.Failure<SessionVoteDto>(permiso.Error);

        var estado = await EstadoDeSesion.CargarAsync(db, sesion, reloj.UtcNow, seguimiento: true, ct);
        var quorum = estado.Quorum();
        if (quorum is null || !quorum.Cumple)
            return Result.Failure<SessionVoteDto>(quorum is null
                ? ErroresDeGobierno.ReglasInvalidas("NoRulesForDate", "El órgano no tiene reglas de quórum vigentes en la fecha de la sesión.")
                : ErroresDeGobierno.De(sesion.PuedeCerrar(quorum)!));

        var nombre = ReferenciasDeGobierno.Nombre(persona);
        var miembro = estado.MiembroDe(persona.Id);
        if (miembro is null) return Result.Failure<SessionVoteDto>(ErroresDeGobierno.VotanteExcluido(nombre, MotivosDeExclusion.NoMiembro));

        var puntoSeguido = estado.Puntos.Single(p => p.Id == punto.Id);
        var vigente = estado.Votos.FirstOrDefault(v => v.ItemId == puntoSeguido.Id && v.PersonId == persona.Id && v.EsVigente);
        if (vigente is not null && string.IsNullOrWhiteSpace(request.ReplacementReason))
            return Result.Failure<SessionVoteDto>(ErroresDeGobierno.De(SessionVote.PuedeRegistrar(sesion, puntoSeguido, persona.Id, estado.Votos)!));

        // ¿Contaría este voto? El motor decide con los votos vigentes de los demás más éste.
        var simulados = estado.Votos.Where(v => v.ItemId == puntoSeguido.Id && v.EsVigente && v.PersonId != persona.Id)
            .Select(v => new VotoRegistrado(v.Id, v.PersonId, v.Value, null))
            .Append(new VotoRegistrado(-1, persona.Id, request.Vote, null)).ToList();
        var rechazo = estado.Evaluar(puntoSeguido, simulados)!.Rechazados.FirstOrDefault(r => r.Voto == -1);
        if (rechazo is not null)
            return Result.Failure<SessionVoteDto>(ErroresDeGobierno.VotanteExcluido(nombre, rechazo.Motivo switch
            {
                MotivoDeVotoRechazado.Excluido => rechazo.Detalle ?? MotivosDeExclusion.Interesado,
                MotivoDeVotoRechazado.NoEsMiembroConVoto => MotivosDeExclusion.NoMiembro,
                _ => MotivosDeExclusion.Ausente,
            }));

        Guid? desafio = null;
        if (request.Method is VoteMethod.InPersonPasskey or VoteMethod.InPersonTotp)
        {
            var verificado = await presencia.VerificarAsync(sesion, usuario, persona.Id, request.Method, request.Presence!, ct);
            if (verificado.IsFailure) return Result.Failure<SessionVoteDto>(verificado.Error);
            desafio = verificado.Value;
        }

        // Segregación por persona (T66): quien radicó o solicitó no vota lo suyo; se registra la intervención Votar del miembro.
        var asunto = asuntos.Buscar(puntoSeguido.SubjectType);
        if (asunto is null) return Result.Failure<SessionVoteDto>(ErroresDeGobierno.SujetoNoDisponible(puntoSeguido.SubjectType));
        var descrito = await asunto.DescribirAsync(puntoSeguido.SubjectPublicId, sesion.HeldOn, ct);
        if (descrito.IsFailure) return Result.Failure<SessionVoteDto>(descrito.Error);
        var usuarioDelMiembro = (await presencia.UsuarioDeAsync(persona.Id, ct))?.Id ?? 0;
        var segregacion = await intervenciones.VerificarDeAsync(descrito.Value.Expediente, AccionesDeSegregacion.Votar, sesion.HeldOn,
            usuarioDelMiembro, persona.Id, ct);
        if (segregacion.IsFailure) return Result.Failure<SessionVoteDto>(segregacion.Error);

        var ahora = reloj.UtcNow;
        var voto = vigente is null
            ? SessionVote.Registrar(sesion, puntoSeguido, persona.Id, miembro.Id, request.Vote, request.Method, usuario, ahora, estado.Votos, desafio)
            : SessionVote.Reemplazar(sesion, vigente, request.Vote, request.ReplacementReason!, request.Method, usuario, ahora, desafio);
        db.SessionVotes.Add(voto);
        if (usuarioDelMiembro > 0) intervenciones.Registrar(segregacion.Value, puntoSeguido.SubjectType, puntoSeguido.SubjectPublicId);
        await db.SaveChangesAsync(ct);

        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, [usuario], ct);
        return Result.Success(new SessionVoteDto(voto.PublicId, new PersonaRefDto(persona.PublicId, nombre), voto.Value, voto.Method,
            usuarios.GetValueOrDefault(usuario), voto.RecordedAt, vigente?.PublicId, voto.ReplacementReason, null));
    }

    /// <summary>Quién puede registrar este voto (F8; api.md §1.4): 422 <c>Lending.Permission.Required</c> con lo que falta.</summary>
    private async Task<Result> PermisoAsync(VoteMethod metodo, int usuario, int persona, CancellationToken ct)
    {
        var todos = permisos.EsMaestroGlobal;
        var concedidos = new HashSet<string>(todos ? Array.Empty<string>() : await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var secretaria = todos || concedidos.Contains(PermisosDeSesion.RegistrarVotos);
        var propio = todos || concedidos.Contains(PermisosDeSesion.Votar);

        switch (metodo)
        {
            case VoteMethod.RecordedBySecretary:
                return secretaria ? Result.Success() : Result.Failure(ErroresDeGobierno.PermisoRequerido(PermisosDeSesion.RegistrarVotos));
            case VoteMethod.OwnSession:
                if (!propio && !secretaria) return Result.Failure(ErroresDeGobierno.PermisoRequerido(PermisosDeSesion.RegistrarVotos, PermisosDeSesion.Votar));
                var suya = await db.Users.AsNoTracking().Where(u => u.Id == usuario).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
                // Desde su sesión sólo se vota lo propio; el voto de otro lo registra la secretaría.
                return propio && suya == persona ? Result.Success() : Result.Failure(ErroresDeGobierno.PermisoRequerido(PermisosDeSesion.RegistrarVotos));
            default:
                return secretaria || propio
                    ? Result.Success()
                    : Result.Failure(ErroresDeGobierno.PermisoRequerido(PermisosDeSesion.RegistrarVotos, PermisosDeSesion.Votar));
        }
    }
}

/// <summary>
/// Cierra el acta (api.md §5, <c>POST /{id}/close</c>, <c>Lending.Sessions.Close</c>) en una <see cref="TransaccionExplicita"/>, <b>todo o
/// nada</b>: sin quórum → <c>Lending.Session.QuorumNotMet</c> con <c>{ required, present }</c>; si el acta o algún asunto cambió desde que se
/// revisó (<see cref="ExpectedContentSha256"/> distinta de la huella actual) → <c>Lending.Decision.ContentChanged</c>. Con quórum calcula cada
/// punto con <see cref="EvaluadorDeVotacion"/> y sella su resultado; por cada punto que decide verifica la segregación de quien cierra
/// (<c>Decidir</c>), escribe <c>LND_CreditDecisions</c> y llama a <see cref="IAsuntoDeActa.AlDecidirAsync"/> —si uno falla, no queda nada—;
/// después numera el acta con <see cref="NumeradorDeCartera"/> (<c>SessionMinutes</c>, prefijo del órgano), sella <c>ContentSha256</c> y
/// guarda el PDF por <see cref="IActaDeSesionEnPdf"/>. (nuevo)
/// </summary>
public sealed record CloseSessionCommand(Guid SessionPublicId, string ExpectedContentSha256) : IRequest<Result<SessionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseSessionCommandValidator : AbstractValidator<CloseSessionCommand>
{
    public CloseSessionCommandValidator()
    {
        RuleFor(x => x.SessionPublicId).NotEmpty();
        RuleFor(x => x.ExpectedContentSha256).NotEmpty().Matches("^[0-9a-f]{64}$")
            .WithMessage("Mandá la huella del acta que revisó (64 caracteres hexadecimales).");
    }
}

public sealed class CloseSessionCommandHandler(
    IApplicationDbContext db,
    AsuntosDeActa asuntos,
    RegistroDeIntervenciones intervenciones,
    NumeradorDeCartera numerador,
    IActaDeSesionEnPdf actaEnPdf,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CloseSessionCommand, Result<SessionDto>>
{
    public const string MotivoDeNoAprobado = "El asunto no alcanzó la mayoría requerida en la sesión del órgano.";

    public async Task<Result<SessionDto>> Handle(CloseSessionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<SessionDto>(ErroresDeGobierno.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var sesion = await db.LendingSessions.FirstOrDefaultAsync(s => s.PublicId == request.SessionPublicId && !s.IsDeleted, ct);
            if (sesion is null) return Result.Failure<SessionDto>(ErroresDeGobierno.SesionInexistente());
            if (sesion.ExigirAbierta() is { } noAbierta) return Result.Failure<SessionDto>(ErroresDeGobierno.De(noAbierta));

            var ahora = reloj.UtcNow;
            var estado = await EstadoDeSesion.CargarAsync(db, sesion, ahora, seguimiento: true, ct);
            var quorum = estado.Quorum();
            if (quorum is null)
                return Result.Failure<SessionDto>(ErroresDeGobierno.ReglasInvalidas("NoRulesForDate",
                    "El órgano no tiene reglas de quórum vigentes en la fecha de la sesión."));
            if (sesion.PuedeCerrar(quorum) is { } sinQuorum) return Result.Failure<SessionDto>(ErroresDeGobierno.De(sinQuorum));

            var descritos = await ActasDeSesion.DescribirAsync(estado, asuntos, ct);
            if (descritos.IsFailure) return Result.Failure<SessionDto>(descritos.Error);
            var contenidos = descritos.Value.ToDictionary(d => d.Key, d => d.Value.ContentSha256);
            var actual = estado.HuellaActual(contenidos);
            if (!string.Equals(actual, request.ExpectedContentSha256, StringComparison.Ordinal))
                return Result.Failure<SessionDto>(ErroresDeGobierno.ContenidoCambiado(actual));

            // Cada punto se evalúa y se sella; los que deciden escriben su decisión y se aplican a su asunto, sin guardar todavía.
            var decididos = new List<(SessionItem Punto, CreditDecision Decision, IAsuntoDeActa Asunto)>();
            foreach (var punto in estado.Puntos)
            {
                var evaluacion = estado.Evaluar(punto)!;
                punto.SellarResultado(sesion, evaluacion, punto.PropuestaSiAprueba, punto.Conditions);
                if (punto.Kind != SessionItemKind.Decision) continue;

                var asunto = descritos.Value[punto.Id];
                var segregacion = await intervenciones.VerificarAsync(asunto.Expediente, AccionesDeSegregacion.Decidir, sesion.HeldOn, ct);
                if (segregacion.IsFailure)
                {
                    db.DescartarCambios();
                    return Result.Failure<SessionDto>(segregacion.Error);
                }

                var decision = CreditDecision.Colegiada(new LoDecidido(punto.SubjectType, punto.SubjectPublicId, asunto.Expediente.PublicId,
                    punto.Outcome!.Value, asunto.MontoConsolidado, asunto.RoutingSha256)
                {
                    AttributionMatrixId = asunto.AttributionMatrixId,
                    LevelId = asunto.LevelId,
                    Conditions = punto.Conditions,
                    Reason = punto.Outcome == CreditDecisionOutcome.Deny ? MotivoDeNoAprobado : null,
                }, punto, ahora);
                db.CreditDecisions.Add(decision);
                intervenciones.Registrar(segregacion.Value, punto.SubjectType, punto.SubjectPublicId);
                decididos.Add((punto, decision, asuntos.Buscar(punto.SubjectType)!));
            }

            foreach (var (punto, decision, asunto) in decididos)
            {
                var aplicado = await asunto.AlDecidirAsync(new DecisionDeActa(sesion, punto, decision, punto.Outcome!.Value, punto.Conditions, sesion.HeldOn), ct);
                if (aplicado.IsFailure)
                {
                    // Todo o nada: ni la decisión, ni el número, ni el sello, ni lo que hayan cambiado los asuntos anteriores.
                    db.DescartarCambios();
                    return Result.Failure<SessionDto>(aplicado.Error);
                }
            }

            await db.SaveChangesAsync(ct);
            foreach (var (punto, decision, _) in decididos) punto.AsignarDecision(sesion, decision.Id);

            await numerador.NumerarActaAsync(sesion, estado.Organo, ct);
            sesion.Cerrar(quorum, sesion.Number!, ahora, usuario, HuellaDelActaCerrada(actual, sesion, estado), null);
            await db.SaveChangesAsync(ct);

            var dto = await ActasDeSesion.DtoAsync(db, estado, null, ct);
            var pdf = await actaEnPdf.GuardarAsync(dto, ct);
            if (pdf.IsFailure) return Result.Failure<SessionDto>(pdf.Error);
            if (pdf.Value is { } adjunto)
            {
                sesion.MinutesAttachmentPublicId = adjunto;
                await db.SaveChangesAsync(ct);
                dto = dto with { MinutesAttachmentPublicId = adjunto };
            }
            return Result.Success(dto);
        }, ct);
    }

    /// <summary>La huella sellada del acta cerrada: la del contenido revisado más el número y el resultado de cada punto.</summary>
    private static string HuellaDelActaCerrada(string contenido, Session sesion, EstadoDeSesion estado) =>
        new HuellaDeCartera()
            .Texto("content", contenido)
            .Texto("number", sesion.Number)
            .Lista("results", estado.Puntos.OrderBy(p => p.Order), (h, p) => h
                .Identificador("item", p.PublicId)
                .Enumeracion("outcome", p.Outcome)
                .Texto("conditions", p.Conditions)
                .Entero("for", p.VotesFor)
                .Entero("against", p.VotesAgainst)
                .Entero("abstain", p.Abstentions))
            .Calcular();
}

/// <summary>Lo que comparten los comandos de puntos. (nuevo)</summary>
internal static class ConsultasDePuntos
{
    /// <summary>La sesión y su punto vivo, con seguimiento (cualquiera puede ser nulo).</summary>
    public static async Task<(Session? Sesion, SessionItem? Punto)> CargarAsync(IApplicationDbContext db, Guid sesionPublicId, Guid puntoPublicId, CancellationToken ct)
    {
        var sesion = await db.LendingSessions.FirstOrDefaultAsync(s => s.PublicId == sesionPublicId && !s.IsDeleted, ct);
        if (sesion is null) return (null, null);
        var punto = await db.SessionItems.FirstOrDefaultAsync(i => i.PublicId == puntoPublicId && i.SessionId == sesion.Id && !i.IsDeleted, ct);
        return (sesion, punto);
    }

    public static async Task<SessionItemDto> DtoAsync(IApplicationDbContext db, Session sesion, Guid puntoPublicId, DateTime ahora, CancellationToken ct)
    {
        var estado = await EstadoDeSesion.CargarAsync(db, sesion, ahora, seguimiento: false, ct);
        var dto = await ActasDeSesion.DtoAsync(db, estado, null, ct);
        return dto.Items.Single(i => i.ItemPublicId == puntoPublicId);
    }
}
