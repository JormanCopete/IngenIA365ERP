using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.AdminResetPassword;

public sealed class AdminResetPasswordCommandHandler : IRequestHandler<AdminResetPasswordCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordPolicyEnforcer _passwords;
    private readonly IDateTimeService _clock;
    private readonly IRefreshTokenStore? _refreshStore;
    private readonly ISender _mediator;
    private readonly ICentralIdentityProvider _identidadCentral;

    public AdminResetPasswordCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPasswordPolicyEnforcer passwords,
        IDateTimeService clock,
        ISender mediator,
        ICentralIdentityProvider identidadCentral,
        IRefreshTokenStore? refreshStore = null)
    {
        _identidadCentral = identidadCentral;
        _db = db;
        _currentUser = currentUser;
        _passwords = passwords;
        _clock = clock;
        _mediator = mediator;
        _refreshStore = refreshStore;
    }

    public async Task<Result> Handle(AdminResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        int? tenantId = int.TryParse(_currentUser.TenantId, out var tid) ? tid : null;

        var policy = await _passwords.ValidateAsync(request.NewPassword, tenantId, ct);
        if (policy.IsFailure) return policy;

        var notReused = await _passwords.EnsureNotReusedAsync(user.Id, request.NewPassword, tenantId, ct);
        if (notReused.IsFailure) return notReused;

        var now = _clock.UtcNow;
        var actor = _currentUser.UserName ?? "SYSTEM";

        // La contraseña se cambia en la identidad CENTRAL, que es contra la que
        // valida el acceso.
        //
        // Antes se escribía sólo aquí, en SEC_Users, y el acceso no consulta esa
        // tabla: el botón respondía "listo", el hash cambiaba, y la persona seguía
        // entrando con la contraseña vieja. La afectada creía tener una nueva y
        // quien la restableció creía haber cortado el acceso. Ninguna de las dos
        // cosas era cierta.
        //
        // AdminResetPasswordAsync además regenera el SecurityStamp, lo que invalida
        // los tokens de refresco centrales — o sea que sí corta las sesiones.
        var correo = user.Email ?? user.Username;
        var central = string.IsNullOrWhiteSpace(correo)
            ? null
            : await _identidadCentral.FindByEmailAsync(correo, ct);

        if (central is null)
        {
            return Result.Failure(
                "Identity.CentralUserNotFound",
                "Esta persona no tiene identidad central, así que no hay contraseña que " +
                "restablecer. Revisá que la fila de SEC_Users corresponda a alguien que " +
                "aceptó una invitación.");
        }

        var cambio = await _identidadCentral.AdminResetPasswordAsync(
            central.Id, request.NewPassword, ct);

        if (!cambio.Succeeded)
        {
            return Result.Failure(
                "Identity.PasswordResetFailed",
                cambio.ErrorCodes.Count > 0
                    ? string.Join(", ", cambio.ErrorCodes)
                    : "No se pudo restablecer la contraseña en la identidad central.");
        }

        // SEC_Users conserva su centinela: su columna de contraseña no la consulta
        // nadie para autenticar, y escribir ahí un hash real haria creer que si.
        user.LastPasswordChangeAt = now;
        user.MustChangePassword = true;
        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;
        user.UpdatedBy = actor;

        _db.PasswordHistory.Add(new PasswordHistory
        {
            UserId = user.Id,
            PasswordHash = user.PasswordHash,
            SetAt = now
        });

        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        var families = new HashSet<Guid>();
        foreach (var t in activeTokens)
        {
            t.RevokedAt = now;
            t.RevocationReason = "AdminRevoke";
            families.Add(t.FamilyId);
        }

        await _db.SaveChangesAsync(ct);
        await _passwords.TrimHistoryAsync(user.Id, tenantId, ct);

        if (_refreshStore is not null)
        {
            foreach (var fam in families)
            {
                await _refreshStore.InvalidateFamilyAsync(fam.ToString(), ct);
            }
        }

        await _mediator.Send(new SendNotificationCommand(new NotificationPayload(
            RecipientUserPublicId: user.PublicId,
            Type: NotificationType.PasswordChanged,
            Subject: "Un administrador restableció tu contraseña",
            Body: "Tu contraseña fue restablecida por un administrador. " +
                  "Al ingresar deberás cambiarla. Si no esperabas este cambio, " +
                  "contacta inmediatamente al equipo de seguridad.",
            Channels: NotificationChannels.InApp | NotificationChannels.Email)), ct);

        return Result.Success();
    }
}
