using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class FinancialReportConfiguration : IEntityTypeConfiguration<FinancialReport>
{
    public void Configure(EntityTypeBuilder<FinancialReport> builder)
    {
        builder.ToTable("ACC_FinancialReports");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_FinancialReports_PublicId");

        builder.Property(e => e.VerificationDigit).HasMaxLength(1);
        builder.Property(e => e.LastName1).HasMaxLength(80);
        builder.Property(e => e.LastName2).HasMaxLength(80);
        builder.Property(e => e.FirstName).HasMaxLength(80);
        builder.Property(e => e.CompanyName).HasMaxLength(80);
        builder.Property(e => e.FirstName2).HasMaxLength(50);
        builder.Property(e => e.SecondName).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(100);
        builder.Property(e => e.SavingsAccount).HasMaxLength(50);
        builder.Property(e => e.ProcessedByLegacySystem).HasMaxLength(1);
        builder.Property(e => e.Value1).HasPrecision(18, 2);
        builder.Property(e => e.Value2).HasPrecision(18, 2);
        builder.Property(e => e.Value3).HasPrecision(18, 2);
        builder.Property(e => e.Value4).HasPrecision(18, 2);
        builder.Property(e => e.Value5).HasPrecision(18, 2);
        builder.Property(e => e.Value6).HasPrecision(18, 2);
        builder.Property(e => e.Value7).HasPrecision(18, 2);
        builder.Property(e => e.Value8).HasPrecision(18, 2);
        builder.Property(e => e.Value9).HasPrecision(18, 2);
        builder.Property(e => e.Value10).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.FormatId, e.ConceptId, e.PersonId, e.AccountId, e.Year }).IsUnique().HasDatabaseName("UK_ACC_FinancialReports_Natural");

        builder.HasOne(e => e.Account).WithMany(a => a.FinancialReports).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
