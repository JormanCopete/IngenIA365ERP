using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Users.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.AssignRole;

public sealed class AssignRoleCommandHandler : IRequestHandler<AssignRoleCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IPermissionClaimsCache? _claimsCache;
    private readonly ISender _mediator;

    public AssignRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        ISender mediator,
        IPermissionClaimsCache? claimsCache = null)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _mediator = mediator;
        _claimsCache = claimsCache;
    }

    public async Task<Result> Handle(AssignRoleCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.PublicId == request.RolePublicId, ct);
        if (role is null) return Result.Failure("Generic.NotFound", "El rol no existe.");

        if (!role.IsAssignable)
        {
            return Result.Failure(UserErrorCodes.RoleNotAssignable,
                $"El rol '{role.Code}' es interno del sistema y no se asigna a usuarios.");
        }

        var alreadyAssigned = await _db.UserRoles
            .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct);

        if (alreadyAssigned)
        {
            return Result.Failure(UserErrorCodes.AlreadyAssignedRole,
                $"El usuario ya tiene asignado el rol '{role.Code}'.");
        }

        var now = _clock.UtcNow;
        var actor = _currentUser.UserName ?? "SYSTEM";

        _db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            AssignedAt = now,
            AssignedBy = actor,
            CreatedBy = actor,
            UpdatedBy = actor
        });

        await _db.SaveChangesAsync(ct);

        if (_claimsCache is not null)
        {
            // La cooperativa sale de la peticion, que es de donde tambien la toma
            // PermisosDeLaPeticion al poblar el cache: las dos puntas tienen que
            // usar la misma clave o la invalidacion no encuentra nada. Es el Id
            // interno, no el PublicId.
            await _claimsCache.InvalidateAsync(user.Id,
                _currentUser.TenantId ?? string.Empty, ct);
        }

        await _mediator.Send(new SendNotificationCommand(new NotificationPayload(
            RecipientUserPublicId: user.PublicId,
            Type: NotificationType.RoleAssigned,
            Subject: $"Se te asignó el rol {role.Name}",
            Body: $"Un administrador te asignó el rol '{role.Name}'. " +
                  "Los nuevos permisos se reflejarán en tu próxima sesión (≤ 30 min).",
            Channels: NotificationChannels.InApp | NotificationChannels.Email)), ct);

        return Result.Success();
    }
}
