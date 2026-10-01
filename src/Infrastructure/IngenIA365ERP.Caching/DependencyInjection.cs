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

        // ConnectionStrings:Redis tiene prioridad — es donde
        // appsettings.Development.json guarda el valor con abortConnect=false.
        var connectionStringFromConnStrings = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(connectionStringFromConnStrings))
        {
            redisSettings.ConnectionString = connectionStringFromConnStrings;
        }

        // Forzamos AbortOnConnectFail=false para tolerar el warm-up de WSL2 /
        // Docker (~1–3s) sin tumbar el arranque de la API.
        var redisConfig = ConfigurationOptions.Parse(redisSettings.ConnectionString);
        redisConfig.AbortOnConnectFail = false;
        var multiplexer = ConnectionMultiplexer.Connect(redisConfig);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisSettings.ConnectionString;
            options.InstanceName = redisSettings.InstanceName;
        });

        // Cuantas bases logicas declara el servidor. Singleton: solo consulta al
        // multiplexor, y el valor no cambia mientras Redis no reinicie.
        services.AddSingleton<ICacheSlotCapacity, RedisSlotCapacity>();

        services.AddScoped<ICacheService, RedisCacheService>();

        // T029 — Abstracciones de seguridad respaldadas por Redis.
        //
        // IRevokedTokenBlacklist estaba aquí y no lo inyectaba nadie: la
        // revocación viva es el SecurityStamp, que invalida los tokens sin
        // necesidad de una lista negra. Una abstracción registrada sin
        // consumidor hace creer que hay una defensa donde no la hay.
        services.AddScoped<IRefreshTokenStore, RedisRefreshTokenStore>();
        services.AddScoped<IPermissionClaimsCache, RedisPermissionClaimsCache>();
        // Feature 012 (T33, T085): desafíos de la aprobación presencial y TOTP ya usados, en la ranura de la cooperativa.
        services.AddScoped<IngenIA365ERP.Application.Common.Approvals.IDesafiosDePresencia, RedisDesafiosDePresencia>();

        // Feature 002 (Chunk C.2) — identidad central:
        // - TenantMembershipReader: cache 60s + JOIN a ADM_TenantMemberships/Tenants/MfaPolicies
        //   con invalidación distribuida vía pub/sub Redis.
        // - MembershipChangedNotifier: publica en canal 'membership-changed' al mutar.
        // - LoginAttemptCounter: lockout progresivo por email (5/10/15/20 fallos).
        services.AddScoped<ITenantMembershipReader, RedisTenantMembershipReader>();
        services.AddScoped<IMembershipChangedNotifier, RedisMembershipChangedNotifier>();
        services.AddScoped<ILoginAttemptCounter, RedisLoginAttemptCounter>();

        // Tope diario de solicitudes de recuperacion. Clave propia, no el contador
        // de intentos: ahi no hay fallos que castigar, hay volumen que acotar.
        services.AddScoped<
            IngenIA365ERP.Application.Identity.Auth.Recuperacion.ITopeDeSolicitudesDeRecuperacion,
            Services.Identity.RedisTopeDeSolicitudesDeRecuperacion>();

        // Feature 002 (US1.2.0) — lock distribuido para serializar trabajo
        // crítico entre instancias (single-use estricto de invitations,
        // password reset tokens, MFA enrollment confirms). SET NX PX +
        // Lua release atómico. Singleton — solo consume IConnectionMultiplexer.
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();

        // Feature 002 (US2) — refresh tokens del flujo central.
        // Separado del IRefreshTokenStore legacy (Fase 0 con int UserId).
        services.AddSingleton<ICentralRefreshTokenStore, RedisCentralRefreshTokenStore>();

        // Feature 002 · Phase 4b — secret + recovery codes pendientes entre
        // BeginMfaEnrollment y ConfirmMfaEnrollment. TTL típico 10 min.
        services.AddSingleton<IMfaPendingStore, RedisMfaPendingStore>();

        // El reto de WebAuthn va aparte del pendiente de TOTP: aquel guarda una
        // sola clave por persona, y con dos pestañas abiertas la segunda pisaria
        // el reto de la primera.
        services.AddSingleton<IWebAuthnChallengeStore, RedisWebAuthnChallengeStore>();

        // Suscriptor pub/sub al canal de invalidaciones — se monta una vez por proceso.
        // El cleanup se delega al ConnectionMultiplexer singleton (dispose drops la suscripción).
        _ = RedisTenantMembershipReader.StartSubscriptionAsync(multiplexer);

        return services;
    }
}
