using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Memberships.RevokeMembership;

/// <summary>
/// T095 — Revoca una membresía. Si el target es admin y es el ÚLTIMO admin
/// activo del tenant, rechaza con <c>Membership.LastAdminProtected</c>
/// (research D-09). Publica IMembershipChangedNotifier al éxito.
/// </summary>
public sealed class RevokeMembershipCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    IMembershipChangedNotifier membershipNotifier,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<RevokeMembershipCommandHandler> logger)
    : IRequestHandler<RevokeMembershipCommand, Result>
{
    public async Task<Result> Handle(RevokeMembershipCommand request, CancellationToken ct)
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

        // Salvaguarda del último admin.
        if (membership.IsTenantAdmin && membership.Status == MembershipStatus.Active)
        {
            var otherActiveAdmins = await adminDb.TenantMemberships
                .CountAsync(m => m.TenantId == request.TenantPublicId
                              && m.PublicId != membership.PublicId
                              && m.Status == MembershipStatus.Active
                              && m.IsTenantAdmin, ct);
            if (otherActiveAdmins == 0)
            {
                return Result.Failure(
                    "Membership.LastAdminProtected",
                    "No puedes revocar al único administrador activo de la empresa.");
            }
        }

        var now = clock.UtcNow;
        try
        {
            membership.Revoke(byUserId: currentUser.CentralUserId!.Value, now);
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
                Action: AuditEventTypes.MembershipRevoked,
                EntityType: nameof(TenantMembership),
                EntityPublicId: membership.CentralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: new[] { "Status" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/tenants/{tenantPublicId}/members/{publicId}/revoke",
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
