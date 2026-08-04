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
            context.Logger.LogWarning(
                "No existe ningún master admin y faltan MASTER_ADMIN_EMAIL / MASTER_ADMIN_PASSWORD — " +
                "la instalación no tendrá gobierno hasta sembrarlo (re-ejecute el seed con las variables definidas).");
            return 0;
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
