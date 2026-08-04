using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class IncomeTaxLineConfiguration : IEntityTypeConfiguration<IncomeTaxLine>
{
    public void Configure(EntityTypeBuilder<IncomeTaxLine> builder)
    {
        builder.ToTable("ACC_IncomeTaxLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_IncomeTaxLines_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        builder.Property(e => e.LineCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(100);
        builder.Property(e => e.AccountCode).HasMaxLength(15);
        builder.Property(e => e.TaxRate).HasPrecision(10, 5);
        builder.Property(e => e.BaseAccountCode).HasMaxLength(15);
        builder.Property(e => e.Sign).HasMaxLength(1);

        builder.HasIndex(e => e.LineCode).IsUnique().HasDatabaseName("UK_ACC_IncomeTaxLines_LineCode");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
