using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.AssignBranch;

public sealed class AssignBranchCommandHandler : IRequestHandler<AssignBranchCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IAdminDbContext _admin;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public AssignBranchCommandHandler(
        IApplicationDbContext db,
        IAdminDbContext admin,
        ICurrentUserService currentUser,
        IDateTimeService clock)
    {
        _db = db;
        _admin = admin;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(AssignBranchCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        // Las sucursales viven en la base administrativa (ADM_Branches), no en la
        // operativa. La replica que habia aqui estaba siempre vacia.
        var branch = await _admin.TenantBranches
            .FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId, ct);
        if (branch is null) return Result.Failure("Generic.NotFound", "La sucursal no existe.");

        var existing = await _db.UserBranchAssignments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.BranchId == branch.Id, ct);

        var now = _clock.UtcNow;
        var actor = _currentUser.UserName ?? "SYSTEM";

        if (existing is not null && !existing.IsDeleted)
        {
            return Result.Failure(UserErrorCodes.AlreadyAssignedBranch,
                "El usuario ya está asignado a esa sucursal.");
        }

        // Si se marca IsDefault=true, rebajar las demás a false primero.
        if (request.IsDefault)
        {
            var others = await _db.UserBranchAssignments
                .Where(a => a.UserId == user.Id && a.IsDefault && !a.IsDeleted)
                .ToListAsync(ct);
            foreach (var o in others)
            {
                o.IsDefault = false;
                o.UpdatedBy = actor;
            }
        }

        if (existing is not null)
        {
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
            existing.IsActive = true;
            existing.IsDefault = request.IsDefault;
            existing.UpdatedBy = actor;
        }
        else
        {
            _db.UserBranchAssignments.Add(new UserBranchAssignment
            {
                UserId = user.Id,
                TenantId = branch.TenantId,
                BranchId = branch.Id,
                IsDefault = request.IsDefault,
                IsActive = true,
                CreatedBy = actor,
                UpdatedBy = actor
            });
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
