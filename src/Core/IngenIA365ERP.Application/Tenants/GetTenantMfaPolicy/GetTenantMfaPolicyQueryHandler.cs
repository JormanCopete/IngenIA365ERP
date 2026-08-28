using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Memberships;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tenants.GetTenantMfaPolicy;

public sealed class GetTenantMfaPolicyQueryHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb)
    : IRequestHandler<GetTenantMfaPolicyQuery, Result<TenantMfaPolicyDto>>
{
    public async Task<Result<TenantMfaPolicyDto>> Handle(
        GetTenantMfaPolicyQuery request, CancellationToken ct)
    {
        var guard = await Authz.EnsureTenantAdminOrMasterAsync(
            currentUser, adminDb, request.TenantPublicId, ct);
        if (!guard.IsAllowed)
            return Result.Failure<TenantMfaPolicyDto>(guard.ErrorCode!, guard.Message!);

        var policy = await adminDb.TenantMfaPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == request.TenantPublicId, ct);

        if (policy is null)
        {
            return Result.Success(new TenantMfaPolicyDto(
                TenantPublicId: request.TenantPublicId,
                IsRequired: false,
                ActivatedAt: null,
                DeactivatedAt: null,
                // Sin fila no hay restriccion. Mismo criterio que el lector de
                // membresias: «no hay politica» no puede significar «no acepta
                // nada» en un sitio y «no exige» en el otro.
                MetodosAceptados: ConversionDeMetodosMfa.ALiterales(
                    ConversionDeMetodosMfa.Todos)));
        }

        return Result.Success(new TenantMfaPolicyDto(
            TenantPublicId: policy.TenantId,
            IsRequired: policy.IsRequired,
            ActivatedAt: policy.ActivatedAt,
            DeactivatedAt: policy.DeactivatedAt,
            MetodosAceptados: ConversionDeMetodosMfa.ALiterales(policy.AllowedMethodsMask)));
    }
}
