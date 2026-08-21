using IngenIA365ERP.Persistence.Identity;
using IngenIA365ERP.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 004: bootstrap del administrador master en instalaciones greenfield
/// (reemplaza al script SQL congelado 16_Seed_Default_GlobalMasterAdmin.sql).
/// Idempotente: si ya existe un master vivo, no hace nada. Credenciales por
/// variables de entorno MASTER_ADMIN_EMAIL / MASTER_ADMIN_PASSWORD; hash
/// BCrypt cost 11 (constitución).
/// </summary>
public sealed class MasterAdminSeeder : IDataSeeder
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

        var email = Environment.GetEnvironmentVariable("MASTER_ADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("MASTER_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            // Fallo duro, no aviso. Un arranque sobre base vacia y sin estas variables
            // dejaba la instalacion SIN GOBIERNO y sin error visible: nadie puede
            // administrar nada, el log lo dice una vez entre cientos de lineas, y el
            // sintoma aparece mucho despues, cuando alguien intenta entrar. Es el peor
            // modo de fallo de todo el corte a schema-per-tenant, y cuesta una linea
            // evitarlo.
            throw new InvalidOperationException(
                "No existe ningun administrador maestro y faltan las variables de entorno " +
                "MASTER_ADMIN_EMAIL y MASTER_ADMIN_PASSWORD. Definilas en el entorno del proceso " +
                "antes de arrancar: sin ellas la instalacion queda sin gobierno.");
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
