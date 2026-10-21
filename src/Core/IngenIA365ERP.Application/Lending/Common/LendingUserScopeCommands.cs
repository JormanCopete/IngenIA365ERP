using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Common;

// Feature 013, T0299 (US17; T62, FR-004, SC-015; api.md §3.8; data-model §3.14): el alcance de Cartera de un usuario —las oficinas, de
// sólo lectura, y las líneas que opera—. (nuevo)

/// <summary>El usuario del alcance.</summary>
public sealed record UsuarioDelAlcanceDto(Guid UserPublicId, string Name, string? Email);

/// <summary>La sucursal contable (<c>COR_Branches</c>) vinculada a una oficina.</summary>
public sealed record SucursalContableDto(Guid PublicId, string? Code, string Name);

/// <summary>Una oficina asignada en Seguridad (<c>SEC_UserBranchAssignments</c> → <c>ADM_Branches</c>), con su sucursal contable si la tiene.</summary>
public sealed record OficinaDelAlcanceDto(Guid BranchPublicId, string Code, string Name, bool IsDefault, SucursalContableDto? AccountingBranch);

/// <summary>Una línea de crédito asignada (<c>LND_UserCreditLineScopes</c>).</summary>
public sealed record LineaDelAlcanceDto(Guid CreditLinePublicId, string Code, string Name, bool IsDefault);

/// <summary>El alcance de Cartera de un usuario (api.md §3.8).</summary>
public sealed record LendingUserScopeDto(
    UsuarioDelAlcanceDto User,
    bool HasAllOffices,
    bool HasAllLines,
    IReadOnlyList<OficinaDelAlcanceDto> Offices,
    IReadOnlyList<LineaDelAlcanceDto> Lines);

/// <summary>
/// El alcance de Cartera de un usuario (api.md §3.8, <c>GET /api/lending/scopes/users/{userPublicId}</c>, <c>Lending.Scopes.Manage</c>): si
/// tiene por sus roles <c>Lending.Scope.AllOffices</c> / <c>.AllLines</c>, sus oficinas —de sólo lectura, las asigna Seguridad— con su
/// sucursal contable y sus líneas vivas. Usuario inexistente → <c>Generic.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetLendingUserScopeQuery(Guid UserPublicId) : IRequest<Result<LendingUserScopeDto>>;

public sealed class GetLendingUserScopeQueryValidator : AbstractValidator<GetLendingUserScopeQuery>
{
    public GetLendingUserScopeQueryValidator() => RuleFor(x => x.UserPublicId).NotEmpty();
}

public sealed class GetLendingUserScopeQueryHandler(IApplicationDbContext db, IAdminDbContext admin, ICurrentTenantService cooperativa)
    : IRequestHandler<GetLendingUserScopeQuery, Result<LendingUserScopeDto>>
{
    public async Task<Result<LendingUserScopeDto>> Handle(GetLendingUserScopeQuery request, CancellationToken ct)
    {
        var usuario = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId && !u.IsDeleted, ct);
        return usuario is null
            ? Result.Failure<LendingUserScopeDto>(Error.NotFound)
            : Result.Success(await AlcanceDeUsuario.DtoAsync(db, admin, cooperativa, usuario, ct));
    }
}

/// <summary>Una línea que se asigna al usuario (a lo sumo una por defecto).</summary>
public sealed record LineaAsignadaInput(Guid CreditLinePublicId, bool IsDefault);

/// <summary>
/// Reemplaza las líneas de crédito de un usuario (api.md §3.8, <c>PUT /api/lending/scopes/users/{userPublicId}</c>,
/// <c>Lending.Scopes.Manage</c>): las que siguen conservan su fila, las nuevas se agregan y las retiradas quedan de baja lógica
/// (<see cref="UserCreditLineScope.Reemplazar"/>). Dos por defecto → <c>Lending.Scope.DefaultDuplicate</c>; una línea inexistente o fuera
/// del alcance de quien asigna → el 404 de la línea (<c>Lending.CreditLine.NotFound</c>, con <c>Lending.Access.OutOfScope</c>). (nuevo)
/// </summary>
public sealed record SetUserCreditLineScopesCommand(Guid UserPublicId, IReadOnlyList<LineaAsignadaInput> Lines)
    : IRequest<Result<LendingUserScopeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetUserCreditLineScopesCommandValidator : AbstractValidator<SetUserCreditLineScopesCommand>
{
    public SetUserCreditLineScopesCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.Lines).NotNull().WithMessage("Mandá la lista de líneas (puede ir vacía para retirarlas todas).");
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(x => x.CreditLinePublicId).NotEmpty());
    }
}

