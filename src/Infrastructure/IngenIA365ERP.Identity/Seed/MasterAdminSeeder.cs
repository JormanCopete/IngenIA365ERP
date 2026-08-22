using IngenIA365ERP.Persistence.Identity;
using IngenIA365ERP.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 004: bootstrap del administrador master en instalaciones greenfield
/// (reemplaza al script SQL congelado 16_Seed_Default_GlobalMasterAdmin.sql).
/// Idempotente: si ya existe un master vivo, no hace nada. Hash BCrypt cost 11
/// (constitución).
///
/// <para>
/// <b>De dónde salen las credenciales.</b> Primero de las variables de entorno
/// <c>MASTER_ADMIN_EMAIL</c> / <c>MASTER_ADMIN_PASSWORD</c>; si no están, de la
/// configuración, en <c>MasterAdmin:Email</c> / <c>MasterAdmin:Password</c>.
/// </para>
///
/// <para>
/// La segunda vía existe porque la primera no sirve en desarrollo: una variable
/// de entorno vive en la terminal que lanzó el proceso y se pierde en cuanto se
/// abre otra, así que arrancar y parar la API obligaba a reponerla cada vez. La
/// configuración se lee de <c>appsettings.Development.local.json</c>, que git
/// ignora y persiste entre reinicios. En despliegue siguen mandando las
/// variables de entorno, que es donde las pone el orquestador.
/// </para>
///
/// <para>
/// El valor no se registra nunca, ni siquiera truncado.
/// </para>
/// </summary>
public sealed class MasterAdminSeeder(IConfiguration configuracion) : IDataSeeder
{
    public int Order => 5;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Admin;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var admin = context.Admin!;

        var masterExists = await admin.Users.AnyAsync(u => u.IsGlobalMasterAdmin && !u.IsDeleted, ct);
        if (masterExists)
            return 0;

        var email = Environment.GetEnvironmentVariable("MASTER_ADMIN_EMAIL")
            ?? configuracion["MasterAdmin:Email"];
        var password = Environment.GetEnvironmentVariable("MASTER_ADMIN_PASSWORD")
            ?? configuracion["MasterAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            // Fallo duro, no aviso. Un arranque sobre base vacia y sin estas variables
            // dejaba la instalacion SIN GOBIERNO y sin error visible: nadie puede
            // administrar nada, el log lo dice una vez entre cientos de lineas, y el
            // sintoma aparece mucho despues, cuando alguien intenta entrar. Es el peor
            // modo de fallo de todo el corte a schema-per-tenant, y cuesta una linea
            // evitarlo.
            throw new InvalidOperationException(
                "No existe ningun administrador maestro y no hay credenciales para sembrarlo. " +
                "Poné MASTER_ADMIN_EMAIL y MASTER_ADMIN_PASSWORD en el entorno, o " +
                "MasterAdmin:Email y MasterAdmin:Password en appsettings.Development.local.json " +
                "(git lo ignora). Sin eso la instalacion queda sin gobierno.");
        }

        var user = new CentralUserIdentity
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11),
            IsGlobalMasterAdmin = true,
            Status = 0, // Active
            CreatedBy = SeedContext.ParametricCreatedBy
        };

        admin.Users.Add(user);
        await admin.SaveChangesAsync(ct);

        context.Logger.LogInformation("Master admin sembrado: {Email} (Id {Id})", email, user.Id);
        return 1;
    }
}
