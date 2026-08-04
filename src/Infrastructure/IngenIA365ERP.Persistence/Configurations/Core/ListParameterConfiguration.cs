using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class ListParameterConfiguration : IEntityTypeConfiguration<ListParameter>
{
    public void Configure(EntityTypeBuilder<ListParameter> builder)
    {
        builder.ToTable("COR_ListParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_ListParameters_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        builder.Property(e => e.Description).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ListType).HasMaxLength(4);
        builder.Property(e => e.ValidateExpiryDate).HasDefaultValue(false);

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
