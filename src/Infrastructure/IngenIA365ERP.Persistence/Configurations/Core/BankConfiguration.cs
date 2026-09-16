using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("COR_Banks");
        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict); // feature 009 (FR-088)
        builder.HasIndex(e => e.PersonId);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Banks_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        // Único cuando existe: los registros migrados del SOLIDO pueden venir sin código.
        builder.HasIndex(e => e.LegacyCode).IsUnique().HasFilter("[LegacyCode] IS NOT NULL");
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(30);
        builder.Property(e => e.AccountCode).HasMaxLength(20);
        builder.Property(e => e.VoucherTypeCode).HasMaxLength(10);
        builder.Property(e => e.TransferCode).HasMaxLength(20);
        builder.Property(e => e.AccountClass).HasMaxLength(2);
        builder.Property(e => e.CheckDigitRequired).HasDefaultValue(false);
        builder.Property(e => e.AccountingAccountCode).HasMaxLength(20);
        builder.Property(e => e.PrintFormat).HasMaxLength(2);
        builder.Property(e => e.FinancialTaxRate).HasPrecision(6, 3).HasDefaultValue(0m);
        builder.Property(e => e.FileStructure).HasMaxLength(4);
        builder.Property(e => e.ChargesCommission).HasDefaultValue(false);
        builder.Property(e => e.CommissionAccount).HasMaxLength(20);
        builder.Property(e => e.CommissionAmount).HasPrecision(17, 4);
        builder.Property(e => e.PromptForPrinter).HasDefaultValue(false);
        builder.Property(e => e.ControlSequential).HasMaxLength(2);

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
