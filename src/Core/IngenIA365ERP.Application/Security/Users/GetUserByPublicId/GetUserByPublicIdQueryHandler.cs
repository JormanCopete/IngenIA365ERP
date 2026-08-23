using IngenIA365ERP.Application.Common.Interfaces;
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

    public GetUserByPublicIdQueryHandler(IApplicationDbContext db, IAdminDbContext admin)
    {
        _db = db;
        _admin = admin;
    }

    public async Task<Result<UserDetailDto>> Handle(
        GetUserByPublicIdQuery request, CancellationToken ct)
    {
        // Proyección directa para evitar materializar entidades completas.
        var dto = await _db.Users
            .Where(u => u.PublicId == request.UserPublicId)
            .Select(u => new UserDetailDto(
                u.PublicId,
                u.Username,
                u.Email,
                u.IdentificationNumber,
                u.PersonId,
                u.IsActive,
                u.IsDeleted,
                u.IsEmailVerified,
                u.IsMfaEnabled,
                u.IsSaasOperator,
                u.MustChangePassword,
                u.LastLoginAt,
                u.LastPasswordChangeAt,
                u.FailedLoginAttempts,
                u.LockoutEndAt,
                u.Roles.Select(r => new UserRoleDto(r.PublicId, r.Code, r.Name)).ToList(),
                // Branches asignadas — leídas vía UserBranchAssignments por separado para
                // mantener la proyección dentro de límites de EF (las navegaciones a
                // TenantBranch viven en un DbSet diferente).
                new List<UserBranchDto>()))
            .FirstOrDefaultAsync(ct);

        if (dto is null)
        {
            return Result.Failure<UserDetailDto>("Generic.NotFound", "Recurso no encontrado.");
        }

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
