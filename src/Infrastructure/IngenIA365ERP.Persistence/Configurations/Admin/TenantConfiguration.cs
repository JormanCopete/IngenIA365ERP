using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("ADM_Tenants");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.SchemaName).IsUnique();
        builder.HasIndex(e => e.Subdomain).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SchemaName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Subdomain).HasMaxLength(100);
        builder.Property(e => e.PlanType).HasMaxLength(50).HasDefaultValue("Basic");
        builder.Property(e => e.DatabaseName).HasMaxLength(100);

        // Dos cooperativas NO pueden apuntar a la misma base: seria un fallo de
        // aislamiento silencioso, con las dos leyendo y escribiendo lo mismo.
        // Nota para SQL Server: alli un indice unico admite un solo NULL, asi que
        // toda cooperativa debe recibir DatabaseName al aprovisionarse. En
        // PostgreSQL, el motor vivo, los NULL se consideran distintos.
        builder.HasIndex(e => e.DatabaseName).IsUnique();

        builder.Property(e => e.ConnectionString).HasMaxLength(500);
        builder.Property(e => e.AuditDatabaseName).HasMaxLength(100);
        builder.Property(e => e.MigrationsVersion).HasMaxLength(200);
        builder.Property(e => e.ProvisioningError).HasMaxLength(2000);
        // Obligatoria y CON default de base, por la misma razon que las de abajo:
        // TenantDbContext escribe esta tabla sin conocer esta columna.
        builder.Property(e => e.ProvisioningState)
            .HasMaxLength(30).IsRequired().HasDefaultValue("Pending");
        // Defaults de BD (feature 004): ADM_Tenants tambien la escribe el store
        // multitenant (ErpTenantInfo/TenantDbContext), que no conoce estas
        // columnas NOT NULL — sin default el INSERT de interop fallaria.
        builder.Property(e => e.ContactEmail).HasMaxLength(200).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(e => e.StorageLimitMb).HasDefaultValue(5120L);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.MaxUsers).HasDefaultValue(10);
        builder.Property(e => e.ContactPhone).HasMaxLength(30);

        builder.HasMany(e => e.Subscriptions).WithOne(s => s.Tenant).HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Settings).WithOne(s => s.Tenant).HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
