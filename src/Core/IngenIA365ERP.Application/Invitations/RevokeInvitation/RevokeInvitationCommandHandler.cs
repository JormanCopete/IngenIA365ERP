using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Invitations.RevokeInvitation;

public sealed class RevokeInvitationCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext db,
    IDateTimeService clock,
    ILogger<RevokeInvitationCommandHandler> logger)
    : IRequestHandler<RevokeInvitationCommand, Result>
{
    public async Task<Result> Handle(RevokeInvitationCommand request, CancellationToken ct)
    {
        var callerCentralUserId = currentUser.CentralUserId;
        if (callerCentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure("Identity.Unauthenticated", "Se requiere autenticación válida.");
        }

        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.PublicId == request.InvitationPublicId, ct);
        if (invitation is null)
        {
            return Result.Failure("Invitation.NotFound", "La invitación no existe.");
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            // Mensaje específico por estado terminal — útil para el admin que
            // intenta revocar algo ya consumido o expirado.
            var reason = invitation.Status switch
            {
                InvitationStatus.Accepted => "ya fue aceptada",
                InvitationStatus.Expired => "ya expiró",
                InvitationStatus.Revoked => "ya estaba revocada",
                InvitationStatus.Superseded => "fue reemplazada por una invitación más reciente",
                _ => "no está en un estado revocable",
            };
            return Result.Failure(
                "Invitation.NotPending",
                $"La invitación no se puede revocar porque {reason}.");
        }

        // Autorización: master admin pasa siempre; resto debe ser tenant admin
        // activo del MISMO tenant invitante.
        if (!currentUser.IsGlobalMasterAdmin)
        {
            var isAdminOfTenant = await db.TenantMemberships
                .AsNoTracking()
                .AnyAsync(m => m.CentralUserId == callerCentralUserId.Value
                            && m.TenantId == invitation.TenantId
                            && m.Status == MembershipStatus.Active
                            && m.IsTenantAdmin, ct);
            if (!isAdminOfTenant)
            {
                return Result.Failure(
                    "Invitation.Forbidden",
                    "Solo un administrador activo de la empresa invitante (o el master admin) puede revocar esta invitación.");
            }
        }

        invitation.Revoke(callerCentralUserId.Value, clock.UtcNow);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Invitación {InvitationId} revocada por {Caller}.",
            invitation.PublicId, callerCentralUserId);

        return Result.Success();
    }
}
