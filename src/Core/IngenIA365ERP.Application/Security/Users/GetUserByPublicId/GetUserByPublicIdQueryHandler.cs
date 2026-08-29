using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.GetUserByPublicId;

public sealed class GetUserByPublicIdQueryHandler
    : IRequestHandler<GetUserByPublicIdQuery, Result<UserDetailDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IAdminDbContext _admin;
    private readonly ICentralIdentityProvider identidadCentral;

    public GetUserByPublicIdQueryHandler(
        IApplicationDbContext db,
        IAdminDbContext admin,
        ICentralIdentityProvider identidadCentral)
    {
        _db = db;
        _admin = admin;
        this.identidadCentral = identidadCentral;
    }

    public async Task<Result<UserDetailDto>> Handle(
        GetUserByPublicIdQuery request, CancellationToken ct)
    {
        // Proyección directa para evitar materializar entidades completas.
        var fila = await _db.Users
            .Where(u => u.PublicId == request.UserPublicId)
            .Select(u => new
            {
                u.PublicId,
                u.Username,
                u.Email,
                u.IdentificationNumber,
                u.PersonId,
                u.IsActive,
                u.IsDeleted,
                u.IsEmailVerified,
                u.IsSaasOperator,
                u.MustChangePassword,
                u.LastPasswordChangeAt,
                u.FailedLoginAttempts,
                u.LockoutEndAt,
                u.CentralUserId,
                Roles = u.Roles.Select(r => new UserRoleDto(r.PublicId, r.Code, r.Name)).ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (fila is null)
        {
            return Result.Failure<UserDetailDto>("Generic.NotFound", "Recurso no encontrado.");
        }

        // Segundo factor y último acceso salen de la identidad central, igual que
        // en el listado. Las columnas homónimas de SEC_Users existen pero no las
        // escribe nadie: este endpoint respondía «MFA: no» para todo el mundo.
        CentralUserSecuritySnapshot? seguridad = null;
        if (fila.CentralUserId is { } centralUserId)
        {
            var snapshots = await identidadCentral.GetSecuritySnapshotsAsync([centralUserId], ct);
            if (snapshots.TryGetValue(centralUserId, out var s))
            {
                seguridad = s;
            }
        }

        var dto = new UserDetailDto(
            fila.PublicId,
            fila.Username,
            fila.Email,
            fila.IdentificationNumber,
            fila.PersonId,
            fila.IsActive,
            fila.IsDeleted,
            fila.IsEmailVerified,
            seguridad?.TwoFactorEnabled,
            fila.IsSaasOperator,
            fila.MustChangePassword,
            seguridad?.LastLoginAt,
            fila.LastPasswordChangeAt,
            fila.FailedLoginAttempts,
            fila.LockoutEndAt,
            fila.Roles,
            // Branches asignadas — leídas vía UserBranchAssignments por separado para
            // mantener la proyección dentro de límites de EF (las navegaciones a
            // TenantBranch viven en un DbSet diferente).
            new List<UserBranchDto>());

        // Sucursales asignadas. Van en DOS lecturas y no en un Join porque las dos
        // mitades viven en bases distintas: la asignacion es de la cooperativa, la
        // sucursal es del plano de control. Un Join entre ambas solo funcionaba
        // mientras todo compartia base, y dejara de funcionar del todo cuando cada
        // cooperativa tenga la suya.
        var asignaciones = await _db.UserBranchAssignments
            .Where(a => a.UserId ==
                _db.Users.Where(u => u.PublicId == request.UserPublicId).Select(u => u.Id).First())
            .Select(a => new { a.BranchId, a.IsDefault })
            .ToListAsync(ct);

        var idsSucursal = asignaciones.Select(a => a.BranchId).Distinct().ToList();
        var sucursales = await _admin.TenantBranches
            .AsNoTracking()
            .Where(b => idsSucursal.Contains(b.Id))
            .Select(b => new { b.Id, b.PublicId, b.Code, b.Name })
            .ToListAsync(ct);

        var branches = asignaciones
            .Join(sucursales, a => a.BranchId, b => b.Id,
                (a, b) => new UserBranchDto(b.PublicId, b.Code, b.Name, a.IsDefault))
            .ToList();

        return Result.Success(dto with { Branches = branches });
    }
}
