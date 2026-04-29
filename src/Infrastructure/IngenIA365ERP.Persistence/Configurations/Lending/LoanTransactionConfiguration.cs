using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class LendingTransactionConfiguration : IEntityTypeConfiguration<LendingTransaction>
{
    public void Configure(EntityTypeBuilder<LendingTransaction> builder)
    {
        builder.ToTable("LND_Transactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_Transactions_PublicId");

        builder.Property(e => e.VoucherType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AccountCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.TransactionCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.CostCenterId).HasMaxLength(10).IsRequired();
        builder.Property(e => e.BranchId).HasMaxLength(5).IsRequired();
        builder.Property(e => e.SecondaryAccount).HasMaxLength(15).IsRequired();
        builder.Property(e => e.SearchCode).HasMaxLength(8).IsRequired();
        builder.Property(e => e.BankCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CrossDocumentType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CrossDocumentNumber).HasMaxLength(20);
        builder.Property(e => e.SecondaryCostCenter).HasMaxLength(10).IsRequired();
        builder.Property(e => e.IsAdvancePayment).HasMaxLength(2).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.Property(e => e.IdentificationNumber).HasMaxLength(20);
        builder.Property(e => e.InvoiceNumber).HasMaxLength(20);
        builder.Property(e => e.UserFullName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Period).HasMaxLength(8).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(100).IsRequired();
        builder.Property(e => e.CheckNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CodeudorCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.OverdraftUser).HasMaxLength(20);
        builder.Property(e => e.TransactionSource).HasMaxLength(3).IsRequired();
        builder.Property(e => e.ReliquidationFlag).HasMaxLength(2);

        builder.Property(e => e.DebitAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);
        builder.Property(e => e.InterestRate).HasPrecision(10, 6);
        builder.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        builder.Property(e => e.LocalCheckAmount).HasPrecision(18, 2);
        builder.Property(e => e.OtherCheckAmount).HasPrecision(18, 2);
        builder.Property(e => e.WithholdingBase).HasPrecision(18, 2);
        builder.Property(e => e.OverdraftAmount).HasPrecision(18, 3);
        builder.Property(e => e.ReliquidatedInstallment).HasPrecision(18, 3);

        builder.HasIndex(e => e.TransactionDate).HasDatabaseName("IX_LND_Transactions_TransactionDate");
        builder.HasIndex(e => new { e.PersonCode, e.CreditLineId, e.PortfolioNumber }).HasDatabaseName("IX_LND_Transactions_PersonLine");

        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
