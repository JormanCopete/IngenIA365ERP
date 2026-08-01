using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Invitations.Services;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Invitations.IssueMasterInvitation;

/// <summary>
/// T052 — Análogo a <c>IssueTenantInvitationCommandHandler</c> (T051) pero
/// exclusivo para master admin. Permite <c>InviteAsTenantAdmin = true</c>
/// (FR-025/FR-026). Defensa en profundidad: además del filter
/// <c>RequireMasterAdmin</c> del endpoint, el handler vuelve a verificar
/// la claim <c>is_global_master_admin</c>.
/// </summary>
public sealed class IssueMasterInvitationCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext db,
    ISecureTokenGenerator tokens,
    IInvitationEmailDispatcher emailDispatcher,
    IDateTimeService clock,
    IOptions<IdentityEmailOptions> identityEmailOptions,
    ILogger<IssueMasterInvitationCommandHandler> logger)
    : IRequestHandler<IssueMasterInvitationCommand, Result<IssueMasterInvitationResult>>
{
    private readonly IdentityEmailOptions _identityEmailOptions = identityEmailOptions.Value;

    public async Task<Result<IssueMasterInvitationResult>> Handle(
        IssueMasterInvitationCommand request, CancellationToken ct)
    {
        var inviterCentralUserId = currentUser.CentralUserId;
        if (inviterCentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<IssueMasterInvitationResult>(
                "Identity.Unauthenticated",
                "Se requiere autenticación válida.");
        }

        if (!currentUser.IsGlobalMasterAdmin)
        {
            return Result.Failure<IssueMasterInvitationResult>(
                "Invitation.MasterOnly",
                "Solo el administrador global del SaaS puede emitir invitaciones desde este endpoint.");
        }

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == request.TenantPublicId && t.IsActive, ct);
        if (tenant is null)
        {
            return Result.Failure<IssueMasterInvitationResult>(
                "Tenant.NotFound",
                $"La empresa indicada (Id={request.TenantPublicId}) no existe o está inactiva.");
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var displayEmail = request.Email.Trim();
        var now = clock.UtcNow;

        var previous = await db.Invitations
            .Where(i => i.TenantId == tenant.PublicId
                     && i.NormalizedEmail == normalizedEmail
                     && i.Status == InvitationStatus.Pending)
            .ToListAsync(ct);
        foreach (var p in previous) p.MarkSuperseded();

        var (plainToken, hash) = tokens.Generate();
        var expiresAt = now.AddDays(_identityEmailOptions.InvitationLifetimeDays);

        var invitation = Invitation.Create(
            email: displayEmail,
            normalizedEmail: normalizedEmail,
            tenantId: tenant.PublicId,
            invitedByUserId: inviterCentralUserId.Value,
            inviteAsTenantAdmin: request.InviteAsTenantAdmin,
            tokenHash: hash,
            createdAt: now,
            expiresAt: expiresAt);

        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        await emailDispatcher.DispatchAsync(new InvitationEmailRequest(
            Invitation: invitation,
            Tenant: tenant,
            InviterDisplayName: currentUser.Email ?? "Master admin",
            PlainTokenBase64Url: plainToken), ct);

        logger.LogInformation(
            "Invitación master {InvitationId} emitida por {Inviter} para {Email} → tenant {Tenant} " +
            "(asTenantAdmin={AsAdmin}, expira {Expires}).",
            invitation.PublicId, inviterCentralUserId, displayEmail, tenant.Name,
            request.InviteAsTenantAdmin, expiresAt);

        return Result.Success(new IssueMasterInvitationResult(
            InvitationPublicId: invitation.PublicId,
            ExpiresAt: expiresAt,
            InviteAsTenantAdmin: request.InviteAsTenantAdmin));
    }
}
