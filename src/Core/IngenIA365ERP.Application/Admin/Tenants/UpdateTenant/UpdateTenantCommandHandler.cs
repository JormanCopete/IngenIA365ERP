using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Tenants.UpdateTenant;

public sealed class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, Result>
{
    private readonly IAdminDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateTenantCommandHandler(IAdminDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateTenantCommand request, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.PublicId == request.TenantPublicId, ct);
        if (tenant is null) return Result.Failure("Generic.NotFound", "Cooperativa no encontrada.");

        tenant.Name = request.Name;
        tenant.LegalName = request.LegalName;
        tenant.LegalAddress = request.LegalAddress;
        tenant.TaxRegime = request.TaxRegime;
        tenant.ContactEmail = request.ContactEmail;
        tenant.ContactPhone = request.ContactPhone;
        tenant.PlanType = request.PlanType;
        tenant.MaxUsers = request.MaxUsers;
        tenant.StorageLimitMb = request.StorageLimitMb;
        tenant.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
