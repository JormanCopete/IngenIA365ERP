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

    public GetUserByPublicIdQueryHandler(IApplicationDbContext db) => _db = db;

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

        // Segunda lectura: branches asignadas (no navegación directa, FK en assignment).
        var branches = await _db.UserBranchAssignments
            .Where(a => a.UserId ==
                _db.Users.Where(u => u.PublicId == request.UserPublicId).Select(u => u.Id).First())
            .Join(_db.TenantBranches,
                a => a.BranchId, b => b.Id,
                (a, b) => new UserBranchDto(b.PublicId, b.Code, b.Name, a.IsDefault))
            .ToListAsync(ct);

        return Result.Success(dto with { Branches = branches });
    }
}
