using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Memberships.ActivateMembership;

public sealed class ActivateMembershipCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    IMembershipChangedNotifier membershipNotifier,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<ActivateMembershipCommandHandler> logger)
    : IRequestHandler<ActivateMembershipCommand, Result>
{
    public async Task<Result> Handle(ActivateMembershipCommand request, CancellationToken ct)
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

        var now = clock.UtcNow;
        try
        {
            if (membership.Status == MembershipStatus.Suspended) membership.Reactivate(now);
            else membership.Activate(now);
        }
        catch (Exception ex)
        {
            return Result.Failure("Membership.InvalidTransition", ex.Message);
        }

        await adminDb.SaveChangesAsync(ct);
        await membershipNotifier.PublishAsync(membership.CentralUserId, ct);

        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: membership.TenantId.ToString("N"),
                UserId: currentUser.CentralUserId!.Value.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: AuditEventTypes.MembershipActivatedFromSuspension,
                EntityType: nameof(TenantMembership),
                EntityPublicId: membership.CentralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: new[] { "Status" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/tenants/{tenantPublicId}/members/{publicId}/activate",
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
