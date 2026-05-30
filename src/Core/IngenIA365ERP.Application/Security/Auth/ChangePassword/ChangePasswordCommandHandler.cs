using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.ChangePassword;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordPolicyEnforcer _passwords;
    private readonly IDateTimeService _clock;
    private readonly ISender _mediator;

    public ChangePasswordCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPasswordPolicyEnforcer passwords,
        IDateTimeService clock,
        ISender mediator)
    {
        _db = db;
        _currentUser = currentUser;
        _passwords = passwords;
        _clock = clock;
        _mediator = mediator;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure("Generic.Unauthorized", "No autenticado.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, ct);
        if (user is null)
        {
            return Result.Failure("Generic.NotFound", "Usuario no encontrado.");
        }

        if (!_passwords.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure("Auth.WrongCurrentPassword", "La contraseña actual no es correcta.");
        }

        int? tenantId = int.TryParse(_currentUser.TenantId, out var tid) ? tid : null;

        var policy = await _passwords.ValidateAsync(request.NewPassword, tenantId, ct);
        if (policy.IsFailure)
        {
            return policy;
        }

        var notReused = await _passwords.EnsureNotReusedAsync(user.Id, request.NewPassword, tenantId, ct);
        if (notReused.IsFailure)
        {
            return notReused;
        }

        var now = _clock.UtcNow;
        user.PasswordHash = _passwords.Hash(request.NewPassword);
        user.LastPasswordChangeAt = now;
        user.MustChangePassword = false;

        _db.PasswordHistory.Add(new PasswordHistory
        {
            UserId = user.Id,
            PasswordHash = user.PasswordHash,
            SetAt = now
        });

        await _db.SaveChangesAsync(ct);
        await _passwords.TrimHistoryAsync(user.Id, tenantId, ct);

        await _mediator.Send(new SendNotificationCommand(new NotificationPayload(
            RecipientUserPublicId: user.PublicId,
            Type: NotificationType.PasswordChanged,
            Subject: "Tu contraseña fue cambiada",
            Body: $"La contraseña de tu cuenta fue actualizada el {now:yyyy-MM-dd HH:mm} UTC. " +
                  "Si no fuiste tú, contacta inmediatamente al administrador de seguridad.",
            Channels: NotificationChannels.InApp | NotificationChannels.Email)), ct);

        return Result.Success();
    }
}
