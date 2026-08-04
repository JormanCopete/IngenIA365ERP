using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class StampTaxConfiguration : IEntityTypeConfiguration<StampTax>
{
    public void Configure(EntityTypeBuilder<StampTax> builder)
    {
        builder.ToTable("ACC_StampTaxes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_StampTaxes_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(5);
        builder.Property(e => e.Grade).HasMaxLength(5).IsRequired();
        builder.Property(e => e.DebitAccountCode).HasMaxLength(15);
        builder.Property(e => e.CreditAccountCode).HasMaxLength(15);

        builder.HasIndex(e => e.Grade).IsUnique().HasDatabaseName("UK_ACC_StampTaxes_Grade");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
