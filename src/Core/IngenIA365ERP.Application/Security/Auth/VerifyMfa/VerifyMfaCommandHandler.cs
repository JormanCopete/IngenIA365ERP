using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using DomainRefreshToken = IngenIA365ERP.Domain.Entities.Security.RefreshToken;

namespace IngenIA365ERP.Application.Security.Auth.VerifyMfa;

public sealed class VerifyMfaCommandHandler : IRequestHandler<VerifyMfaCommand, Result<AuthTokensResult>>
{
    private readonly IApplicationDbContext _db;
    private readonly IMfaChallengeStore _challenges;
    private readonly ITotpService _totp;
    private readonly IMfaBackupCodeGenerator _backupCodes;
    private readonly IAccessTokenIssuer _tokens;
    private readonly IRefreshTokenStore _refreshStore;
    private readonly IDateTimeService _clock;
    private readonly IUserPermissionResolver? _permissions;

    public VerifyMfaCommandHandler(
        IApplicationDbContext db,
        IMfaChallengeStore challenges,
        ITotpService totp,
        IMfaBackupCodeGenerator backupCodes,
        IAccessTokenIssuer tokens,
        IRefreshTokenStore refreshStore,
        IDateTimeService clock,
        IUserPermissionResolver? permissions = null)
    {
        _db = db;
        _challenges = challenges;
        _totp = totp;
        _backupCodes = backupCodes;
        _tokens = tokens;
        _refreshStore = refreshStore;
        _clock = clock;
        _permissions = permissions;
    }

    public async Task<Result<AuthTokensResult>> Handle(VerifyMfaCommand request, CancellationToken ct)
    {
        var challenge = await _challenges.ConsumeAsync(request.MfaChallengeToken, ct);
        if (challenge is null)
        {
            return Result.Failure<AuthTokensResult>(
                "Auth.MfaChallengeExpired",
                "El challenge MFA expiró o ya fue consumido.");
        }

        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == challenge.UserId, ct);

        if (user is null || user.IsDeleted || !user.IsActive)
        {
            return Result.Failure<AuthTokensResult>(
                "Auth.InvalidCredentials",
                "Usuario o contraseña inválidos.");
        }

        // Si el usuario aún no inscribió MFA, se permite continuar (post-login
        // se le forzará la inscripción); FR-005/006 establece MFA obligatorio
        // antes del primer acceso a operaciones sensibles.
        if (user.IsMfaEnabled)
        {
            var verified = request.UseBackupCode
                ? await ConsumeBackupCodeAsync(user.Id, request.BackupCode!, ct)
                : VerifyTotp(user.MfaSecret, request.TotpCode!);

            if (!verified)
            {
                return Result.Failure<AuthTokensResult>(
                    request.UseBackupCode ? "Auth.InvalidBackupCode" : "Auth.InvalidMfaCode",
                    request.UseBackupCode
                        ? "El código de respaldo es inválido o ya fue utilizado."
                        : "El código TOTP es inválido.");
            }
        }

        // Validación de sucursal.
        Guid? branchPublicId = null;
        if (request.BranchPublicId.HasValue)
        {
            var branchAssigned = await _db.UserBranchAssignments
                .Include(a => a.Branch)
                .AnyAsync(a => a.UserId == user.Id
                            && a.Branch != null
                            && a.Branch.PublicId == request.BranchPublicId.Value, ct);
            if (!branchAssigned)
            {
                return Result.Failure<AuthTokensResult>(
                    "Auth.BranchNotAuthorized",
                    "El usuario no tiene acceso a la sucursal elegida.");
            }
            branchPublicId = request.BranchPublicId;
        }

        var roles = user.Roles.Select(r => r.Name).ToList();

        // T075 — Permisos efectivos para los claims `perm` del JWT.
        // Si el resolver no está registrado (escenarios de test sin DI completo),
        // emitimos lista vacía → el usuario operará sin permisos hasta el siguiente
        // refresh, que sí lo poblará.
        var permissions = _permissions is not null
            ? await _permissions.ResolveAsync(user.Id, challenge.TenantIdentifier, ct)
            : Array.Empty<string>();

        var jti = Guid.NewGuid().ToString("N");
        var accessClaims = new AccessTokenClaims(
            UserPublicId: user.PublicId,
            UserId: user.Id,
            Username: user.Username,
            TenantPublicId: challenge.TenantPublicId,
            TenantId: challenge.TenantIdentifier,
            BranchPublicId: branchPublicId,
            Roles: roles,
            Permissions: permissions,
            PermissionsVersion: 1,
            Jti: jti);

        var access = _tokens.IssueAccessToken(accessClaims);
        var refresh = _tokens.IssueRefreshToken();
        var familyId = Guid.NewGuid();

        var refreshEntity = new DomainRefreshToken
        {
            UserId = user.Id,
            Token = refresh.Token,
            TokenHash = refresh.TokenHash,
            FamilyId = familyId,
            ExpiresAt = refresh.ExpiresAt,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        };
        _db.RefreshTokens.Add(refreshEntity);

        // Mirror en Redis para detección de rotación rápida.
        await _refreshStore.StoreAsync(
            refresh.TokenHash,
            new RefreshTokenContext(
                user.Id, challenge.TenantIdentifier, familyId.ToString(),
                _clock.UtcNow, request.IpAddress, request.UserAgent, null),
            refresh.ExpiresAt - _clock.UtcNow,
            ct);

        user.LastLoginAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result.Success(new AuthTokensResult(
            access.Token,
            refresh.Token,
            access.ExpiresAt,
            refresh.ExpiresAt,
            new AuthenticatedUserDto(
                user.PublicId,
                user.Username,
                new AuthenticatedTenantDto(challenge.TenantPublicId, challenge.TenantIdentifier),
                branchPublicId.HasValue
                    ? new AuthenticatedBranchDto(branchPublicId.Value, branchPublicId.Value.ToString())
                    : null,
                roles)));
    }

    private bool VerifyTotp(string? protectedSecret, string totpCode)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            return false;
        }
        try
        {
            var plain = _totp.UnprotectSecret(protectedSecret);
            return _totp.Verify(plain, totpCode);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ConsumeBackupCodeAsync(int userId, string backupCode, CancellationToken ct)
    {
        var candidates = await _db.MfaBackupCodes
            .Where(c => c.UserId == userId && c.UsedAt == null)
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(c => _backupCodes.Verify(backupCode, c.CodeHash));
        if (match is null)
        {
            return false;
        }
        match.MarkUsed(_clock.UtcNow);
        return true;
    }
}
