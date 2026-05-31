using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Caching.Configuration;
using IngenIA365ERP.Caching.Services;
using IngenIA365ERP.Caching.Services.Identity;
using IngenIA365ERP.Caching.Services.Security;
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

        var multiplexer = ConnectionMultiplexer.Connect(redisSettings.ConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisSettings.ConnectionString;
            options.InstanceName = redisSettings.InstanceName;
        });

        services.AddScoped<ICacheService, RedisCacheService>();

        // T029 — Abstracciones de seguridad respaldadas por Redis.
        services.AddScoped<IRefreshTokenStore, RedisRefreshTokenStore>();
        services.AddScoped<IRevokedTokenBlacklist, RedisRevokedTokenBlacklist>();
        services.AddScoped<IPermissionClaimsCache, RedisPermissionClaimsCache>();
        services.AddScoped<IMfaResetCoordinator, RedisMfaResetCoordinator>();

        // T052 — Cache de challenge MFA (post-login, pre-verify) y enrollment.
        services.AddScoped<IMfaChallengeStore, RedisMfaChallengeStore>();
        services.AddScoped<IMfaEnrollmentStore, RedisMfaEnrollmentStore>();

        // Feature 002 (Chunk C.2) — identidad central:
        // - TenantMembershipReader: cache 60s + JOIN a ADM_TenantMemberships/Tenants/MfaPolicies
        //   con invalidación distribuida vía pub/sub Redis.
        // - MembershipChangedNotifier: publica en canal 'membership-changed' al mutar.
        // - LoginAttemptCounter: lockout progresivo por email (5/10/15/20 fallos).
        services.AddScoped<ITenantMembershipReader, RedisTenantMembershipReader>();
        services.AddScoped<IMembershipChangedNotifier, RedisMembershipChangedNotifier>();
        services.AddScoped<ILoginAttemptCounter, RedisLoginAttemptCounter>();

        // Feature 002 (US1.2.0) — lock distribuido para serializar trabajo
        // crítico entre instancias (single-use estricto de invitations,
        // password reset tokens, MFA enrollment confirms). SET NX PX +
        // Lua release atómico. Singleton — solo consume IConnectionMultiplexer.
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();

        // Suscriptor pub/sub al canal de invalidaciones — se monta una vez por proceso.
        // El cleanup se delega al ConnectionMultiplexer singleton (dispose drops la suscripción).
        _ = RedisTenantMembershipReader.StartSubscriptionAsync(multiplexer);

        return services;
    }
}
