using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class LendingDocumentConfiguration : IEntityTypeConfiguration<LendingDocument>
{
    public void Configure(EntityTypeBuilder<LendingDocument> builder)
    {
        builder.ToTable("LND_Documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_Documents_PublicId");

        builder.Property(e => e.VoucherType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.AccountCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DocumentType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CheckNumber).HasMaxLength(15).IsRequired();
        builder.Property(e => e.RecordFlag).HasMaxLength(2).IsRequired();
        builder.Property(e => e.BankCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.IsClosed).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IsVoided).HasMaxLength(2).IsRequired();
        builder.Property(e => e.ClosedInPortfolio).HasMaxLength(2);
        builder.Property(e => e.BeneficiaryId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.BeneficiaryCheckId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LegacyCompronte).HasMaxLength(5);
        builder.Property(e => e.DebitAmount).HasPrecision(18, 2);
        builder.Property(e => e.CreditAmount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
