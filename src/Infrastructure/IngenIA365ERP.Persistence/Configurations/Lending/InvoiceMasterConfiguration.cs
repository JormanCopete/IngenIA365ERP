using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class InvoiceMasterConfiguration : IEntityTypeConfiguration<InvoiceMaster>
{
    public void Configure(EntityTypeBuilder<InvoiceMaster> builder)
    {
        builder.ToTable("LND_InvoiceMasters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_InvoiceMasters_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20);
        builder.Property(e => e.Barcode).HasMaxLength(120);
        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.Property(e => e.PaymentType).HasMaxLength(3).IsRequired();
        builder.Property(e => e.InvoicedAmount).HasPrecision(18, 3);
        builder.Property(e => e.LastPaymentAmount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
