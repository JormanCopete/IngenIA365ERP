using IngenIA365ERP.Application.Admin.Tenants.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Tenants.RegisterTenant;

public sealed class RegisterTenantCommandHandler
    : IRequestHandler<RegisterTenantCommand, Result<Guid>>
{
    private readonly IAdminDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public RegisterTenantCommandHandler(
        IAdminDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(RegisterTenantCommand request, CancellationToken ct)
    {
        // Unicidad — Nit global, Subdomain global, SchemaName global.
        if (await _db.Tenants.IgnoreQueryFilters()
                .AnyAsync(t => t.Nit == request.Nit, ct))
        {
            return Result.Failure<Guid>(TenantErrorCodes.NitTaken,
                $"Ya existe una cooperativa con NIT '{request.Nit}'.");
        }
        if (!string.IsNullOrWhiteSpace(request.Subdomain)
            && await _db.Tenants.IgnoreQueryFilters()
                .AnyAsync(t => t.Subdomain == request.Subdomain, ct))
        {
            return Result.Failure<Guid>(TenantErrorCodes.SubdomainTaken,
                $"El subdominio '{request.Subdomain}' ya está en uso.");
        }
        if (await _db.Tenants.IgnoreQueryFilters()
                .AnyAsync(t => t.SchemaName == request.SchemaName, ct))
        {
            return Result.Failure<Guid>(TenantErrorCodes.SchemaTaken,
                $"El schema '{request.SchemaName}' ya está en uso.");
        }

        var actor = _currentUser.UserName ?? "SYSTEM";
        var tenant = new Tenant
        {
            Name = request.Name,
            SchemaName = request.SchemaName,
            Subdomain = request.Subdomain,
            Nit = request.Nit,
            LegalName = request.LegalName,
            LegalAddress = request.LegalAddress,
            TaxRegime = request.TaxRegime,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            PlanType = request.PlanType,
            MaxUsers = request.MaxUsers,
            StorageLimitMb = request.StorageLimitMb,
            IsActive = true,
            ActivatedAt = _clock.UtcNow,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        return Result.Success(tenant.PublicId);
    }
}
