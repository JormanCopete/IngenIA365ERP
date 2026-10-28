using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity;
using IngenIA365ERP.Application.Identity.Correccion.CorregirDocumento;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.SendRecoveryLink;

/// <summary>
/// Feature 017 (US7, T080, D-12): el administrador de la cooperativa ya no fija la clave de nadie
/// (<c>reset-password</c> se retiró sin alias): le manda a la persona el mismo enlace de recuperación del
/// «olvidé mi contraseña», y sólo a su correo verificado. Así la clave nueva la conoce sólo la persona. Queda
/// el evento <c>Identity.RecoveryLinkSent</c> en la auditoría de la cooperativa.
/// </summary>
public sealed record SendRecoveryLinkCommand(Guid UserPublicId) : IRequest<Result>;

public sealed class SendRecoveryLinkCommandHandler(
    ICurrentCentralUserContext currentUser,
    IApplicationDbContext cooperativa,
    ICentralIdentityProvider centralIdentity,
    ICanalDeRecuperacion canal,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock)
    : IRequestHandler<SendRecoveryLinkCommand, Result>
{
    public async Task<Result> Handle(SendRecoveryLinkCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is not { } actor || currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure("Identity.Unauthenticated", "Inicie sesión de nuevo.");

        var persona = await IdentidadDelUsuario.ResolverAsync(cooperativa, centralIdentity, request.UserPublicId, ct);
        var cuenta = persona is { } id ? await centralIdentity.FindByIdAsync(id, ct) : null;
        if (cuenta is null || cuenta.Status == CentralUserStatus.Disabled)
            return Result.Failure("Generic.NotFound", "El usuario no existe.");
        if (!cuenta.EmailConfirmed)
            return Result.Failure(
                "Identity.Email.NotVerified",
                "El correo de esta persona no está verificado: no se le puede enviar el enlace. Pídale que acepte su invitación.");

        await canal.EnviarEnlaceAsync(cuenta, null, ct);

        if (currentUser.ActiveTenantPublicId is { } coop)
            await auditoria.EnCooperativaAsync(
                coop, AuditEventTypes.IdentityRecoveryLinkSent, actor, currentUser.Email, nameof(CentralUser), cuenta.Id,
                null, new { Destino = cuenta.Email }, clock.UtcNow, ct);
        return Result.Success();
    }
}
