using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("COR_Cities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Cities_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        // Único cuando existe: los registros migrados del SOLIDO pueden venir sin código.
        builder.HasIndex(e => e.LegacyCode).IsUnique().HasFilter("[LegacyCode] IS NOT NULL");
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DepartmentId).IsRequired();

        // Feature 012 (T176): código DIVIPOLA, único entre las vivas; par PlataformaParaInventario.
        builder.Property(e => e.DaneCode).HasMaxLength(5);
        builder.HasIndex(e => e.DaneCode).IsUnique()
            .HasDatabaseName("UK_COR_Cities_DaneCode")
            .HasFilter("[DaneCode] IS NOT NULL AND [IsDeleted] = 0");

        // FK index
        builder.HasIndex(e => e.DepartmentId).HasDatabaseName("IX_COR_Cities_DepartmentId");

        // Relationships
        builder.HasOne(e => e.Department).WithMany(d => d.Cities).HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
