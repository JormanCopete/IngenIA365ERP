using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Identity.Configuration;
using IngenIA365ERP.Identity.KeyManagement;
using IngenIA365ERP.Identity.Models;
using IngenIA365ERP.Identity.Policies;
using IngenIA365ERP.Identity.Services;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

        // === Data Protection ===
        //
        // Este llavero cifra el secreto TOTP de cada persona
        // (AspNetCoreIdentityProvider), la clave de cada adjunto
        // (AttachmentEncryptionService) y lo que pase por EncryptionService.
        // Perderlo no es una molestia: deja a todo el mundo sin segundo factor y
        // vuelve ilegibles los archivos cifrados.
        //
        // Faltaba PersistKeysTo*. Sin esa llamada las claves van al perfil del
        // proceso, que en un contenedor Linux es su capa escribible: se pierden al
        // rotar el pod, y con dos réplicas cada una tiene la suya, así que lo que
        // cifra un pod el otro no lo abre.
        //
        // Van a la base administrativa, que es única y compartida, y así entran en
        // los respaldos que ya existen. SetApplicationName es el discriminador de
        // aislamiento: si cambia, el llavero deja de reconocerse. No tocarlo.
        services.AddDataProtection()
            .PersistKeysToDbContext<AdminDbContext>()
            .SetApplicationName("IngenIA365ERP")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

        // === WebAuthn / passkeys ===
        //
        // ValidateOnStart y no validación perezosa: si el dominio o los orígenes
        // están mal, el navegador rechaza con SecurityError SIN hacer una sola
        // petición al servidor. No habría 4xx, ni registro, ni forma de enterarse
        // salvo porque alguien avise de que «el botón no hace nada». Es preferible
        // que el proceso no arranque.
        services.AddOptions<WebAuthnOptions>()
            .Bind(configuration.GetSection(WebAuthnOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<WebAuthnOptions>, WebAuthnOptionsValidator>();
        services.AddSingleton<
            Application.Common.Interfaces.Identity.IWebAuthnService,
            WebAuthn.WebAuthnService>();

        // === Register services ===
        //
        // IIdentityAuthenticationService e IEncryptionService estaban aquí y no
        // los inyectaba nadie. El primero era una capa de autenticación entera
        // —con sus propios LoginCommand/RefreshTokenCommand— del diseño anterior
        // a CQRS; el segundo, un cifrado genérico que nadie llamaba (el vivo es
        // AttachmentEncryptionService). Se fueron con sus implementaciones.
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPermissionService, PermissionService>();

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
