using System.Collections.Concurrent;
using System.Security.Cryptography;
using IngenIA365ERP.Application.Common.Interfaces.Security;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// Fallback in-memory cuando Redis no está disponible (dev local). En producción
/// debe usarse <c>RedisMfaChallengeStore</c> y <c>RedisMfaEnrollmentStore</c>.
/// Entradas con TTL gestionado a nivel de aplicación con <see cref="Timer"/>.
/// </summary>
internal sealed class InMemoryMfaChallengeStore : IMfaChallengeStore
{
    private readonly ConcurrentDictionary<string, (MfaChallengeContext Ctx, DateTime ExpiresAt)> _items = new();

    public Task<string> IssueAsync(MfaChallengeContext context, TimeSpan ttl, CancellationToken ct)
    {
        var token = OpaqueToken();
        _items[token] = (context, DateTime.UtcNow.Add(ttl));
        return Task.FromResult(token);
    }

    public Task<MfaChallengeContext?> ConsumeAsync(string token, CancellationToken ct)
    {
        if (_items.TryRemove(token, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            return Task.FromResult<MfaChallengeContext?>(entry.Ctx);
        }
        return Task.FromResult<MfaChallengeContext?>(null);
    }

    internal static string OpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

internal sealed class InMemoryMfaEnrollmentStore : IMfaEnrollmentStore
{
    private readonly ConcurrentDictionary<string, (MfaEnrollmentContext Ctx, DateTime ExpiresAt)> _items = new();

    public Task<string> IssueAsync(MfaEnrollmentContext context, TimeSpan ttl, CancellationToken ct)
    {
        var token = InMemoryMfaChallengeStore.OpaqueToken();
        _items[token] = (context, DateTime.UtcNow.Add(ttl));
        return Task.FromResult(token);
    }

    public Task<MfaEnrollmentContext?> GetAsync(string token, CancellationToken ct)
    {
        if (_items.TryGetValue(token, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            return Task.FromResult<MfaEnrollmentContext?>(entry.Ctx);
        }
        _items.TryRemove(token, out _);
        return Task.FromResult<MfaEnrollmentContext?>(null);
    }

    public Task RemoveAsync(string token, CancellationToken ct)
    {
        _items.TryRemove(token, out _);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Cache in-memory de permisos efectivos (fallback dev). En producción
/// se usa <c>RedisPermissionClaimsCache</c> con pub/sub para invalidación
/// cross-instance.
/// </summary>
internal sealed class InMemoryPermissionClaimsCache : IPermissionClaimsCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, (IReadOnlyList<string> Perms, DateTime ExpiresAt)> _items = new();

    private static string Key(int userId, string tenantId) => $"{tenantId}:{userId}";

    public Task<IReadOnlyList<string>?> GetAsync(int userId, string tenantId, CancellationToken ct)
    {
        if (_items.TryGetValue(Key(userId, tenantId), out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            return Task.FromResult<IReadOnlyList<string>?>(entry.Perms);
        }
        _items.TryRemove(Key(userId, tenantId), out _);
        return Task.FromResult<IReadOnlyList<string>?>(null);
    }

    public Task SetAsync(int userId, string tenantId, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        _items[Key(userId, tenantId)] = (permissions, DateTime.UtcNow.Add(Ttl));
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(int userId, string tenantId, CancellationToken ct)
    {
        _items.TryRemove(Key(userId, tenantId), out _);
        return Task.CompletedTask;
    }

    public Task InvalidateAllForTenantAsync(string tenantId, CancellationToken ct)
    {
        var prefix = $"{tenantId}:";
        foreach (var k in _items.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            _items.TryRemove(k, out _);
        }
        return Task.CompletedTask;
    }

    public Task InvalidateRoleAsync(int roleId, CancellationToken ct)
    {
        // Sin índice rol→user, purgamos todo. El TTL acota el costo.
        _items.Clear();
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, (RefreshTokenContext Ctx, DateTime ExpiresAt)> _byTokenHash = new();
    private readonly ConcurrentDictionary<string, bool> _invalidFamilies = new();

    public Task StoreAsync(string token, RefreshTokenContext context, TimeSpan ttl, CancellationToken ct)
    {
        _byTokenHash[token] = (context, DateTime.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task<RefreshTokenContext?> GetAsync(string token, CancellationToken ct)
    {
        if (_byTokenHash.TryGetValue(token, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            return Task.FromResult<RefreshTokenContext?>(entry.Ctx);
        }
        return Task.FromResult<RefreshTokenContext?>(null);
    }

    public Task MarkRotatedAsync(string token, string replacedByToken, CancellationToken ct)
    {
        if (_byTokenHash.TryGetValue(token, out var entry))
        {
            _byTokenHash[token] = (entry.Ctx with { ReplacedByToken = replacedByToken }, entry.ExpiresAt);
        }
        return Task.CompletedTask;
    }

    public Task InvalidateFamilyAsync(string familyId, CancellationToken ct)
    {
        _invalidFamilies[familyId] = true;
        return Task.CompletedTask;
    }

    public Task<bool> IsFamilyInvalidatedAsync(string familyId, CancellationToken ct) =>
        Task.FromResult(_invalidFamilies.ContainsKey(familyId));
}
