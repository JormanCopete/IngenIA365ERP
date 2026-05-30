using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IAccessTokenIssuer _tokens;
    private readonly IRefreshTokenStore _refreshStore;
    private readonly IDateTimeService _clock;

    public LogoutCommandHandler(
        IApplicationDbContext db,
        IAccessTokenIssuer tokens,
        IRefreshTokenStore refreshStore,
        IDateTimeService clock)
    {
        _db = db;
        _tokens = tokens;
        _refreshStore = refreshStore;
        _clock = clock;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        var hash = _tokens.HashRefreshToken(request.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || token.RevokedAt is not null)
        {
            // Idempotente: ya estaba revocado o no existe → 204.
            return Result.Success();
        }

        token.RevokedAt = _clock.UtcNow;
        token.RevocationReason = "LogoutUser";
        await _db.SaveChangesAsync(ct);
        await _refreshStore.MarkRotatedAsync(hash, string.Empty, ct);

        return Result.Success();
    }
}
