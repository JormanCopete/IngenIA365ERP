using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.MultiTenancy;

public class TenantDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    public DbSet<ErpTenantInfo> Tenants => Set<ErpTenantInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ErpTenantInfo>(e =>
        {
            e.ToTable("ADM_Tenants", "dbo");
            e.HasKey(t => t.InternalId);
            e.Property(t => t.InternalId).HasColumnName("Id").ValueGeneratedOnAdd();
            e.Property(t => t.PublicId).HasColumnName("PublicId");
            e.Property(t => t.Identifier).HasMaxLength(100).IsRequired();
            e.HasIndex(t => t.Identifier).IsUnique();
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.ConnectionString).HasMaxLength(500);
            e.Property(t => t.SchemaName).HasMaxLength(100).HasDefaultValue("dbo");
            // Ambas existen en ADM_Tenants desde el modelo de base por cooperativa.
            // Este contexto ve un subconjunto de la tabla a proposito; estas dos
            // entran porque sin ellas el directorio no puede distinguir una
            // cooperativa lista de una a medio aprovisionar.
            e.Property(t => t.DatabaseName).HasMaxLength(63);
            // Con default de base: la columna es NOT NULL DEFAULT 'Pending' en ADM_Tenants y
            // este contexto la mapea como texto nullable. Sin el default aqui, EF la incluye
            // en el INSERT con NULL explicito cuando nadie la asigna, y el default de base
            // no aplica (solo aplica si la columna se omite). Lo destaparon las seis clases
            // sobre ApiTestFixture, que sembraban la cooperativa demo sin asignarla.
            e.Property(t => t.ProvisioningState).HasMaxLength(50).HasDefaultValue("Pending");
            e.Property(t => t.LicenseType).HasMaxLength(50).HasDefaultValue("Basic");
            e.Property(t => t.MaxUsers).HasDefaultValue(10);
            e.Property(t => t.ExpirationDate);
            e.Property(t => t.CreatedAt);
        });
    }
}
