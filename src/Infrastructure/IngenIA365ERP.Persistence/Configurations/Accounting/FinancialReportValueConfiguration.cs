using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class FinancialReportValueConfiguration : IEntityTypeConfiguration<FinancialReportValue>
{
    public void Configure(EntityTypeBuilder<FinancialReportValue> builder)
    {
        builder.ToTable("ACC_FinancialReportValues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_FinancialReportValues_PublicId");

        builder.Property(e => e.ValueCode).HasMaxLength(20);
        builder.Property(e => e.Description).HasMaxLength(100).IsRequired();

        builder.HasIndex(e => new { e.FormatId, e.ValueId }).IsUnique().HasDatabaseName("UK_ACC_FinancialReportValues_Natural");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
