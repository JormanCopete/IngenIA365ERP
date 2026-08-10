using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class DianReportFormatConfiguration : IEntityTypeConfiguration<DianReportFormat>
{
    public void Configure(EntityTypeBuilder<DianReportFormat> builder)
    {
        builder.ToTable("ACC_DianReportFormats");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_DianReportFormats_PublicId");

        builder.Property(e => e.FormatCode).HasMaxLength(10);
        builder.Property(e => e.Description).HasMaxLength(100);
        builder.Property(e => e.Threshold).HasPrecision(18, 0);
        builder.Property(e => e.MinorTaxId).HasMaxLength(20);
        builder.Property(e => e.BalanceThreshold).HasPrecision(18, 0);
        builder.Property(e => e.DianTaxId).HasMaxLength(15);

        builder.HasIndex(e => new { e.FormatId, e.ConceptId }).IsUnique().HasDatabaseName("UK_ACC_DianReportFormats_Natural");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
