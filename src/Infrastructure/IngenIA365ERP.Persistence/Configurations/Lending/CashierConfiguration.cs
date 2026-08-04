using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CashierConfiguration : IEntityTypeConfiguration<Cashier>
{
    public void Configure(EntityTypeBuilder<Cashier> builder)
    {
        builder.ToTable("LND_Cashiers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_Cashiers_PublicId");

        builder.Property(e => e.CashierCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.Property(e => e.VoucherType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(50).IsRequired();
        builder.Property(e => e.PrinterName).HasMaxLength(60).IsRequired();


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
