using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.ConnectedGroups;

// Feature 013, US2 (T0393; FR-041, SC-022; DT §7.10 C112): los grupos conectados que registra el jefe de cartera con su fundamento. Son
// personas por su vínculo económico o jurídico, sin oficina; leerlos es lectura sensible auditada, como los vinculados (Lending.RelatedParty.Viewed).
// Escribir exige Lending.RelatedParties.Manage (el catálogo lo nombra así: «personas vinculadas y grupos conectados»). (nuevo)

/// <summary>Un miembro del grupo (api.md, rutas /api/lending/connected-groups). (nuevo)</summary>
public sealed record ConnectedGroupMemberDto(PersonaRefDto Person, DateOnly ValidFrom, DateTime? EndedAt);

/// <summary>Un grupo conectado registrado con sus miembros. (nuevo)</summary>
public sealed record ConnectedGroupDto(
    Guid PublicId,
    string Name,
    ConnectedGroupBasis Basis,
    string Grounds,
    Guid? SupportAttachmentPublicId,
    DateOnly ValidFrom,
    DateTime? EndedAt,
    string? EndReason,
    IReadOnlyList<ConnectedGroupMemberDto> Members);

/// <summary>Los errores de los grupos conectados. Los mensajes no muestran códigos (FR-013). (nuevo)</summary>
public static class ErroresDeGruposConectados
{
    public const string CodigoInexistente = ConnectedGroup.CodigoInexistente;
    public const string CodigoMiembroRepetido = "Lending.ConnectedGroup.MemberDuplicate";
    public const string CodigoMiembroInexistente = "Lending.ConnectedGroup.MemberNotFound";
    public const string CodigoTerminado = ConnectedGroup.CodigoTerminado;
    public const string PermisoDeGestion = "Lending.RelatedParties.Manage";

    public static Error Inexistente() => new(CodigoInexistente, "El grupo conectado no existe.");

    public static Error MiembroRepetido(Guid persona) => new ErrorConDatos(CodigoMiembroRepetido,
        "La persona ya está en el grupo.", new { personPublicId = persona });

    public static Error MiembroInexistente() => new(CodigoMiembroInexistente, "La persona no está en el grupo.");

    public static Error Terminado() => new(CodigoTerminado, "El grupo ya está terminado: registre uno nuevo.");
}

/// <summary>Lo que comparten los comandos y las consultas de grupos conectados. (nuevo)</summary>
internal static class ConsultasDeGrupos
{
    /// <summary>Sin el permiso de gestión, el mismo 404 que un grupo inexistente (api.md §1.3).</summary>
    public static async Task<bool> PuedeGestionarAsync(ICurrentUserPermissions permisos, CancellationToken ct) =>
        permisos.EsMaestroGlobal || (await permisos.ListAsync(ct)).Contains(ErroresDeGruposConectados.PermisoDeGestion, StringComparer.OrdinalIgnoreCase);

    public static async Task<ConnectedGroupDto> DtoAsync(IApplicationDbContext db, ConnectedGroup g, CancellationToken ct)
    {
        var miembros = await db.ConnectedGroupMembers.AsNoTracking().Where(m => m.GroupId == g.Id && !m.IsDeleted)
            .OrderBy(m => m.ValidFrom).ThenBy(m => m.Id).ToListAsync(ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, miembros.Select(m => m.PersonId), ct);
        return new ConnectedGroupDto(g.PublicId, g.Name, g.Basis, g.Grounds, g.SupportAttachmentPublicId, g.ValidFrom, g.EndedAt, g.EndReason,
            miembros.Select(m => new ConnectedGroupMemberDto(personas[m.PersonId], m.ValidFrom, m.EndedAt)).ToList());
    }

    /// <summary>Las personas vivas por su <c>PublicId</c>; nulo si alguna no existe.</summary>
    public static async Task<IReadOnlyDictionary<Guid, int>?> PersonasAsync(IApplicationDbContext db, IEnumerable<Guid> publicas, CancellationToken ct)
    {
        var pedidas = publicas.Distinct().ToArray();
        var vivas = await db.People.AsNoTracking().Where(p => pedidas.Contains(p.PublicId) && !p.IsDeleted).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        return pedidas.All(vivas.ContainsKey) ? vivas : null;
    }
}

