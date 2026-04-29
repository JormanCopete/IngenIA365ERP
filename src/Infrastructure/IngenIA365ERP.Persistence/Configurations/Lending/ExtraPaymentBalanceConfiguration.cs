using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class ExtraPaymentBalanceConfiguration : IEntityTypeConfiguration<ExtraPaymentBalance>
{
    public void Configure(EntityTypeBuilder<ExtraPaymentBalance> builder)
    {
        builder.ToTable("LND_ExtraPaymentBalances");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_ExtraPaymentBalances_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Balance).HasPrecision(18, 2);
        builder.Property(e => e.OpeningBalance).HasPrecision(18, 2);
        builder.Property(e => e.DebitAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
