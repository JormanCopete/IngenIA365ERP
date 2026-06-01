using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Memberships.DemoteFromTenantAdmin;

/// <summary>
/// T097 — Degrada a un admin de empresa a miembro regular. Salvaguarda
/// transaccional (research D-09):
/// <list type="bullet">
///   <item>Si es el ÚNICO admin activo → <c>Membership.LastAdminProtected</c>.</item>
///   <item>Si es auto-degrade del último admin → <c>Membership.SelfDemoteBlocked.LastAdmin</c>.</item>
/// </list>
/// </summary>
public sealed class DemoteFromTenantAdminCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    IMembershipChangedNotifier membershipNotifier,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<DemoteFromTenantAdminCommandHandler> logger)
    : IRequestHandler<DemoteFromTenantAdminCommand, Result>
{
    public async Task<Result> Handle(DemoteFromTenantAdminCommand request, CancellationToken ct)
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

        if (!membership.IsTenantAdmin)
            return Result.Failure("Membership.NotAdmin", "Esta membresía no es admin.");

        var otherActiveAdmins = await adminDb.TenantMemberships
            .CountAsync(m => m.TenantId == request.TenantPublicId
                          && m.PublicId != membership.PublicId
                          && m.Status == MembershipStatus.Active
                          && m.IsTenantAdmin, ct);

        if (otherActiveAdmins == 0)
        {
            var isSelf = membership.CentralUserId == currentUser.CentralUserId;
            return Result.Failure(
                isSelf ? "Membership.SelfDemoteBlocked.LastAdmin"
                       : "Membership.LastAdminProtected",
                "No se puede degradar al único administrador activo de la empresa.");
        }

        membership.DemoteFromAdmin();
        await adminDb.SaveChangesAsync(ct);
        await membershipNotifier.PublishAsync(membership.CentralUserId, ct);

        var now = clock.UtcNow;
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: membership.TenantId.ToString("N"),
                UserId: currentUser.CentralUserId!.Value.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: AuditEventTypes.MembershipDemotedFromAdmin,
                EntityType: nameof(TenantMembership),
                EntityPublicId: membership.CentralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: new[] { "IsTenantAdmin" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/tenants/{tenantPublicId}/members/{publicId}/demote-admin",
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