/// <summary>
/// Registra un grupo conectado (C112; <c>Lending.RelatedParties.Manage</c>; con clave): nombre, fundamento —vinculación económica o jurídica,
/// codeudor común, vinculado— con su texto obligatorio, soporte opcional, desde cuándo rige (hoy si no viene) y al menos dos miembros. La
/// concentración se mide desde entonces por deudor y por grupo con el mismo límite (FR-041). (nuevo)
/// </summary>
public sealed record RegisterConnectedGroupCommand(
    string Name,
    ConnectedGroupBasis Basis,
    string Grounds,
    IReadOnlyList<Guid> MemberPublicIds,
    Guid? SupportAttachmentPublicId = null,
    DateOnly? ValidFrom = null) : IRequest<Result<ConnectedGroupDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterConnectedGroupCommandValidator : AbstractValidator<RegisterConnectedGroupCommand>
{
    public RegisterConnectedGroupCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre del grupo.").MaximumLength(150);
        RuleFor(x => x.Basis).IsInEnum();
        RuleFor(x => x.Grounds).NotEmpty().WithMessage("Indique el fundamento del grupo: por qué estas personas forman un riesgo común.")
            .MaximumLength(1000);
        RuleFor(x => x.MemberPublicIds).NotNull().Must(m => m is { Count: >= 2 }).WithMessage("Un grupo tiene al menos dos personas.");
        RuleForEach(x => x.MemberPublicIds).NotEmpty();
    }
}

public sealed class RegisterConnectedGroupCommandHandler(
    IApplicationDbContext db,
    ICurrentUserPermissions permisos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RegisterConnectedGroupCommand, Result<ConnectedGroupDto>>
{
    public async Task<Result<ConnectedGroupDto>> Handle(RegisterConnectedGroupCommand request, CancellationToken ct)
    {
        if (!await ConsultasDeGrupos.PuedeGestionarAsync(permisos, ct)) return Result.Failure<ConnectedGroupDto>(Error.NotFound);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ConnectedGroupDto>(ErroresDeGobierno.SinUsuario());

        var repetida = request.MemberPublicIds.GroupBy(x => x).FirstOrDefault(g => g.Count() > 1);
        if (repetida is not null) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.MiembroRepetido(repetida.Key));
        var personas = await ConsultasDeGrupos.PersonasAsync(db, request.MemberPublicIds, ct);
        if (personas is null) return Result.Failure<ConnectedGroupDto>(ErroresDeGobierno.PersonaInexistente());

        var desde = request.ValidFrom ?? reloj.HoyLocal;
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var grupo = ConnectedGroup.Registrar(request.Name, request.Basis, request.Grounds, request.SupportAttachmentPublicId, desde, usuario);
            db.ConnectedGroups.Add(grupo);
            await db.SaveChangesAsync(ct);
            foreach (var p in request.MemberPublicIds) db.ConnectedGroupMembers.Add(ConnectedGroupMember.Nuevo(grupo.Id, personas[p], desde));
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeGrupos.DtoAsync(db, grupo, ct));
        }, ct);
    }
}

/// <summary>Agrega una persona a un grupo vigente, con motivo (C112). (nuevo)</summary>
public sealed record AddConnectedGroupMemberCommand(Guid GroupPublicId, Guid PersonPublicId, string Reason)
    : IRequest<Result<ConnectedGroupDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class AddConnectedGroupMemberCommandValidator : ValidadorConMotivo<AddConnectedGroupMemberCommand>
{
    public AddConnectedGroupMemberCommandValidator()
    {
        RuleFor(x => x.GroupPublicId).NotEmpty();
        RuleFor(x => x.PersonPublicId).NotEmpty();
    }
}

