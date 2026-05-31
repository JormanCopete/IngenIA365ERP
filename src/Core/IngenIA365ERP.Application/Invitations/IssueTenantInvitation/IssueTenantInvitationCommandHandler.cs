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

namespace IngenIA365ERP.Application.Invitations.IssueTenantInvitation;

/// <summary>
/// T051 — Verifica autoría, marca invitaciones previas Pending del mismo
/// (Email, Tenant) como Superseded, genera token + hash via
/// <see cref="ISecureTokenGenerator"/>, persiste la nueva
/// <see cref="Invitation"/> y dispara el correo via
/// <see cref="IInvitationEmailDispatcher"/>.
///
/// <para>
/// El token plano vive únicamente en memoria durante la ejecución del
/// handler y se entrega al dispatcher; nunca se persiste ni se retorna al
/// caller. El hash SHA-256 es lo único que queda en
/// <c>ADM_Invitations.TokenHash</c>.
/// </para>
/// </summary>
public sealed class IssueTenantInvitationCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext db,
    ISecureTokenGenerator tokens,
    IInvitationEmailDispatcher emailDispatcher,
    IDateTimeService clock,
    IOptions<IdentityEmailOptions> identityEmailOptions,
    ILogger<IssueTenantInvitationCommandHandler> logger)
    : IRequestHandler<IssueTenantInvitationCommand, Result<IssueTenantInvitationResult>>
{
    private readonly IdentityEmailOptions _identityEmailOptions = identityEmailOptions.Value;

    public async Task<Result<IssueTenantInvitationResult>> Handle(
        IssueTenantInvitationCommand request, CancellationToken ct)
    {
        // 1) Caller autenticado con JWT central.
        var inviterCentralUserId = currentUser.CentralUserId;
        if (inviterCentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<IssueTenantInvitationResult>(
                "Identity.Unauthenticated",
                "Se requiere autenticación válida.");
        }

        // 2) Resolver tenant por PublicId.
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == request.TenantPublicId && t.IsActive, ct);
        if (tenant is null)
        {
            return Result.Failure<IssueTenantInvitationResult>(
                "Tenant.NotFound",
                $"La empresa indicada (Id={request.TenantPublicId}) no existe o está inactiva.");
        }

        // 3) Autorización: master admin global pasa siempre; resto debe ser
        //    tenant admin activo del tenant invitante.
        if (!currentUser.IsGlobalMasterAdmin)
        {
            var membership = await db.TenantMemberships
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    m => m.CentralUserId == inviterCentralUserId.Value
                      && m.TenantId == tenant.PublicId
                      && m.Status == MembershipStatus.Active
                      && m.IsTenantAdmin,
                    ct);
            if (membership is null)
            {
                return Result.Failure<IssueTenantInvitationResult>(
                    "Invitation.Forbidden",
                    "Solo un administrador activo de la empresa puede emitir invitaciones.");
            }
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var displayEmail = request.Email.Trim();
        var now = clock.UtcNow;

        // 4) Superseder invitaciones previas Pending del mismo (email, tenant)
        //    para evitar tokens activos en paralelo y confusión del destinatario.
        var previous = await db.Invitations
            .Where(i => i.TenantId == tenant.PublicId
                     && i.NormalizedEmail == normalizedEmail
                     && i.Status == InvitationStatus.Pending)
            .ToListAsync(ct);
        foreach (var p in previous)
        {
            p.MarkSuperseded();
        }

        // 5) Generar token + hash, crear Invitation.
        var (plainToken, hash) = tokens.Generate();
        var expiresAt = now.AddDays(_identityEmailOptions.InvitationLifetimeDays);

        var invitation = Invitation.Create(
            email: displayEmail,
            normalizedEmail: normalizedEmail,
            tenantId: tenant.PublicId,
            invitedByUserId: inviterCentralUserId.Value,
            inviteAsTenantAdmin: false, // por command Tenant siempre miembro regular (FR-025/FR-026)
            tokenHash: hash,
            createdAt: now,
            expiresAt: expiresAt);

        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        // 6) Enviar correo. El dispatcher arma el enlace con el token plano y
        //    el subject. Cualquier excepción al enviar propaga — la invitación
        //    queda persistida y el admin verá en logs que el correo falló.
        await emailDispatcher.DispatchAsync(new InvitationEmailRequest(
            Invitation: invitation,
            Tenant: tenant,
            InviterDisplayName: currentUser.Email ?? "Administrador",
            PlainTokenBase64Url: plainToken), ct);

        logger.LogInformation(
            "Invitación {InvitationId} emitida por {Inviter} para {Email} → tenant {Tenant} (expira {Expires}).",
            invitation.PublicId, inviterCentralUserId, displayEmail, tenant.Name, expiresAt);

        return Result.Success(new IssueTenantInvitationResult(
            InvitationPublicId: invitation.PublicId,
            ExpiresAt: expiresAt));
    }
}
