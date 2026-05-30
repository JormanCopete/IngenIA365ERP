using IngenIA365ERP.Application.Admin.Tenants.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Tenants.ActivateTenant;

public sealed class ActivateTenantCommandHandler : IRequestHandler<ActivateTenantCommand, Result>
{
    private readonly IAdminDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public ActivateTenantCommandHandler(
        IAdminDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(ActivateTenantCommand request, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.PublicId == request.TenantPublicId, ct);
        if (tenant is null) return Result.Failure("Generic.NotFound", "Cooperativa no encontrada.");

        if (tenant.IsActive && tenant.SuspendedAt is null)
        {
            return Result.Failure(TenantErrorCodes.AlreadyActive,
                "La cooperativa ya está activa.");
        }

        tenant.IsActive = true;
        tenant.SuspendedAt = null;
        tenant.ActivatedAt ??= _clock.UtcNow;
        tenant.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