public sealed class AddConnectedGroupMemberCommandHandler(IApplicationDbContext db, ICurrentUserPermissions permisos, IDateTimeService reloj)
    : IRequestHandler<AddConnectedGroupMemberCommand, Result<ConnectedGroupDto>>
{
    public async Task<Result<ConnectedGroupDto>> Handle(AddConnectedGroupMemberCommand request, CancellationToken ct)
    {
        if (!await ConsultasDeGrupos.PuedeGestionarAsync(permisos, ct)) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.Inexistente());
        var grupo = await db.ConnectedGroups.FirstOrDefaultAsync(g => g.PublicId == request.GroupPublicId && !g.IsDeleted, ct);
        if (grupo is null) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.Inexistente());
        if (grupo.EndedAt is not null) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.Terminado());
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<ConnectedGroupDto>(ErroresDeGobierno.PersonaInexistente());
        if (await db.ConnectedGroupMembers.AnyAsync(m => m.GroupId == grupo.Id && m.PersonId == persona && m.EndedAt == null && !m.IsDeleted, ct))
            return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.MiembroRepetido(request.PersonPublicId));

        db.ConnectedGroupMembers.Add(ConnectedGroupMember.Nuevo(grupo.Id, persona.Value, reloj.HoyLocal));
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDeGrupos.DtoAsync(db, grupo, ct));
    }
}

/// <summary>Retira una persona de un grupo, con motivo (C112): desde ese día ya no suma en el grupo. (nuevo)</summary>
public sealed record EndConnectedGroupMemberCommand(Guid GroupPublicId, Guid PersonPublicId, string Reason)
    : IRequest<Result<ConnectedGroupDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class EndConnectedGroupMemberCommandValidator : ValidadorConMotivo<EndConnectedGroupMemberCommand>
{
    public EndConnectedGroupMemberCommandValidator()
    {
        RuleFor(x => x.GroupPublicId).NotEmpty();
        RuleFor(x => x.PersonPublicId).NotEmpty();
    }
}

public sealed class EndConnectedGroupMemberCommandHandler(IApplicationDbContext db, ICurrentUserPermissions permisos, IDateTimeService reloj)
    : IRequestHandler<EndConnectedGroupMemberCommand, Result<ConnectedGroupDto>>
{
    public async Task<Result<ConnectedGroupDto>> Handle(EndConnectedGroupMemberCommand request, CancellationToken ct)
    {
        if (!await ConsultasDeGrupos.PuedeGestionarAsync(permisos, ct)) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.Inexistente());
        var grupo = await db.ConnectedGroups.AsNoTracking().FirstOrDefaultAsync(g => g.PublicId == request.GroupPublicId && !g.IsDeleted, ct);
        if (grupo is null) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.Inexistente());
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        var miembro = persona is null
            ? null
            : await db.ConnectedGroupMembers.FirstOrDefaultAsync(m => m.GroupId == grupo.Id && m.PersonId == persona && m.EndedAt == null && !m.IsDeleted, ct);
        if (miembro is null) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.MiembroInexistente());

        miembro.Retirar(reloj.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDeGrupos.DtoAsync(db, grupo, ct));
    }
}

/// <summary>Termina un grupo conectado, con motivo (C112). (nuevo)</summary>
public sealed record EndConnectedGroupCommand(Guid GroupPublicId, string Reason) : IRequest<Result<ConnectedGroupDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class EndConnectedGroupCommandValidator : ValidadorConMotivo<EndConnectedGroupCommand>
{
    public EndConnectedGroupCommandValidator() => RuleFor(x => x.GroupPublicId).NotEmpty();
}

