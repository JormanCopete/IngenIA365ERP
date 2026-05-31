using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Invitations.PreviewInvitation;

/// <summary>
/// T053 — Resuelve token plano → hash → invitation. NO consume el token
/// (no lo marca como Accepted) ni emite eventos auditables. Es lectura pura
/// para que la UI decida qué pantalla pintar.
/// </summary>
public sealed class PreviewInvitationQueryHandler(
    IAdminDbContext db,
    ISecureTokenGenerator tokens,
    ICentralIdentityProvider centralIdentity,
    IDateTimeService clock)
    : IRequestHandler<PreviewInvitationQuery, Result<PreviewInvitationResult>>
{
    public async Task<Result<PreviewInvitationResult>> Handle(
        PreviewInvitationQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure<PreviewInvitationResult>(
                "Invitation.TokenMissing",
                "Falta el token de la invitación.");
        }

        var hash = tokens.HashPlainToken(request.Token);

        var invitation = await db.Invitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invitation is null)
        {
            return Result.Failure<PreviewInvitationResult>(
                "Invitation.NotFound",
                "El enlace no corresponde a ninguna invitación.");
        }

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == invitation.TenantId, ct);
        if (tenant is null)
        {
            // Defensa en profundidad: la invitación referencia un tenant que
            // ya no existe o fue soft-deleted. Tratamos como NotFound para no
            // revelar inconsistencias de datos al destinatario.
            return Result.Failure<PreviewInvitationResult>(
                "Invitation.NotFound",
                "El enlace no corresponde a ninguna invitación.");
        }

        var now = clock.UtcNow;
        var (isValid, errorCode) = ResolveValidity(invitation, now);

        var existing = await centralIdentity.FindByEmailAsync(invitation.Email, ct);
        var isExisting = existing is not null;

        return Result.Success(new PreviewInvitationResult(
            TenantPublicId: tenant.PublicId,
            TenantName: tenant.Name,
            Email: invitation.Email,
            IsExistingCentralUser: isExisting,
            InviteAsTenantAdmin: invitation.InviteAsTenantAdmin,
            ExpiresAt: invitation.ExpiresAt,
            IsValid: isValid,
            ErrorCode: errorCode));
    }

    private static (bool IsValid, string? ErrorCode) ResolveValidity(Invitation invitation, DateTime now) =>
        invitation.Status switch
        {
            InvitationStatus.Pending when invitation.ExpiresAt <= now => (false, "Invitation.Expired"),
            InvitationStatus.Pending => (true, null),
            InvitationStatus.Accepted => (false, "Invitation.AlreadyAccepted"),
            InvitationStatus.Expired => (false, "Invitation.Expired"),
            InvitationStatus.Revoked => (false, "Invitation.Revoked"),
            InvitationStatus.Superseded => (false, "Invitation.Superseded"),
            _ => (false, "Invitation.Unknown"),
        };
}
