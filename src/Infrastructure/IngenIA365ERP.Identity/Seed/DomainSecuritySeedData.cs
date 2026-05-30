using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Seed para la tabla de dominio <c>SEC_Users</c> (la que consume el
/// <c>LoginCommandHandler</c> del flujo Phase 0/US1). Convive con
/// <see cref="IdentitySeedData"/> que cubre la tabla legacy de
/// ASP.NET Identity (<c>ApplicationUsers</c>) usada por el endpoint
/// previo.
///
/// Credenciales sembradas: <c>admin@ingenia365.com</c> / <c>Admin@Temporal2024!</c>
/// (BCrypt cost 11 — SC-008). En dev, <c>MustChangePassword=false</c> para
/// permitir login directo desde el front. En producción, este seed debe
/// reemplazarse por aprovisionamiento explícito con contraseña aleatoria
/// y <c>MustChangePassword=true</c>.
///
/// Comportamiento al existir el usuario:
///  - Si está vivo, hash es BCrypt y verifica → no toca nada (idempotente).
///  - Si el hash NO es formato BCrypt (p. ej. PBKDF2 legacy <c>AQAAAA…</c>)
///    se rehidrata SIEMPRE — la fila es inservible para BCrypt.Verify y
///    bloquearía el login en cualquier entorno.
///  - Si el hash es BCrypt pero no verifica / cuenta soft-deleted / lockout
///    activo → en Development se rehidrata; en otros entornos solo se loguea.
/// </summary>
public static class DomainSecuritySeedData
{
    public const string AdminEmail = "admin@ingenia365.com";
    public const string AdminPassword = "Admin@Temporal2024!";
    private const int BcryptCost = 11;

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<User>>();
        var env = scope.ServiceProvider.GetService<IHostEnvironment>();
        var isDevelopment = env?.IsDevelopment() ?? false;

        try
        {
            var existing = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == AdminEmail || u.Email == AdminEmail);

            if (existing is null)
            {
                var admin = new User
                {
                    Username = AdminEmail,
                    Email = AdminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, workFactor: BcryptCost),
                    IsActive = true,
                    IsEmailVerified = true,
                    IsMfaEnabled = false,
                    MustChangePassword = false,
                    IsSaasOperator = true,
                    LastPasswordChangeAt = DateTime.UtcNow,
                    CreatedBy = "Seed",
                    UpdatedBy = "Seed"
                };
                db.Users.Add(admin);
                await db.SaveChangesAsync(default);
                await AssignCompanyAdminRoleAsync(db, admin.Id, logger);
                logger.LogInformation("Admin de dominio sembrado en SEC_Users: {Email}.", AdminEmail);
                return;
            }

            var isBcryptHash = IsBcrypt(existing.PasswordHash);
            var passwordOk = isBcryptHash && TryVerify(AdminPassword, existing.PasswordHash);

            logger.LogInformation(
                "Admin de dominio existe en SEC_Users (Id={Id}, IsActive={Active}, IsDeleted={Deleted}, " +
                "MustChangePassword={MustChange}, LockoutEndAt={Lockout}, FailedAttempts={Failed}, " +
                "hashEsBCrypt={IsBcrypt}, passwordVerifica={PwOk}).",
                existing.Id, existing.IsActive, existing.IsDeleted,
                existing.MustChangePassword, existing.LockoutEndAt, existing.FailedLoginAttempts,
                isBcryptHash, passwordOk);

            if (passwordOk && existing.IsActive && !existing.IsDeleted
                && (existing.LockoutEndAt is null || existing.LockoutEndAt < DateTime.UtcNow)
                && existing.FailedLoginAttempts == 0)
            {
                return; // sano — no toca nada.
            }

            // Caso 1: hash no es BCrypt (formato legacy / corrupto) → rehidratar
            // SIEMPRE, sin importar el entorno. La fila es inutilizable como está.
            // Caso 2: hash BCrypt válido pero alguna condición de bloqueo →
            // rehidratar solo en Development; en otros entornos solo loguear.
            if (!isBcryptHash || isDevelopment)
            {
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword, workFactor: BcryptCost);
                existing.IsActive = true;
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedBy = null;
                existing.LockoutEndAt = null;
                existing.FailedLoginAttempts = 0;
                existing.MustChangePassword = false;
                existing.IsSaasOperator = true;
                existing.LastPasswordChangeAt = DateTime.UtcNow;
                existing.UpdatedBy = "Seed";

                await db.SaveChangesAsync(default);
                await AssignCompanyAdminRoleAsync(db, existing.Id, logger);

                var reason = !isBcryptHash
                    ? "hash legacy/no-BCrypt detectado — reseteado"
                    : "hash BCrypt OK pero cuenta bloqueada/inactiva/soft-deleted — reseteada en dev";
                logger.LogWarning("Admin de dominio rehidratado: {Reason}.", reason);
            }
            else
            {
                logger.LogWarning(
                    "Admin de dominio inconsistente y entorno no-Development. No se rehidrata automáticamente — " +
                    "revisa la fila manualmente o ejecuta el seeder en Development.");
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 207 or 208)
        {
            logger.LogError(ex,
                "SEC_Users no está alineada con la entidad User. " +
                "Ejecuta database/migration/18_SEC_Users_BackfillColumns.sql " +
                "y reinicia el API. El login Phase 0 no funcionará hasta entonces.");
        }
    }

    /// <summary>
    /// Asocia el admin a <c>CompanyAdmin</c> (rol SaaS-global sembrado por
    /// BuiltInRolesSeeder). Idempotente: si ya está asignado, no hace nada.
    /// Sin esta asignación, el JWT no traería claims <c>perm</c> y todo
    /// endpoint con <c>[RequirePermission]</c> respondería 404.
    /// </summary>
    private static async Task AssignCompanyAdminRoleAsync(
        IApplicationDbContext db, int userId, ILogger logger)
    {
        try
        {
            var companyAdmin = await db.Roles
                .IgnoreQueryFilters()
                .Where(r => r.Code == "CompanyAdmin" && r.TenantId == null)
                .Select(r => new { r.Id })
                .FirstOrDefaultAsync();
            if (companyAdmin is null)
            {
                logger.LogWarning(
                    "Rol CompanyAdmin no encontrado en SEC_Roles. " +
                    "Verifica que BuiltInRolesSeeder corrió antes de DomainSecuritySeedData.");
                return;
            }

            var already = await db.UserRoles
                .IgnoreQueryFilters()
                .AnyAsync(ur => ur.UserId == userId && ur.RoleId == companyAdmin.Id);
            if (already) return;

            db.UserRoles.Add(new IngenIA365ERP.Domain.Entities.Security.UserRole
            {
                UserId = userId,
                RoleId = companyAdmin.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = "Seed",
                CreatedBy = "Seed",
                UpdatedBy = "Seed"
            });
            await db.SaveChangesAsync(default);
            logger.LogInformation("Admin asignado al rol CompanyAdmin.");
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 207 or 208)
        {
            logger.LogError(ex,
                "No se pudo asignar CompanyAdmin al admin (SEC_UserRoles desfasada). " +
                "Ejecuta database/migration/19_SEC_Roles_UserRoles_BackfillColumns.sql.");
        }
    }

    /// <summary>Hash BCrypt válido empieza con <c>$2a$</c>, <c>$2b$</c> o <c>$2y$</c>.</summary>
    private static bool IsBcrypt(string? hash) =>
        !string.IsNullOrEmpty(hash)
        && hash.Length >= 60
        && (hash.StartsWith("$2a$", StringComparison.Ordinal)
            || hash.StartsWith("$2b$", StringComparison.Ordinal)
            || hash.StartsWith("$2y$", StringComparison.Ordinal));

    private static bool TryVerify(string plain, string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return false;
        try { return BCrypt.Net.BCrypt.Verify(plain, hash); }
        catch { return false; }
    }
}
