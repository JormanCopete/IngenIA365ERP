using System.Collections.Concurrent;
using IngenIA365ERP.Application.Common.Interfaces.Security;

namespace IngenIA365ERP.API.Services;

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
