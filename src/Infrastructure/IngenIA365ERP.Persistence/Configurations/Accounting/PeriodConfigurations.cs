using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.ToTable("ACC_FiscalYears");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_FiscalYears_PublicId");

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.ClosedBy).HasMaxLength(100);
        builder.Property(e => e.ReopenedBy).HasMaxLength(100);
        builder.Property(e => e.ReopenReason).HasMaxLength(200);

        builder.HasIndex(e => e.Year).IsUnique().HasDatabaseName("UK_ACC_FiscalYears_Year").HasFilter("[IsDeleted] = 0");
        builder.HasOne(e => e.ClosingDocument).WithMany().HasForeignKey(e => e.ClosingDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Periods).WithOne(p => p.FiscalYear).HasForeignKey(p => p.FiscalYearId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.StartDate);
        builder.Ignore(e => e.EndDate);
    }
}

public class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> builder)
    {
        builder.ToTable("ACC_AccountingPeriods");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_AccountingPeriods_PublicId");

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.ClosedBy).HasMaxLength(100);
        builder.Property(e => e.ReopenedBy).HasMaxLength(100);
        builder.Property(e => e.ReopenReason).HasMaxLength(200);

        builder.HasIndex(e => new { e.FiscalYearId, e.Month }).IsUnique().HasDatabaseName("UK_ACC_AccountingPeriods_Year_Month").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.StartDate, e.EndDate }).HasDatabaseName("IX_ACC_AccountingPeriods_Dates");
    }
}
