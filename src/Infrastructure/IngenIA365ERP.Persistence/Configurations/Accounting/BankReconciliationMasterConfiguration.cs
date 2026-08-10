using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class BankReconciliationMasterConfiguration : IEntityTypeConfiguration<BankReconciliationMaster>
{
    public void Configure(EntityTypeBuilder<BankReconciliationMaster> builder)
    {
        builder.ToTable("ACC_BankReconciliationMasters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_BankReconciliationMasters_PublicId");

        builder.Property(e => e.PeriodCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.InitialBalance).HasPrecision(18, 2);
        builder.Property(e => e.FinalBalance).HasPrecision(18, 2);
        builder.Property(e => e.IsClosed).HasDefaultValue(false);

        builder.HasIndex(e => new { e.AccountId, e.BankId, e.PeriodCode }).IsUnique().HasDatabaseName("UK_ACC_BankReconciliationMasters_Natural");

        builder.HasOne(e => e.Account).WithMany(a => a.BankReconciliationMasters).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Bank).WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
