using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class BankReconciliationConfiguration : IEntityTypeConfiguration<BankReconciliation>
{
    public void Configure(EntityTypeBuilder<BankReconciliation> builder)
    {
        builder.ToTable("ACC_BankReconciliations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_BankReconciliations_PublicId");

        builder.Property(e => e.DocumentType).HasMaxLength(5);
        builder.Property(e => e.DocumentNumber).HasMaxLength(20);
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.Property(e => e.DebitAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);
        builder.Property(e => e.IsReconciled).HasDefaultValue(false);
        builder.Property(e => e.IsAdditional).HasDefaultValue(false);
        builder.Property(e => e.IsClosed).HasDefaultValue(false);
        builder.Property(e => e.ModuleCode).HasMaxLength(5);

        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_ACC_BankReconciliations_AccountId");
        builder.HasIndex(e => e.BankId).HasDatabaseName("IX_ACC_BankReconciliations_BankId");

        builder.HasOne(e => e.Account).WithMany(a => a.BankReconciliations).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Bank).WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
