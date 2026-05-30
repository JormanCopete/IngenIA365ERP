using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Identity.Services;

/// <summary>
/// Aplica la política activa de contraseñas (FR-007..010). Cost de BCrypt
/// fijado en 11 — exigencia SC-008.
/// </summary>
public class PasswordPolicyEnforcer : IPasswordPolicyEnforcer
{
    private const int BcryptCost = 11;
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;

    public PasswordPolicyEnforcer(IApplicationDbContext db, IDateTimeService clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result> ValidateAsync(string newPassword, int? tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return Result.Failure("Auth.PasswordPolicyViolation",
                "La contraseña no puede ser vacía.");
        }

        var policy = await GetPolicyAsync(tenantId, ct);

        if (newPassword.Length < policy.MinLength)
        {
            return Result.Failure("Auth.PasswordPolicyViolation",
                $"La contraseña debe tener al menos {policy.MinLength} caracteres.");
        }

        if (policy.RequireUppercase && !newPassword.Any(char.IsUpper))
        {
            return Result.Failure("Auth.PasswordPolicyViolation",
                "La contraseña debe incluir al menos una letra mayúscula.");
        }
        if (policy.RequireLowercase && !newPassword.Any(char.IsLower))
        {
            return Result.Failure("Auth.PasswordPolicyViolation",
                "La contraseña debe incluir al menos una letra minúscula.");
        }
        if (policy.RequireDigit && !newPassword.Any(char.IsDigit))
        {
            return Result.Failure("Auth.PasswordPolicyViolation",
                "La contraseña debe incluir al menos un dígito.");
        }
        if (policy.RequireSymbol && newPassword.All(c => char.IsLetterOrDigit(c)))
        {
            return Result.Failure("Auth.PasswordPolicyViolation",
                "La contraseña debe incluir al menos un carácter especial.");
        }

        return Result.Success();
    }

    public async Task<Result> EnsureNotReusedAsync(int userId, string newPassword, int? tenantId, CancellationToken ct)
    {
        var policy = await GetPolicyAsync(tenantId, ct);

        var lastHashes = await _db.PasswordHistory
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.SetAt)
            .Take(policy.HistorySize)
            .Select(h => h.PasswordHash)
            .ToListAsync(ct);

        if (lastHashes.Any(h => BCrypt.Net.BCrypt.Verify(newPassword, h)))
        {
            return Result.Failure("Auth.PasswordPolicyViolation",
                $"La contraseña ya fue utilizada en las últimas {policy.HistorySize} ocasiones.");
        }
        return Result.Success();
    }

    public async Task<bool> IsExpiredAsync(int userId, int? tenantId, DateTime? lastChangedAt, CancellationToken ct)
    {
        if (lastChangedAt is null)
        {
            return true;
        }
        var policy = await GetPolicyAsync(tenantId, ct);
        return _clock.UtcNow - lastChangedAt.Value > TimeSpan.FromDays(policy.ExpiryDays);
    }

    public string Hash(string plainPassword) =>
        BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: BcryptCost);

    public bool Verify(string plainPassword, string hash)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }
        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, hash);
        }
        catch
        {
            return false;
        }
    }

    public async Task TrimHistoryAsync(int userId, int? tenantId, CancellationToken ct)
    {
        var policy = await GetPolicyAsync(tenantId, ct);
        var excess = await _db.PasswordHistory
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.SetAt)
            .Skip(policy.HistorySize)
            .ToListAsync(ct);

        if (excess.Count == 0)
        {
            return;
        }

        foreach (var h in excess)
        {
            // PasswordHistory no hereda AuditableEntity → DELETE físico (append-only auditado).
            _db.PasswordHistory.Remove(h);
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PasswordPolicy> GetPolicyAsync(int? tenantId, CancellationToken ct)
    {
        var tenant = tenantId.HasValue
            ? await _db.PasswordPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == tenantId.Value, ct)
            : null;

        if (tenant is not null)
        {
            return tenant;
        }

        var global = await _db.PasswordPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == null, ct);

        return global ?? new PasswordPolicy();
    }
}
