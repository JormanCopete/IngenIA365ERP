using IngenIA365ERP.Application.Admin.Branches.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Branches.CreateBranch;

public sealed class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Result<Guid>>
{
    private readonly IAdminDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateBranchCommandHandler(IAdminDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateBranchCommand request, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .Where(t => t.PublicId == request.TenantPublicId)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(ct);
        if (tenant is null) return Result.Failure<Guid>("Generic.NotFound", "Cooperativa no encontrada.");

        if (await _db.TenantBranches
                .AnyAsync(b => b.TenantId == tenant.Id && b.Code == request.Code, ct))
        {
            return Result.Failure<Guid>(BranchErrorCodes.CodeTaken,
                $"Ya existe una sucursal con código '{request.Code}' en esta cooperativa.");
        }

        if (request.IsHeadquarters && await _db.TenantBranches
                .AnyAsync(b => b.TenantId == tenant.Id && b.IsHeadquarters, ct))
        {
            return Result.Failure<Guid>(BranchErrorCodes.HeadquartersExists,
                "La cooperativa ya tiene una sucursal matriz.");
        }

        var actor = _currentUser.UserName ?? "SYSTEM";
        var branch = new TenantBranch
        {
            TenantId = tenant.Id,
            Code = request.Code,
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            IsActive = true,
            IsHeadquarters = request.IsHeadquarters,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        _db.TenantBranches.Add(branch);
        await _db.SaveChangesAsync(ct);

        return Result.Success(branch.PublicId);
    }
}
