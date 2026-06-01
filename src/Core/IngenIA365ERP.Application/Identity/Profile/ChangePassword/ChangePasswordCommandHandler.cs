using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.ChangePassword;

public sealed class ChangePasswordCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IPasswordChangedNotifier notifier,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<ChangePasswordCommandHandler> logger)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure("Identity.WrongTokenPurpose",
                "Este endpoint requiere purpose=full.");

        var centralUserId = currentUser.CentralUserId.Value;

        // ICentralIdentityProvider.ChangePasswordAsync:
        //  - valida currentPassword
        //  - corre Pwned check sobre newPassword
        //  - re-hash con BCrypt cost 11
        //  - regenera SecurityStamp → invalida refresh tokens activos
        var result = await centralIdentity.ChangePasswordAsync(
            centralUserId, request.CurrentPassword, request.NewPassword, ct);
        if (!result.Succeeded)
        {
            var code = result.ErrorCodes.FirstOrDefault() ?? "Profile.Password.ChangeFailed";
            return Result.Failure(code, MapErrorMessage(code));
        }

        var now = clock.UtcNow;

        // Notificación de seguridad — fail-soft.
        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is not null)
        {
            try
            {
                await notifier.NotifyAsync(
                    toEmail: user.Email,
                    recipientName: user.Email,
                    occurredAt: now,
                    ipAddress: request.IpAddress,
                    userAgent: request.UserAgent,
                    ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "PasswordChangedNotifier falló para {Email}; el cambio quedó aplicado.",
                    user.Email);
            }
        }

        await EmitAuditAsync(centralUserId, user?.Email ?? string.Empty,
            AuditEventTypes.ProfilePasswordChanged, request, now, ct);

        return Result.Success();
    }

    private async Task EmitAuditAsync(
        Guid centralUserId, string email, string action,
        ChangePasswordCommand request, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: action,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: request.IpAddress,
                UserAgent: request.UserAgent,
                Endpoint: "/api/profile/password",
                HttpMethod: "POST",
                HttpStatusCode: 204,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}", action);
        }
    }

    private static string MapErrorMessage(string code) => code switch
    {
        "Identity.Password.Pwned" =>
            "La contraseña aparece en filtraciones públicas; elige otra.",
        "Identity.PasswordMismatch" or "Identity.InvalidCredentials" =>
            "La contraseña actual no es correcta.",
        _ => "No se pudo cambiar la contraseña.",
    };
}
