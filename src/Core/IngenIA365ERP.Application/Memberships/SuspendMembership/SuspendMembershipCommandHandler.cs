using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Memberships.SuspendMembership;

public sealed class SuspendMembershipCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    IMembershipChangedNotifier membershipNotifier,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<SuspendMembershipCommandHandler> logger)
    : IRequestHandler<SuspendMembershipCommand, Result>
{
    public async Task<Result> Handle(SuspendMembershipCommand request, CancellationToken ct)
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
            membership.Suspend(byUserId: currentUser.CentralUserId!.Value, now);
        }
        catch (Exception ex)
        {
            return Result.Failure("Membership.InvalidTransition", ex.Message);
        }

        await adminDb.SaveChangesAsync(ct);
        await membershipNotifier.PublishAsync(membership.CentralUserId, ct);

        await EmitAuditAsync(
            currentUser.CentralUserId!.Value, currentUser.Email ?? string.Empty,
            membership.TenantId, membership.CentralUserId,
            AuditEventTypes.MembershipSuspended, now, ct);

        return Result.Success();
    }

    private async Task EmitAuditAsync(
        Guid actorCentralUserId, string actorEmail,
        Guid tenantId, Guid targetCentralUserId,
        string action, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: tenantId.ToString("N"),
                UserId: actorCentralUserId.ToString("N"),
                UserName: actorEmail,
                Action: action,
                EntityType: nameof(Domain.Entities.Admin.TenantMembership),
                EntityPublicId: targetCentralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: new[] { "Status" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/tenants/{tenantPublicId}/members/{publicId}/suspend",
                HttpMethod: "POST", HttpStatusCode: 204, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}", action);
        }
    }
}
