using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Memberships;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Tenants.UpdateTenantMfaPolicy;

public sealed class UpdateTenantMfaPolicyCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    IMembershipChangedNotifier membershipNotifier,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<UpdateTenantMfaPolicyCommandHandler> logger)
    : IRequestHandler<UpdateTenantMfaPolicyCommand, Result>
{
    public async Task<Result> Handle(UpdateTenantMfaPolicyCommand request, CancellationToken ct)
    {
        var guard = await Authz.EnsureTenantAdminOrMasterAsync(
            currentUser, adminDb, request.TenantPublicId, ct);
        if (!guard.IsAllowed) return Result.Failure(guard.ErrorCode!, guard.Message!);

        var tenant = await adminDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == request.TenantPublicId && t.IsActive, ct);
        if (tenant is null)
            return Result.Failure("Tenant.NotFound", "La empresa no existe o está inactiva.");

        var policy = await adminDb.TenantMfaPolicies
            .FirstOrDefaultAsync(p => p.TenantId == request.TenantPublicId, ct);

        var now = clock.UtcNow;
        var actor = currentUser.CentralUserId!.Value;
        string action;

        if (policy is null)
        {
            policy = TenantMfaPolicy.CreateForTenant(request.TenantPublicId);
            adminDb.TenantMfaPolicies.Add(policy);
        }

        if (policy.IsRequired == request.IsRequired)
            return Result.Success(); // idempotente

        if (request.IsRequired)
        {
            policy.Enable(actor, now);
            action = AuditEventTypes.TenantMfaPolicyActivated;
        }
        else
        {
            policy.Disable(actor, now);
            action = AuditEventTypes.TenantMfaPolicyDeactivated;
        }

        await adminDb.SaveChangesAsync(ct);

        // Invalidar caché de membresías de TODOS los usuarios del tenant.
        await membershipNotifier.PublishForTenantMembersAsync(request.TenantPublicId, ct);

        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: request.TenantPublicId.ToString("N"),
                UserId: actor.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: action,
                EntityType: nameof(TenantMfaPolicy),
                EntityPublicId: request.TenantPublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: new[] { "IsRequired" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/tenants/{tenantPublicId}/mfa-policy",
                HttpMethod: "PUT", HttpStatusCode: 204, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}", action);
        }

        return Result.Success();
    }
}
