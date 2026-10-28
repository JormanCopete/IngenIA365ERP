using IngenIA365ERP.Application.Common.Audit;
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
    ILogger<IssueMasterInvitationCommandHandler> logger,
    EmisionDeInvitacion emision,
    Identity.AuditoriaDeIdentidad auditoria)
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

        // Feature 017: documento, identidad, destino, rol y cargo en un solo sitio (research R7).
        var preparada = await emision.PrepararAsync(
            tenant, inviterCentralUserId.Value, request.InviteAsTenantAdmin, request.Datos(), ct);
        if (preparada.IsFailure)
            return Result.Failure<IssueMasterInvitationResult>(preparada.Error);

        var invitation = preparada.Value.Invitacion;
        var expiresAt = invitation.ExpiresAt;
        await db.SaveChangesAsync(ct);

        await auditoria.EnCooperativaAsync(
            tenant.PublicId, AuditEventTypes.InvitationIssuedByMaster, inviterCentralUserId.Value, currentUser.Email,
            nameof(Invitation), invitation.PublicId, null,
            new { invitation.DocumentType, ConDocumento = invitation.RequiresDocument, request.InviteAsTenantAdmin },
            clock.UtcNow, ct);

        await emailDispatcher.DispatchAsync(new InvitationEmailRequest(
            Invitation: invitation,
            Tenant: tenant,
            InviterDisplayName: currentUser.Email ?? "Master admin",
            PlainTokenBase64Url: preparada.Value.TokenPlano,
            RecipientDisplayName: preparada.Value.NombreDelDestinatario,
            ExistingIdentity: preparada.Value.IdentidadExistente), ct);

        logger.LogInformation(
            "Invitación master {InvitationId} emitida por {Inviter} → tenant {Tenant} " +
            "(asTenantAdmin={AsAdmin}, expira {Expires}).",
            invitation.PublicId, inviterCentralUserId, tenant.Name,
            request.InviteAsTenantAdmin, expiresAt);

        return Result.Success(new IssueMasterInvitationResult(
            InvitationPublicId: invitation.PublicId,
            ExpiresAt: expiresAt,
            InviteAsTenantAdmin: request.InviteAsTenantAdmin,
            Message: IssueTenantInvitation.MensajesDeInvitacion.Enviada));
    }
}
