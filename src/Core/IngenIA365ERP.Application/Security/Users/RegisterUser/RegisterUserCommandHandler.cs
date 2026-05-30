using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Users.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.RegisterUser;

public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordPolicyEnforcer _passwords;
    private readonly IDateTimeService _clock;
    private readonly ISender _mediator;

    public RegisterUserCommandHandler(
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

    public async Task<Result<Guid>> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();

        // Unicidad de Username/Email (FR del data-model — ambos son únicos globales).
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == request.Username, ct))
        {
            return Result.Failure<Guid>(UserErrorCodes.UsernameTaken,
                $"Ya existe un usuario con el nombre '{request.Username}'.");
        }
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == request.Email, ct))
        {
            return Result.Failure<Guid>(UserErrorCodes.EmailTaken,
                $"Ya existe un usuario con el correo '{request.Email}'.");
        }

        // Política de contraseñas (FR-007/008).
        var policy = await _passwords.ValidateAsync(request.InitialPassword, tenantId, ct);
        if (policy.IsFailure) return Result.Failure<Guid>(policy.Error);

        // Validar roles (los PublicIds deben existir y ser asignables).
        var requestedRolePublicIds = request.RolePublicIds?.Distinct().ToList() ?? [];
        var matchedRoles = requestedRolePublicIds.Count == 0
            ? new List<Role>()
            : await _db.Roles
                .Where(r => requestedRolePublicIds.Contains(r.PublicId))
                .ToListAsync(ct);

        if (matchedRoles.Count != requestedRolePublicIds.Count)
        {
            return Result.Failure<Guid>("Generic.NotFound",
                "Uno o más roles solicitados no existen.");
        }
        if (matchedRoles.Any(r => !r.IsAssignable))
        {
            return Result.Failure<Guid>(UserErrorCodes.RoleNotAssignable,
                "Uno o más roles no son asignables a usuarios (rol interno del sistema).");
        }

        var now = _clock.UtcNow;
        var actor = _currentUser.UserName ?? "SYSTEM";

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PersonId = request.PersonId,
            IdentificationNumber = request.IdentificationNumber,
            PasswordHash = _passwords.Hash(request.InitialPassword),
            LastPasswordChangeAt = now,
            MustChangePassword = true,                 // FR-009 — cambio obligatorio al primer login
            IsActive = true,
            IsEmailVerified = false,
            IsMfaEnabled = false,
            FailedLoginAttempts = 0,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        foreach (var role in matchedRoles)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                AssignedAt = now,
                AssignedBy = actor,
                CreatedBy = actor,
                UpdatedBy = actor
            });
        }
        // Persist el password en historial (FR-010).
        _db.PasswordHistory.Add(new PasswordHistory
        {
            UserId = user.Id,
            PasswordHash = user.PasswordHash,
            SetAt = now
        });
        await _db.SaveChangesAsync(ct);

        await _mediator.Send(new SendNotificationCommand(new NotificationPayload(
            RecipientUserPublicId: user.PublicId,
            Type: NotificationType.UserInvitationCreated,
            Subject: "Tu cuenta en IngenIA365ERP fue creada",
            Body: $"Hola, un administrador creó tu cuenta. Inicia sesión con el correo " +
                  $"{request.Email} — al primer ingreso deberás cambiar la contraseña.",
            Channels: NotificationChannels.InApp | NotificationChannels.Email)), ct);

        return Result.Success(user.PublicId);
    }

    private int? ResolveTenantId() =>
        int.TryParse(_currentUser.TenantId, out var tid) ? tid : null;
}
