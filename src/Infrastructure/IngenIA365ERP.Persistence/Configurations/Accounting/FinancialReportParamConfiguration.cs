using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class FinancialReportParamConfiguration : IEntityTypeConfiguration<FinancialReportParam>
{
    public void Configure(EntityTypeBuilder<FinancialReportParam> builder)
    {
        builder.ToTable("ACC_FinancialReportParams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_FinancialReportParams_PublicId");

        builder.Property(e => e.ParameterValue).HasMaxLength(50).IsRequired();

        builder.HasIndex(e => new { e.FormatId, e.ConceptId, e.ParameterOption, e.ParameterValue }).IsUnique().HasDatabaseName("UK_ACC_FinancialReportParams_Natural");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
