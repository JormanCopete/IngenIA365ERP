using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Caching.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisCacheService(IDistributedCache cache, IConnectionMultiplexer redis,
        IOptions<RedisSettings> settings)
    {
        _cache = cache;
        _redis = redis;
        _settings = settings.Value;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var data = await _cache.GetStringAsync(prefixedKey, cancellationToken);
        return data is null ? default : JsonSerializer.Deserialize<T>(data, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        var serialized = JsonSerializer.Serialize(value, JsonOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(_settings.DefaultExpiryMinutes)
        };
        await _cache.SetStringAsync(prefixedKey, serialized, options, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var prefixedKey = GetPrefixedKey(key);
        await _cache.RemoveAsync(prefixedKey, cancellationToken);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var prefixedPattern = GetPrefixedKey(prefix) + "*";
        var server = _redis.GetServer(_redis.GetEndPoints().First());

        var keys = server.Keys(pattern: prefixedPattern).ToArray();
        if (keys.Length > 0)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(keys);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, cancellationToken);
        return value;
    }

    private string GetPrefixedKey(string key) => $"{_settings.InstanceName}{key}";
}
