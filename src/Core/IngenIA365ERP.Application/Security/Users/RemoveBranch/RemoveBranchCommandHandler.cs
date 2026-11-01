using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.RemoveBranch;

public sealed class RemoveBranchCommandHandler : IRequestHandler<RemoveBranchCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IAdminDbContext _admin;
    private readonly ICurrentTenantService _cooperativa;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public RemoveBranchCommandHandler(
        IApplicationDbContext db,
        IAdminDbContext admin,
        ICurrentTenantService cooperativa,
        ICurrentUserService currentUser,
        IDateTimeService clock)
    {
        _db = db;
        _admin = admin;
        _cooperativa = cooperativa;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(RemoveBranchCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        // Sólo las oficinas de la cooperativa de la petición: la de otra responde igual que una inexistente.
        var cooperativaId = await CooperativaActiva.IdAsync(_admin, _cooperativa, ct);
        var branch = cooperativaId is null ? null : await _admin.TenantBranches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId && b.TenantId == cooperativaId, ct);
        if (branch is null) return Result.Failure("Generic.NotFound", "La sucursal no existe.");

        var link = await _db.UserBranchAssignments
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.BranchId == branch.Id && !a.IsDeleted, ct);
        if (link is null)
        {
            return Result.Failure(UserErrorCodes.NotAssignedBranch,
                "El usuario no tiene asignada esa oficina.");
        }

        // Baja lógica explícita (Principio XI): la fila se conserva y AssignBranch la revive. A diferencia de la
        // tabla puente de roles, aquí las columnas de borrado sí están mapeadas.
        var actor = _currentUser.UserName ?? "SYSTEM";
        link.IsDeleted = true;
        link.DeletedAt = _clock.UtcNow;
        link.DeletedBy = actor;
        link.IsDefault = false;
        link.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
