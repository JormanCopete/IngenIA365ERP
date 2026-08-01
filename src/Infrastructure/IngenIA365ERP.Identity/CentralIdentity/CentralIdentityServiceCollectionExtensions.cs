using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Identity.CentralIdentity;

/// <summary>
/// Wire de identidad central (T043). <b>NO se invoca todavía desde Program.cs</b> —
/// eso lo hace Chunk D cuando el login real esté listo. Esta extensión queda
/// disponible para futuro uso y para que los tests de integración la puedan invocar.
///
/// <para>Registra:</para>
/// <list type="bullet">
///   <item><c>IdentityCore&lt;CentralUserIdentity&gt;</c> + Roles + EFStores sobre <c>AdminDbContext</c>.</item>
///   <item><see cref="BcryptPasswordHasher"/> sustituyendo el default PBKDF2.</item>
///   <item>Configuración de <c>IdentityOptions</c>: lockout interno deshabilitado
///         (research D-11), longitud mínima de password 12 chars (FR-043).</item>
///   <item><see cref="ICentralIdentityProvider"/> → <see cref="AspNetCoreIdentityProvider"/>.</item>
///   <item><see cref="IPwnedPasswordService"/> con HttpClient configurado y timeout 2s.</item>
///   <item><see cref="CentralJwtIssuer"/> singleton (reutiliza las RSA keys de Fase 0).</item>
/// </list>
/// </summary>
public static class CentralIdentityServiceCollectionExtensions
{
    public static IServiceCollection AddCentralIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. IdentityCore + EF stores sobre AdminDbContext.
        services
            .AddIdentityCore<CentralUserIdentity>(options =>
            {
                // FR-043: longitud mínima 12, sin obligación de mezclar tipos.
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                // FR-002: email único, case-insensitive (NormalizedEmail).
                options.User.RequireUniqueEmail = true;

                // Research D-11 + A1 del análisis: lockout interno deshabilitado.
                // El lockout progresivo se gestiona en Redis (ILoginAttemptCounter).
                options.Lockout.AllowedForNewUsers = false;
                options.Lockout.MaxFailedAccessAttempts = int.MaxValue;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AdminDbContext>()
            .AddDefaultTokenProviders();   // necesario para password reset + email confirm + 2FA recovery codes

        // 2. Sustituir el PasswordHasher default por BCrypt cost 11.
        services.AddScoped<IPasswordHasher<CentralUserIdentity>, BcryptPasswordHasher>();

        // 3. PwnedPassword con HttpClient nombrado.
        services.Configure<PwnedPasswordOptions>(configuration.GetSection(PwnedPasswordOptions.SectionName));
        services.AddHttpClient<IPwnedPasswordService, PwnedPasswordService>((sp, http) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PwnedPasswordOptions>>().Value;
            http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("IngenIA365ERP/1.0");
        });

        // 4. JWT issuer central — singleton (reutiliza JwtSettings + IRsaKeyProvider).
        //    Doble registro: la abstracción ICentralJwtIssuer (consumida por
        //    handlers de Application) apunta al mismo singleton concreto.
        services.AddSingleton<CentralJwtIssuer>();
        services.AddSingleton<ICentralJwtIssuer>(sp => sp.GetRequiredService<CentralJwtIssuer>());

        // 5. ICentralIdentityProvider (la cara pública para Application).
        services.AddScoped<ICentralIdentityProvider, AspNetCoreIdentityProvider>();

        // 6. Background jobs (T122 — Phase 8).
        services.AddHostedService<Jobs.InvitationExpiryJob>();
        services.AddHostedService<Jobs.PasswordResetTokenCleanupJob>();

        return services;
    }
}
