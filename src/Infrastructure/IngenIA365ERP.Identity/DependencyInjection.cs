using System.Security.Cryptography;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Identity.Configuration;
using IngenIA365ERP.Identity.KeyManagement;
using IngenIA365ERP.Identity.Models;
using IngenIA365ERP.Identity.Policies;
using IngenIA365ERP.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? new JwtSettings();
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // === ErpIdentityDbContext (legacy, multi-motor desde feature 004) ===
        // Mapea tablas del esquema operativo cuyo DDL gobiernan las migraciones
        // de ApplicationDbContext — por eso NUNCA migra (MigrationsTarget.None).
        // El proveedor y la cadena vienen de la seccion Database via el
        // configurador unico registrado por AddPersistenceServices.
        services.AddDbContext<ErpIdentityDbContext>((sp, options) =>
        {
            var configurator = sp.GetRequiredService<Persistence.Providers.IDbProviderConfigurator>();
            var dbOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Persistence.Providers.DatabaseOptions>>().Value;
            configurator.Configure(options, dbOptions.GetActiveConnectionString(), Persistence.Providers.MigrationsTarget.None);
        });

        // === ASP.NET Core Identity ===
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Password policy
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 4;

                // Lockout
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User
                options.User.RequireUniqueEmail = false; // Multi-tenant: same email in different tenants
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<ErpIdentityDbContext>()
            .AddDefaultTokenProviders();

        // === RSA key for JWT ===
        var rsa = RSA.Create();
        if (File.Exists(jwtSettings.PrivateKeyPath))
        {
            var keyPem = File.ReadAllText(jwtSettings.PrivateKeyPath);
            rsa.ImportFromPem(keyPem);
        }
        else
        {
            // Fail-fast en Production: una clave efímera invalidaría todos los
            // tokens en cada reinicio, en silencio (hardening post-T118).
            KeyManagement.RsaKeyGuard.ThrowIfProduction(jwtSettings.PrivateKeyPath);
            rsa = RSA.Create(2048);
        }

        // === JWT Bearer Authentication ===
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

        // === Authorization with Permission policies ===
        services.AddAuthorization();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // === Data Protection (for EncryptionService) ===
        services.AddDataProtection()
            .SetApplicationName("IngenIA365ERP")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

        // === Register services ===
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IIdentityAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IEncryptionService, EncryptionService>();

        // Feature 004 (T037): seeders Phase 0 integrados al framework de
        // seeding — corren tras las migraciones del inicializador.
        services.AddScoped<Persistence.Seeding.IDataSeeder, Seed.MasterAdminSeeder>();
        services.AddScoped<Persistence.Seeding.IDataSeeder, Seed.PhaseZeroSecuritySeeder>();

        // === Fase 0 — US1 ===
        services.AddSingleton<IRsaKeyProvider, RsaKeyProvider>();
        services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IMfaBackupCodeGenerator, MfaBackupCodeGenerator>();
        services.AddScoped<IPasswordPolicyEnforcer, PasswordPolicyEnforcer>();
        services.AddScoped<IUserPermissionResolver, UserPermissionResolver>();

        return services;
    }

    /// <summary>
    /// Extension to add a permission-based authorization policy.
    /// Usage: builder.Services.AddPermissionPolicy("Accounting.ChartOfAccounts.Create");
    /// </summary>
    public static IServiceCollection AddPermissionPolicy(this IServiceCollection services, string permissionCode)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(permissionCode, policy =>
                policy.Requirements.Add(new PermissionRequirement(permissionCode)));
        return services;
    }
}
