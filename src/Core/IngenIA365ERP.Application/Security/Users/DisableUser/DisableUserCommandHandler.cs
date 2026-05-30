using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.DisableUser;

public sealed class DisableUserCommandHandler : IRequestHandler<DisableUserCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IRefreshTokenStore? _refreshStore;

    public DisableUserCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        IRefreshTokenStore? refreshStore = null)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _refreshStore = refreshStore;
    }

    public async Task<Result> Handle(DisableUserCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null)
        {
            return Result.Failure("Generic.NotFound", "El usuario no existe.");
        }

        // Auto-protección: no permitir que el usuario actual se deshabilite a sí mismo.
        if (_currentUser.UserId is { } actorId && actorId == user.Id)
        {
            return Result.Failure("Security.Users.CannotDisableSelf",
                "No puedes deshabilitar tu propia cuenta.");
        }

        if (user.IsDeleted)
        {
            return Result.Failure(UserErrorCodes.AlreadyDisabled,
                "El usuario ya estaba deshabilitado.");
        }

        var now = _clock.UtcNow;
        var actor = _currentUser.UserName ?? "SYSTEM";
        user.IsActive = false;
        user.IsDeleted = true;
        user.DeletedAt = now;
        user.DeletedBy = actor;
        user.UpdatedBy = actor;

        // Revocar todos los refresh activos.
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

        if (_refreshStore is not null)
        {
            foreach (var fam in families)
            {
                await _refreshStore.InvalidateFamilyAsync(fam.ToString(), ct);
            }
        }

        return Result.Success();
    }
}
