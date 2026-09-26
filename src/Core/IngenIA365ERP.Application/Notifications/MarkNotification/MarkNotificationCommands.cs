using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Notifications.MarkNotification;

/// <summary>Marca una notificación como leída — solo el dueño puede hacerlo.</summary>
public sealed record MarkNotificationReadCommand(Guid NotificationPublicId) : IRequest<Result>;

/// <summary>Archiva una notificación (oculta del inbox pero conserva el registro).</summary>
public sealed record MarkNotificationArchivedCommand(Guid NotificationPublicId) : IRequest<Result>;

/// <summary>Marca todas las pendientes como leídas en una sola pasada.</summary>
public sealed record MarkAllNotificationsReadCommand : IRequest<Result>;

public sealed class MarkNotificationReadValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadValidator() =>
        RuleFor(x => x.NotificationPublicId).NotEmpty();
}

public sealed class MarkNotificationArchivedValidator
    : AbstractValidator<MarkNotificationArchivedCommand>
{
    public MarkNotificationArchivedValidator() =>
        RuleFor(x => x.NotificationPublicId).NotEmpty();
}

public sealed class MarkNotificationReadHandler
    : IRequestHandler<MarkNotificationReadCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IActorActual _actor;
    private readonly IDateTimeService _clock;

    public MarkNotificationReadHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IActorActual actor, IDateTimeService clock)
    {
        _db = db; _currentUser = currentUser; _actor = actor; _clock = clock;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var recipient = await ResolveUserPublicIdAsync(_actor, ct);
        if (recipient is null) return Result.Failure("Auth.Unauthorized", "Usuario no identificado.");

        var entry = await _db.Notifications
            .FirstOrDefaultAsync(n => n.PublicId == request.NotificationPublicId, ct);
        if (entry is null) return Result.Failure("Generic.NotFound", "Notificación no encontrada.");

        if (entry.RecipientUserPublicId != recipient.Value)
        {
            // 404 indistinguible — no revelar que existe.
            return Result.Failure("Generic.NotFound", "Notificación no encontrada.");
        }

        if (entry.ReadAt is null)
        {
            entry.ReadAt = _clock.UtcNow;
            entry.UpdatedBy = _currentUser.UserName ?? "SYSTEM";
            await _db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    /// <summary>
    /// El dueño de la bandeja por <see cref="IActorActual"/> (feature 012, T39, T095), no por
    /// <c>ICurrentUserService.UserId</c>, que es nulo para un usuario de identidad central.
    /// </summary>
    internal static async Task<Guid?> ResolveUserPublicIdAsync(IActorActual actor, CancellationToken ct) =>
        (await actor.ObtenerAsync(ct)).UserPublicId;
}

public sealed class MarkNotificationArchivedHandler
    : IRequestHandler<MarkNotificationArchivedCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IActorActual _actor;
    private readonly IDateTimeService _clock;

    public MarkNotificationArchivedHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IActorActual actor, IDateTimeService clock)
    {
        _db = db; _currentUser = currentUser; _actor = actor; _clock = clock;
    }

    public async Task<Result> Handle(MarkNotificationArchivedCommand request, CancellationToken ct)
    {
        var recipient = await MarkNotificationReadHandler.ResolveUserPublicIdAsync(_actor, ct);
        if (recipient is null) return Result.Failure("Auth.Unauthorized", "Usuario no identificado.");

        var entry = await _db.Notifications
            .FirstOrDefaultAsync(n => n.PublicId == request.NotificationPublicId, ct);
        if (entry is null || entry.RecipientUserPublicId != recipient.Value)
        {
            return Result.Failure("Generic.NotFound", "Notificación no encontrada.");
        }

        if (entry.ArchivedAt is null)
        {
            var now = _clock.UtcNow;
            entry.ArchivedAt = now;
            entry.ReadAt ??= now;
            entry.UpdatedBy = _currentUser.UserName ?? "SYSTEM";
            await _db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }
}

public sealed class MarkAllNotificationsReadHandler
    : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IActorActual _actor;
    private readonly IDateTimeService _clock;

    public MarkAllNotificationsReadHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IActorActual actor, IDateTimeService clock)
    {
        _db = db; _currentUser = currentUser; _actor = actor; _clock = clock;
    }

    public async Task<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        var recipient = await MarkNotificationReadHandler.ResolveUserPublicIdAsync(_actor, ct);
        if (recipient is null) return Result.Failure("Auth.Unauthorized", "Usuario no identificado.");

        var now = _clock.UtcNow;
        var actor = _currentUser.UserName ?? "SYSTEM";
        var pending = await _db.Notifications
            .Where(n => n.RecipientUserPublicId == recipient.Value && n.ReadAt == null)
            .ToListAsync(ct);
        foreach (var n in pending)
        {
            n.ReadAt = now;
            n.UpdatedBy = actor;
        }
        if (pending.Count > 0) await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
