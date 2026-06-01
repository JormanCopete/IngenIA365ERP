using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Memberships.PromoteToTenantAdmin;

public sealed class PromoteToTenantAdminCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    IMembershipChangedNotifier membershipNotifier,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<PromoteToTenantAdminCommandHandler> logger)
    : IRequestHandler<PromoteToTenantAdminCommand, Result>
{
    public async Task<Result> Handle(PromoteToTenantAdminCommand request, CancellationToken ct)
    {
        var guard = await Authz.EnsureTenantAdminOrMasterAsync(
            currentUser, adminDb, request.TenantPublicId, ct);
        if (!guard.IsAllowed) return Result.Failure(guard.ErrorCode!, guard.Message!);

        var membership = await adminDb.TenantMemberships
            .FirstOrDefaultAsync(
                m => m.PublicId == request.MembershipPublicId
                  && m.TenantId == request.TenantPublicId, ct);
        if (membership is null)
            return Result.Failure("Membership.NotFound", "La membresía no existe.");

        try
        {
            membership.PromoteToAdmin();
        }
        catch (Exception ex)
        {
            return Result.Failure("Membership.InvalidTransition", ex.Message);
        }

        await adminDb.SaveChangesAsync(ct);
        await membershipNotifier.PublishAsync(membership.CentralUserId, ct);

        var now = clock.UtcNow;
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: membership.TenantId.ToString("N"),
                UserId: currentUser.CentralUserId!.Value.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: AuditEventTypes.MembershipPromotedToAdmin,
                EntityType: nameof(TenantMembership),
                EntityPublicId: membership.CentralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: new[] { "IsTenantAdmin" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/tenants/{tenantPublicId}/members/{publicId}/promote-admin",
                HttpMethod: "POST", HttpStatusCode: 204, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló");
        }

        return Result.Success();
    }
}
