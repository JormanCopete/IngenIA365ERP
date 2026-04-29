using System.Collections.Concurrent;
using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.Caching.Services;

public class MemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, (object Value, DateTime? Expiry)> _cache = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.Expiry == null || entry.Expiry > DateTime.UtcNow)
                return Task.FromResult((T?)entry.Value);
            _cache.TryRemove(key, out _);
        }
        return Task.FromResult(default(T?));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var expiryTime = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : (DateTime?)null;
        _cache[key] = (value!, expiryTime);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var key in _cache.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
