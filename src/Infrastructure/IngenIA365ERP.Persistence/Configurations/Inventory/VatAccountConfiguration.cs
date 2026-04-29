using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class VatAccountConfiguration : IEntityTypeConfiguration<VatAccount>
{
    public void Configure(EntityTypeBuilder<VatAccount> builder)
    {
        builder.ToTable("INV_VatAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.VatRate).HasPrecision(6, 3);
        builder.Property(e => e.AccountType).HasMaxLength(5);
        builder.Property(e => e.AccountCode).HasMaxLength(15);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
