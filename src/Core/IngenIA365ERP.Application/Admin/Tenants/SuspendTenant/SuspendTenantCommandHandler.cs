using IngenIA365ERP.Application.Admin.Tenants.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Tenants.SuspendTenant;

public sealed class SuspendTenantCommandHandler : IRequestHandler<SuspendTenantCommand, Result>
{
    private readonly IAdminDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public SuspendTenantCommandHandler(
        IAdminDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(SuspendTenantCommand request, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.PublicId == request.TenantPublicId, ct);
        if (tenant is null) return Result.Failure("Generic.NotFound", "Cooperativa no encontrada.");

        if (!tenant.IsActive || tenant.SuspendedAt is not null)
        {
            return Result.Failure(TenantErrorCodes.AlreadySuspended,
                "La cooperativa ya está suspendida.");
        }

        tenant.IsActive = false;
        tenant.SuspendedAt = _clock.UtcNow;
        tenant.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
