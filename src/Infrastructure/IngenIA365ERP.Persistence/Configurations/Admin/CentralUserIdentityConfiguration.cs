using IngenIA365ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Mapea <see cref="CentralUserIdentity"/> + las 6 tablas estándar de ASP.NET Core
/// Identity a los nombres con prefijo <c>ADM_*</c> (alineado con DDL 15a).
/// Se aplica explícitamente desde <c>AdminDbContext.OnModelCreating</c> tras
/// invocar <c>base.OnModelCreating(modelBuilder)</c> (que es lo que registra los
/// tipos Identity en el modelo).
/// </summary>
public class CentralUserIdentityConfiguration : IEntityTypeConfiguration<CentralUserIdentity>
{
    public void Configure(EntityTypeBuilder<CentralUserIdentity> builder)
    {
        builder.ToTable("ADM_CentralUsers");

        // ASP.NET Identity ya define PK, columnas core, normalized email/username
        // — solo añadimos las columnas custom.
        builder.Property(e => e.MfaSecret).HasMaxLength(512);
        builder.Property(e => e.Status).HasDefaultValue(0);
        builder.Property(e => e.IsGlobalMasterAdmin).HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        // Lockout interno deshabilitado por defecto (research D-11): el lockout
        // progresivo se gestiona desde Redis (LoginAttemptCounter), no desde Identity.
        builder.Property(e => e.LockoutEnabled).HasDefaultValue(false);

        // Soft-delete query filter.
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>
/// Renombra el resto de tablas estándar de Identity (Roles, junctions, claims,
/// logins, tokens) al prefijo <c>ADM_*</c>. Se aplica desde
/// <c>AdminDbContext.OnModelCreating</c>.
/// </summary>
public static class IdentityStandardTableNaming
{
    public static void RenameIdentityTablesToAdminPrefix(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("ADM_Roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("ADM_CentralUserRoles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("ADM_CentralUserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("ADM_CentralUserLogins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("ADM_CentralUserTokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("ADM_RoleClaims");
    }
}
