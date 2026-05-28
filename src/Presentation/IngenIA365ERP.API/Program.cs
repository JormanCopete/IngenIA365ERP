using AspNetCoreRateLimit;
using Carter;
using IngenIA365ERP.API.Middleware;
using IngenIA365ERP.API.Services;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application;
using IngenIA365ERP.Audit;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Caching.Services;
using IngenIA365ERP.Identity;
using IngenIA365ERP.Identity.Seed;
using IngenIA365ERP.Persistence;
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

    builder.Host.UseSerilog();

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
    builder.Services.AddSingleton<IDateTimeService, DateTimeService>();

    // === Identity & Security ===
    builder.Services.AddIdentityServices(builder.Configuration);

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

    // Infrastructure layer DI registrations
    builder.Services.AddPersistenceServices(builder.Configuration);
    // builder.Services.AddCachingServices(builder.Configuration);  // Using MemoryCacheService instead of Redis for local dev

    // Health checks
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Initialize MongoDB collections and indexes
    var mongoInit = app.Services.GetRequiredService<MongoDbInitializer>();
    await mongoInit.InitializeDefaultAsync();

    // Seed Identity data (roles, permissions, admin user)
    if (app.Environment.IsDevelopment())
    {
        await IdentitySeedData.SeedAsync(app.Services);
    }

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSecurityHeaders();
    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseIpRateLimiting();
    app.UseCors("AllowFrontend");
    app.UseTenantResolution();
    app.UseAuthentication();
    app.UseAuthorization();

    // Map Carter endpoints
    app.MapCarter();

    // Health check endpoint
    app.MapHealthChecks("/api/health");

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
