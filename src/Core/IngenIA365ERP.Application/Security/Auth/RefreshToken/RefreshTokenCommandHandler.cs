using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Auth.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using DomainRefreshToken = IngenIA365ERP.Domain.Entities.Security.RefreshToken;

namespace IngenIA365ERP.Application.Security.Auth.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthTokensResult>>
{
    private readonly IApplicationDbContext _db;
    private readonly IAccessTokenIssuer _tokens;
    private readonly IRefreshTokenStore _refreshStore;
    private readonly IDateTimeService _clock;
    private readonly ISender _mediator;
    private readonly IUserPermissionResolver? _permissions;

    public RefreshTokenCommandHandler(
        IApplicationDbContext db,
        IAccessTokenIssuer tokens,
        IRefreshTokenStore refreshStore,
        IDateTimeService clock,
        ISender mediator,
        IUserPermissionResolver? permissions = null)
    {
        _db = db;
        _tokens = tokens;
        _refreshStore = refreshStore;
        _clock = clock;
        _mediator = mediator;
        _permissions = permissions;
    }

    public async Task<Result<AuthTokensResult>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var hash = _tokens.HashRefreshToken(request.RefreshToken);
        var now = _clock.UtcNow;

        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .ThenInclude(u => u!.Roles)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            return Result.Failure<AuthTokensResult>(
                "Auth.InvalidRefreshToken",
                "El refresh token es inválido o ya expiró.");
        }

        // Detección de reuso: si el token está revocado, se invalida toda la
        // familia y se emite notificación de actividad sospechosa.
        if (stored.RevokedAt is not null)
        {
            await InvalidateFamilyAsync(stored.FamilyId, "ReuseDetected", ct);

            if (stored.User is not null)
            {
                await _mediator.Send(new SendNotificationCommand(new NotificationPayload(
                    RecipientUserPublicId: stored.User.PublicId,
                    Type: NotificationType.SuspiciousSessionActivity,
                    Subject: "Actividad sospechosa detectada en tu cuenta",
                    Body: "Detectamos un intento de reutilizar un refresh token ya invalidado. " +
                          "Hemos cerrado todas las sesiones por seguridad. " +
                          "Si no fuiste tú, cambia tu contraseña de inmediato.",
                    Channels: NotificationChannels.InApp | NotificationChannels.Email)), ct);
            }

            return Result.Failure<AuthTokensResult>(
                "Auth.RefreshTokenReuseDetected",
                "Se detectó reuso del refresh token. Se cerraron todas las sesiones.");
        }

        if (stored.ExpiresAt <= now || stored.User is null || stored.User.IsDeleted || !stored.User.IsActive)
        {
            return Result.Failure<AuthTokensResult>(
                "Auth.InvalidRefreshToken",
                "El refresh token es inválido o ya expiró.");
        }

        var user = stored.User;
        var roles = user.Roles.Select(r => r.Name).ToList();

        // T075 — Permisos efectivos para los claims `perm`. El refresh es
        // EL momento donde un cambio de rol se propaga al cliente
        // (FR-019, SC-005, ≤ 30 min de TTL en el access token).
        var permissions = _permissions is not null
            ? await _permissions.ResolveAsync(user.Id, string.Empty, ct)
            : Array.Empty<string>();

        // Rotación: nuevo par + revocación del previo.
        var newRefresh = _tokens.IssueRefreshToken();
        var jti = Guid.NewGuid().ToString("N");
        var access = _tokens.IssueAccessToken(new AccessTokenClaims(
            UserPublicId: user.PublicId,
            UserId: user.Id,
            Username: user.Username,
            TenantPublicId: Guid.Empty,
            TenantId: string.Empty,
            BranchPublicId: null,
            Roles: roles,
            Permissions: permissions,
            PermissionsVersion: 1,
            Jti: jti));

        stored.RevokedAt = now;
        stored.RevocationReason = "Rotated";
        stored.ReplacedByToken = newRefresh.Token;

        _db.RefreshTokens.Add(new DomainRefreshToken
        {
            UserId = user.Id,
            Token = newRefresh.Token,
            TokenHash = newRefresh.TokenHash,
            FamilyId = stored.FamilyId,
            ExpiresAt = newRefresh.ExpiresAt,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        });

        await _refreshStore.MarkRotatedAsync(hash, newRefresh.TokenHash, ct);

        await _db.SaveChangesAsync(ct);

        return Result.Success(new AuthTokensResult(
            access.Token,
            newRefresh.Token,
            access.ExpiresAt,
            newRefresh.ExpiresAt,
            User: null));
    }

    private async Task InvalidateFamilyAsync(Guid familyId, string reason, CancellationToken ct)
    {
        await _refreshStore.InvalidateFamilyAsync(familyId.ToString(), ct);

        var siblings = await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(ct);
        var now = _clock.UtcNow;
        foreach (var s in siblings)
        {
            s.RevokedAt = now;
            s.RevocationReason = reason;
        }
        await _db.SaveChangesAsync(ct);
    }
}
