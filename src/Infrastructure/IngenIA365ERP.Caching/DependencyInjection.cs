using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Caching.Configuration;
using IngenIA365ERP.Caching.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching;

public static class DependencyInjection
{
    public static IServiceCollection AddCachingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));

        var redisSettings = configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>()
            ?? new RedisSettings();

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisSettings.ConnectionString));

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisSettings.ConnectionString;
            options.InstanceName = redisSettings.InstanceName;
        });

        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }
}