public sealed class EndConnectedGroupCommandHandler(IApplicationDbContext db, ICurrentUserPermissions permisos, IDateTimeService reloj)
    : IRequestHandler<EndConnectedGroupCommand, Result<ConnectedGroupDto>>
{
    public async Task<Result<ConnectedGroupDto>> Handle(EndConnectedGroupCommand request, CancellationToken ct)
    {
        if (!await ConsultasDeGrupos.PuedeGestionarAsync(permisos, ct)) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.Inexistente());
        var grupo = await db.ConnectedGroups.FirstOrDefaultAsync(g => g.PublicId == request.GroupPublicId && !g.IsDeleted, ct);
        if (grupo is null) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.Inexistente());
        if (grupo.EndedAt is not null) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.Terminado());

        grupo.Terminar(reloj.UtcNow, request.Reason);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConsultasDeGrupos.DtoAsync(db, grupo, ct));
    }
}

/// <summary>
/// Los grupos conectados (con los de una persona si se pide; sin los terminados salvo <see cref="IncludeEnded"/>). Lectura sensible: deja
/// <c>Lending.RelatedParty.Viewed</c> con los filtros y las filas. Personas sin oficina: no aplica el alcance de Cartera (como los vinculados).
/// (nuevo)
/// </summary>
public sealed record ListConnectedGroupsQuery(Guid? PersonPublicId = null, bool IncludeEnded = false) : IRequest<Result<IReadOnlyList<ConnectedGroupDto>>>;

public sealed class ListConnectedGroupsQueryValidator : AbstractValidator<ListConnectedGroupsQuery>;

public sealed class ListConnectedGroupsQueryHandler(IApplicationDbContext db, LendingAuditEmitter auditoria)
    : IRequestHandler<ListConnectedGroupsQuery, Result<IReadOnlyList<ConnectedGroupDto>>>
{
    public async Task<Result<IReadOnlyList<ConnectedGroupDto>>> Handle(ListConnectedGroupsQuery request, CancellationToken ct)
    {
        var consulta = db.ConnectedGroups.AsNoTracking().Where(g => !g.IsDeleted);
        if (!request.IncludeEnded) consulta = consulta.Where(g => g.EndedAt == null);
        if (request.PersonPublicId is { } persona)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(g => db.ConnectedGroupMembers.Any(m => m.GroupId == g.Id && m.PersonId == id && !m.IsDeleted));
        }
        var grupos = await consulta.OrderBy(g => g.Name).ThenBy(g => g.Id).ToListAsync(ct);
        var lista = new List<ConnectedGroupDto>();
        foreach (var g in grupos) lista.Add(await ConsultasDeGrupos.DtoAsync(db, g, ct));
        await auditoria.EmitirVinculadosVistosAsync(new { connectedGroups = true, request.PersonPublicId, request.IncludeEnded }, lista.Count, ct);
        return Result.Success<IReadOnlyList<ConnectedGroupDto>>(lista);
    }
}

/// <summary>Un grupo conectado con sus miembros (lectura sensible auditada, <c>Lending.RelatedParty.Viewed</c>). (nuevo)</summary>
public sealed record GetConnectedGroupQuery(Guid GroupPublicId) : IRequest<Result<ConnectedGroupDto>>;

public sealed class GetConnectedGroupQueryValidator : AbstractValidator<GetConnectedGroupQuery>
{
    public GetConnectedGroupQueryValidator() => RuleFor(x => x.GroupPublicId).NotEmpty();
}

public sealed class GetConnectedGroupQueryHandler(IApplicationDbContext db, LendingAuditEmitter auditoria)
    : IRequestHandler<GetConnectedGroupQuery, Result<ConnectedGroupDto>>
{
    public async Task<Result<ConnectedGroupDto>> Handle(GetConnectedGroupQuery request, CancellationToken ct)
    {
        var grupo = await db.ConnectedGroups.AsNoTracking().FirstOrDefaultAsync(g => g.PublicId == request.GroupPublicId && !g.IsDeleted, ct);
        if (grupo is null) return Result.Failure<ConnectedGroupDto>(ErroresDeGruposConectados.Inexistente());
        var dto = await ConsultasDeGrupos.DtoAsync(db, grupo, ct);
        await auditoria.EmitirVinculadosVistosAsync(new { connectedGroupPublicId = grupo.PublicId }, dto.Members.Count, ct);
        return Result.Success(dto);
    }
}
