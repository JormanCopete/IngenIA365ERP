using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.LogoutAll;

public sealed class LogoutAllCommandHandler : IRequestHandler<LogoutAllCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRefreshTokenStore _refreshStore;
    private readonly IDateTimeService _clock;

    public LogoutAllCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRefreshTokenStore refreshStore,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _refreshStore = refreshStore;
        _clock = clock;
    }

    public async Task<Result> Handle(LogoutAllCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
        {
            return Result.Failure("Generic.Unauthorized", "No autenticado.");
        }

        var userId = _currentUser.UserId.Value;
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        var families = new HashSet<Guid>();
        foreach (var t in tokens)
        {
            t.RevokedAt = now;
            t.RevocationReason = "LogoutAll";
            families.Add(t.FamilyId);
        }
        await _db.SaveChangesAsync(ct);

        foreach (var f in families)
        {
            await _refreshStore.InvalidateFamilyAsync(f.ToString(), ct);
        }

        return Result.Success();
    }
}
