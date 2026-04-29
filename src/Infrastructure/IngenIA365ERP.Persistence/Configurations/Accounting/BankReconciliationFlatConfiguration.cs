using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class BankReconciliationFlatConfiguration : IEntityTypeConfiguration<BankReconciliationFlat>
{
    public void Configure(EntityTypeBuilder<BankReconciliationFlat> builder)
    {
        builder.ToTable("ACC_BankReconciliationFlats");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_BankReconciliationFlats_PublicId");

        builder.Property(e => e.AccountCode).HasMaxLength(15);
        builder.Property(e => e.AccountType).HasMaxLength(20);
        builder.Property(e => e.TransactionCode).HasMaxLength(50);
        builder.Property(e => e.AccountNumber).HasMaxLength(50);
        builder.Property(e => e.TransactionDate).HasMaxLength(10);
        builder.Property(e => e.DocumentNumber).HasMaxLength(50);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.TransactionType).HasMaxLength(5);
        builder.Property(e => e.UserName).HasMaxLength(50);
        builder.Property(e => e.IsProcessed).HasDefaultValue(false);
        builder.Property(e => e.Description).HasMaxLength(200);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
