using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class InvoiceParameterConfiguration : IEntityTypeConfiguration<InvoiceParameter>
{
    public void Configure(EntityTypeBuilder<InvoiceParameter> builder)
    {
        builder.ToTable("COR_InvoiceParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_InvoiceParameters_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        builder.Property(e => e.Tag415).HasMaxLength(20);
        builder.Property(e => e.Tag8020).HasMaxLength(4);
        builder.Property(e => e.Tag3900).HasMaxLength(4);
        builder.Property(e => e.Tag96).HasMaxLength(4);
        builder.Property(e => e.BarcodeType).HasMaxLength(4);
        builder.Property(e => e.InvoicePrintParam).HasMaxLength(2);
        builder.Property(e => e.InvoiceGroup).HasMaxLength(4);

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
