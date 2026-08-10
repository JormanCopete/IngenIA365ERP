using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CashBaseConfiguration : IEntityTypeConfiguration<CashBase>
{
    public void Configure(EntityTypeBuilder<CashBase> builder)
    {
        builder.ToTable("LND_CashBases");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_CashBases_PublicId");

        builder.Property(e => e.CashierCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EntryType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.ReceivedByUser).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DeliveredByUser).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
