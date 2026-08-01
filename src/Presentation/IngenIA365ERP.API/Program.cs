using AspNetCoreRateLimit;
using Carter;
using IngenIA365ERP.API.Middleware;
using IngenIA365ERP.API.Middleware.CentralIdentity;
using IngenIA365ERP.API.Services;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application;
using IngenIA365ERP.Audit;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Caching.Services;
using IngenIA365ERP.Identity;
using IngenIA365ERP.Identity.CentralIdentity;
using IngenIA365ERP.Identity.Seed;
using IngenIA365ERP.Persistence;
using IngenIA365ERP.Storage;
using IngenIA365ERP.Caching;
using IngenIA365ERP.API.Hubs;
using IngenIA365ERP.API.HealthChecks;
using Microsoft.OpenApi;  // En OpenApi 2.x los tipos se movieron de Microsoft.OpenApi.Models a la raiz Microsoft.OpenApi
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/ingenia365erp-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting IngenIA365ERP API");

    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    var builder = WebApplication.CreateBuilder(args);

    // T123 — Serilog enrichers para central_user_id + active_tenant_id desde
    // los claims del JWT. Se rehidrata desde DI para tener IHttpContextAccessor.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .WriteTo.Console()
        .WriteTo.File("logs/ingenia365erp-.log", rollingInterval: RollingInterval.Day)
        .Enrich.With(new IngenIA365ERP.API.Logging.CentralIdentityLogEnricher(
            services.GetRequiredService<IHttpContextAccessor>())));

    // Add services to the container
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "IngenIA365ERP API",
            Version = "v1",
            Description = "API para el ERP Financiero IngenIA365ERP"
        });

        // JWT Bearer in Swagger.
        // Microsoft.OpenApi 2.x removio OpenApiSecurityScheme.Reference; ahora se usa
        // OpenApiSecuritySchemeReference y AddSecurityRequirement con delegate (document =>).
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Ejemplo: 'Bearer {token}'",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "Bearer"
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            // El value type del diccionario en OpenApi 2.x es List<string>, no string[].
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        });
    });

    // Carter for Minimal API endpoints
    builder.Services.AddCarter();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(
                    "https://localhost:7200",
                    "http://localhost:5200",
                    "https://localhost:7052")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

    // HttpContext accessor for tenant resolution
    builder.Services.AddHttpContextAccessor();
    // Singleton: consumed by Singleton MongoAuditService — IHttpContextAccessor handles per-request context.
    builder.Services.AddSingleton<ICurrentTenantService, TenantContextAccessor>();

    // === Cross-cutting services consumed by Identity & Audit ===
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
    builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
    // Feature 002 — accessor del JWT central (sub, email, active_tenant_id,
    // tenant_admin, is_global_master_admin, purpose, mfa_verified).
    builder.Services.AddSingleton<
        IngenIA365ERP.Application.Common.Interfaces.Identity.ICurrentCentralUserContext,
        CurrentCentralUserContextAccessor>();
    builder.Services.AddSingleton<IDateTimeService, DateTimeService>();
    // T012: acceso a la IP del cliente desde Application/handlers, sin acoplar a HttpContext.
    builder.Services.AddSingleton<IIpAddressAccessor, IpAddressAccessor>();

    // === Identity & Security ===
    // Fase 0 (legacy ApplicationUser, JwtBearer, PermissionService).
    builder.Services.AddIdentityServices(builder.Configuration);
    // T048 (Feature 002) — identidad central federada sobre AdminDbContext.
    // Registra IdentityCore<CentralUserIdentity>, BcryptPasswordHasher,
    // PwnedPasswordService, CentralJwtIssuer, ICentralIdentityProvider.
    // AdminDbContext ya queda registrado por AddPersistenceServices() abajo.
    builder.Services.AddCentralIdentity(builder.Configuration);

    // T052/T058 — MFA challenge + enrollment stores en memoria (fallback dev).
    // Producción usa los respaldos en Redis vía AddCachingServices.
    builder.Services.AddSingleton<IMfaChallengeStore, InMemoryMfaChallengeStore>();
    builder.Services.AddSingleton<IMfaEnrollmentStore, InMemoryMfaEnrollmentStore>();
    builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
    // T075 — cache de permisos efectivos (fallback dev). En producción
    // AddCachingServices registra el RedisPermissionClaimsCache.
    builder.Services.AddSingleton<IPermissionClaimsCache, InMemoryPermissionClaimsCache>();

    // === Rate Limiting ===
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(options =>
    {
        options.EnableEndpointRateLimiting = true;
        options.StackBlockedRequests = false;
        options.HttpStatusCode = 429;
        options.RealIpHeader = "X-Real-IP";
        options.ClientIdHeader = "X-Tenant-Id";
        options.GeneralRules =
        [
            new RateLimitRule
            {
                Endpoint = "POST:/api/auth/login",
                Period = "1m",
                Limit = 10
            },
            new RateLimitRule
            {
                Endpoint = "POST:/api/auth/refresh",
                Period = "1m",
                Limit = 20
            },
            new RateLimitRule
            {
                Endpoint = "*",
                Period = "1m",
                Limit = 1000
            }
        ];
    });
    builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
    builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

    // === Audit (MongoDB) ===
    builder.Services.AddAuditServices(builder.Configuration);

    // Application layer DI registration (MediatR, FluentValidation, Mapster)
    builder.Services.AddApplicationServices();

    // Feature 002 — opciones del flujo de identidad central
    // (URL base del frontend para el enlace de invitaciones, lifetime, etc.).
    builder.Services.Configure<IngenIA365ERP.Application.Common.Configuration.IdentityEmailOptions>(
        builder.Configuration.GetSection(
            IngenIA365ERP.Application.Common.Configuration.IdentityEmailOptions.SectionName));

    // Infrastructure layer DI registrations
    builder.Services.AddPersistenceServices(builder.Configuration);
    // Feature 002 (US2+US3+US4+Phase 4b) — los handlers de identidad central
    // requieren Redis para: IDistributedLock (single-use de invitaciones),
    // ICentralRefreshTokenStore (family rotation), IMfaPendingStore (secret
    // temporal de enrollment), ITenantMembershipReader (cache + pub/sub),
    // IMembershipChangedNotifier (invalidación distribuida), ILoginAttemptCounter
    // (lockout progresivo). El docker-compose levanta Redis en localhost:6379.
    builder.Services.AddCachingServices(builder.Configuration);

    // T030 — Email (MailKit) y T025/T030a — Storage abstractions.
    builder.Services.AddStorageServices(builder.Configuration);

    // T031 — SignalR para el push de notificaciones in-app.
    builder.Services.AddSignalR();
    // T120 — Implementación SignalR de INotificationPusher (US6).
    builder.Services.AddScoped<
        IngenIA365ERP.Application.Notifications.Contracts.INotificationPusher,
        IngenIA365ERP.API.Hubs.SignalRNotificationPusher>();

    // T136 — Health checks: /health/live (proceso) y /health/ready (deps).
    builder.Services.AddIngenIaHealthChecks(builder.Configuration);

    var app = builder.Build();

    // Initialize MongoDB collections and indexes
    var mongoInit = app.Services.GetRequiredService<MongoDbInitializer>();
    await mongoInit.InitializeDefaultAsync();

    // Seed Identity data (roles, permissions, admin user)
    if (app.Environment.IsDevelopment())
    {
        // Seeder legacy ASP.NET Identity (AspNetUsers/AspNetRoles). El flujo
        // Phase 0+ vive en SEC_Users/SEC_Roles, no en estas tablas — el
        // seeder se mantiene solo para retro-compatibilidad con código
        // legacy que aún consume Identity. Su fallo NO debe derribar el
        // host: si la BD de Identity está rara, logueamos y continuamos.
        try
        {
            await IdentitySeedData.SeedAsync(app.Services);
        }
        catch (Exception ex)
        {
            Log.Warning(ex,
                "IdentitySeedData (legacy ASP.NET Identity) falló al sembrar. " +
                "El flujo Phase 0+ no depende de estas tablas — el host continúa.");
        }

        // T066/T067 (US2) — primero el catálogo y los roles built-in,
        // para que DomainSecuritySeedData pueda asignar CompanyAdmin al admin.
        await DomainPermissionCatalogSeeder.SeedAsync(app.Services);
        await BuiltInRolesSeeder.SeedAsync(app.Services);
        // Seed paralelo para SEC_Users (tabla de dominio usada por el flujo Phase 0/US1).
        await DomainSecuritySeedData.SeedAsync(app.Services);
    }

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        // T137 — HSTS solo en prod (en dev HTTP local rompería).
        app.UseHsts();
    }

    app.UseSecurityHeaders();
    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseIpRateLimiting();
    app.UseCors("AllowFrontend");
    // T048: orden estricto — Authentication primero para que TenantResolution
    // pueda leer el claim active_tenant_id (T044). CentralIdentityChallenge
    // envuelve respuestas 401 vacías en el envelope JSON canónico (T045).
    app.UseAuthentication();
    app.UseCentralIdentityChallenge();
    app.UseTenantResolution();
    app.UseAuthorization();
    // T074 — convierte cualquier 404 bajo /api/* en el envelope canónico,
    // haciendo indistinguible "endpoint no existe" vs "no tienes permiso".
    app.UseNotFoundEnvelope();

    // Map Carter endpoints
    app.MapCarter();

    // T031 — Hub de notificaciones (autenticación JWT obligatoria por [Authorize]).
    app.MapHub<NotificationsHub>("/hubs/notifications");

    // Health check endpoint
    app.MapIngenIaHealthChecks();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make the implicit Program class accessible to integration tests
public partial class Program;
