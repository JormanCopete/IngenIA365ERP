using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.SetDefaultBranch;

public sealed class SetDefaultBranchCommandHandler : IRequestHandler<SetDefaultBranchCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IAdminDbContext _admin;
    private readonly ICurrentTenantService _cooperativa;
    private readonly ICurrentUserService _currentUser;

    public SetDefaultBranchCommandHandler(
        IApplicationDbContext db,
        IAdminDbContext admin,
        ICurrentTenantService cooperativa,
        ICurrentUserService currentUser)
    {
        _db = db;
        _admin = admin;
        _cooperativa = cooperativa;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SetDefaultBranchCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        var cooperativaId = await CooperativaActiva.IdAsync(_admin, _cooperativa, ct);
        var branch = cooperativaId is null ? null : await _admin.TenantBranches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId && b.TenantId == cooperativaId, ct);
        if (branch is null) return Result.Failure("Generic.NotFound", "La sucursal no existe.");

        var vivas = await _db.UserBranchAssignments
            .Where(a => a.UserId == user.Id && !a.IsDeleted)
            .ToListAsync(ct);
        var link = vivas.FirstOrDefault(a => a.BranchId == branch.Id);
        if (link is null)
        {
            return Result.Failure(UserErrorCodes.NotAssignedBranch,
                "El usuario no tiene asignada esa oficina.");
        }
        if (link.IsDefault) return Result.Success();

        var actor = _currentUser.UserName ?? "SYSTEM";

        // Dos guardados y no uno: el índice único filtrado (usuario, cooperativa, por defecto) se comprueba fila a
        // fila, y en un solo lote nada garantiza que la rebaja vaya antes que la subida.
        var anteriores = vivas.Where(a => a.IsDefault).ToList();
        if (anteriores.Count > 0)
        {
            foreach (var o in anteriores)
            {
                o.IsDefault = false;
                o.UpdatedBy = actor;
            }
            await _db.SaveChangesAsync(ct);
        }

        link.IsDefault = true;
        link.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
