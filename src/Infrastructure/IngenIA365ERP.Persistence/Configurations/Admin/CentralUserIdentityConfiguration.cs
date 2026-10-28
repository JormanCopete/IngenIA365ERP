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
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        // Lockout interno deshabilitado por defecto (research D-11): el lockout
        // progresivo se gestiona desde Redis (LoginAttemptCounter), no desde Identity.
        builder.Property(e => e.LockoutEnabled).HasDefaultValue(false);

        // Feature 017 (research R1, R4): documento de acceso, usuario derivado y nombres.
        // UserName NO cambia: sigue igual al correo, así ningún usuario existente se toca.
        builder.Property(e => e.DocumentType).HasMaxLength(3);
        builder.Property(e => e.DocumentNumber).HasMaxLength(20);
        builder.Property(e => e.DocumentUsername).HasMaxLength(24);
        builder.Property(e => e.FirstName).HasMaxLength(60);
        builder.Property(e => e.OtherNames).HasMaxLength(60);
        builder.Property(e => e.LastName).HasMaxLength(60);
        builder.Property(e => e.SecondLastName).HasMaxLength(60);

        builder.HasIndex(e => new { e.DocumentType, e.DocumentNumber }, "UX_ADM_CentralUsers_Document")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [DocumentNumber] IS NOT NULL");
        builder.HasIndex(e => e.DocumentUsername, "UX_ADM_CentralUsers_DocumentUsername")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [DocumentUsername] IS NOT NULL");

        // El de Identity («EmailIndex») no es único: la unicidad era sólo de aplicación
        // (RequireUniqueEmail). La migración trae una guarda que se niega a crear éste si
        // hay duplicados (diagnóstico A1).
        builder.HasIndex(e => e.NormalizedEmail, "UX_ADM_CentralUsers_NormalizedEmail")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [NormalizedEmail] IS NOT NULL");

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
