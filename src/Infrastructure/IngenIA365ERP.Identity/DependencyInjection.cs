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

        // === Clave RSA de los JWT ===
        //
        // Una sola, la del proveedor, para firmar Y para validar.
        //
        // Antes había dos: el proveedor cargaba el PEM por su lado y aquí se
        // cargaba OTRA vez en un RSA distinto. Con el archivo presente daba
        // igual —mismo material—, pero sin él cada rama generaba su propia
        // clave efímera y el sistema quedaba firmando con una y validando con
        // otra: todo respondía 401, sin un solo error en el log. Es el fallo
        // que ya se cazó una vez en T118 y que la duplicación mantenía vivo.
        //
        // Se registra ANTES de AddJwtBearer porque el validador lo resuelve.

        services.AddSingleton<IRsaKeyProvider, RsaKeyProvider>();

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
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

        // La clave se inyecta aparte porque resolverla exige el contenedor, y
        // dentro del lambda de AddJwtBearer todavía no lo hay.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IRsaKeyProvider>((options, claves) =>
                options.TokenValidationParameters.IssuerSigningKey = claves.GetSecurityKey());
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
        // ITotpService y IMfaBackupCodeGenerator estaban registrados aquí y no
        // los llamaba nadie: el TOTP y los códigos de recuperación vivos los
        // hace AspNetCoreIdentityProvider con OtpNet y ASP.NET Identity.
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
