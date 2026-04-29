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
            e.Property(t => t.LicenseType).HasMaxLength(50).HasDefaultValue("Basic");
            e.Property(t => t.MaxUsers).HasDefaultValue(10);
            e.Property(t => t.ExpirationDate);
            e.Property(t => t.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}