public sealed class SetUserCreditLineScopesCommandHandler(
    IApplicationDbContext db,
    IAdminDbContext admin,
    ICurrentTenantService cooperativa,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<SetUserCreditLineScopesCommand, Result<LendingUserScopeDto>>
{
    public async Task<Result<LendingUserScopeDto>> Handle(SetUserCreditLineScopesCommand request, CancellationToken ct)
    {
        var usuario = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId && !u.IsDeleted, ct);
        if (usuario is null) return Result.Failure<LendingUserScopeDto>(Error.NotFound);

        var pedidas = request.Lines.Select(l => l.CreditLinePublicId).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().Where(l => pedidas.Contains(l.PublicId) && !l.IsDeleted)
            .ToDictionaryAsync(l => l.PublicId, l => l.Id, ct);
        foreach (var publica in pedidas)
        {
            if (!lineas.TryGetValue(publica, out var id)) return Result.Failure<LendingUserScopeDto>(ErroresDeGobierno.LineaInexistente());
            // Quien asigna sólo reparte lo que opera: fuera de su alcance, la línea no existe (§1.3).
            var dentro = await alcanceDeLaPeticion.AsegurarAsync(null, id, ErroresDeGobierno.LineaInexistente(), auditoria, "CreditLine", publica, ct);
            if (dentro.IsFailure) return Result.Failure<LendingUserScopeDto>(dentro.Error);
        }

        var actuales = await db.UserCreditLineScopes.Where(s => s.UserId == usuario.Id && !s.IsDeleted).ToListAsync(ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var resultado = UserCreditLineScope.Reemplazar(usuario.Id, actuales,
            request.Lines.Select(l => (lineas[l.CreditLinePublicId], l.IsDefault)), reloj.UtcNow, actor.Name);
        if (resultado.Problema is { } problema) return Result.Failure<LendingUserScopeDto>(ErroresDeGobierno.De(problema));

        db.UserCreditLineScopes.AddRange(resultado.Agregadas);
        await db.SaveChangesAsync(ct);
        return Result.Success(await AlcanceDeUsuario.DtoAsync(db, admin, cooperativa, usuario, ct));
    }
}

/// <summary>El DTO del alcance de un usuario (lo comparten la consulta y el comando). (nuevo)</summary>
internal static class AlcanceDeUsuario
{
    public static async Task<LendingUserScopeDto> DtoAsync(IApplicationDbContext db, IAdminDbContext admin, ICurrentTenantService cooperativa,
        Domain.Entities.Security.User usuario, CancellationToken ct)
    {
        var permisos = await (from ur in db.UserRoles.AsNoTracking()
                              join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                              join rp in db.RolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                              join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                              where ur.UserId == usuario.Id && r.IsActive && !r.IsDeleted && !rp.IsDeleted && !p.IsDeleted
                              select p.Resource + "." + p.Action).Distinct().ToListAsync(ct);
        var todasLasOficinas = permisos.Contains(AlcanceDeCarteraDelActor.TodasLasOficinas, StringComparer.OrdinalIgnoreCase);
        var todasLasLineas = permisos.Contains(AlcanceDeCarteraDelActor.TodasLasLineas, StringComparer.OrdinalIgnoreCase);

        var oficinas = new List<OficinaDelAlcanceDto>();
        if (Guid.TryParse(cooperativa.TenantId, out var publica)
            && await admin.Tenants.AsNoTracking().Where(t => t.PublicId == publica).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct) is { } tenantId)
        {
            var asignaciones = await db.UserBranchAssignments.AsNoTracking()
                .Where(a => a.UserId == usuario.Id && a.TenantId == tenantId && a.IsActive && !a.IsDeleted)
                .Select(a => new { a.BranchId, a.IsDefault })
                .ToListAsync(ct);
            var ids = asignaciones.Select(a => a.BranchId).ToList();
            var adm = await admin.TenantBranches.AsNoTracking().Where(b => ids.Contains(b.Id) && !b.IsDeleted).ToListAsync(ct);
            var publicIds = adm.Select(b => (Guid?)b.PublicId).ToList();
            var sucursales = await db.Branches.AsNoTracking()
                .Where(b => !b.IsDeleted && b.TenantBranchPublicId != null && publicIds.Contains(b.TenantBranchPublicId))
                .ToListAsync(ct);
            oficinas = adm.OrderBy(b => b.Code)
                .Select(b =>
                {
                    var s = sucursales.FirstOrDefault(x => x.TenantBranchPublicId == b.PublicId);
                    return new OficinaDelAlcanceDto(b.PublicId, b.Code, b.Name, asignaciones.Any(a => a.BranchId == b.Id && a.IsDefault),
                        s is null ? null : new SucursalContableDto(s.PublicId, s.LegacyCode, s.Name));
                })
                .ToList();
        }

        var lineas = await (from s in db.UserCreditLineScopes.AsNoTracking()
                            join l in db.CreditLines.AsNoTracking() on s.CreditLineId equals l.Id
                            where s.UserId == usuario.Id && !s.IsDeleted && !l.IsDeleted
                            orderby l.Code
                            select new LineaDelAlcanceDto(l.PublicId, l.Code, l.Name, s.IsDefault)).ToListAsync(ct);

        return new LendingUserScopeDto(new UsuarioDelAlcanceDto(usuario.PublicId, usuario.Username, usuario.Email), todasLasOficinas, todasLasLineas,
            oficinas, lineas);
    }
}
