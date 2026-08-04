using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class ExchangeRateHistoryConfiguration : IEntityTypeConfiguration<ExchangeRateHistory>
{
    public void Configure(EntityTypeBuilder<ExchangeRateHistory> builder)
    {
        builder.ToTable("ACC_ExchangeRateHistory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_ExchangeRateHistory_PublicId");

        builder.Property(e => e.CurrencyCode).HasMaxLength(5);
        builder.Property(e => e.ExchangeRate).HasPrecision(18, 6);
        builder.Property(e => e.SourceAccountCode).HasMaxLength(15);
        builder.Property(e => e.TargetAccountCode).HasMaxLength(15);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.StatementCode).HasMaxLength(5);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
