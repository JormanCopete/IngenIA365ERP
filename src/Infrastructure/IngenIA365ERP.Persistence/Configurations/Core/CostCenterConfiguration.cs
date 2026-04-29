using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ToTable("COR_CostCenters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_CostCenters_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(20);
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.CompanyName).HasMaxLength(100);
        builder.Property(e => e.CompanyTaxId).HasMaxLength(20);
        builder.Property(e => e.PayrollType).HasDefaultValue((short)0);
        builder.Property(e => e.Period).HasDefaultValue((short)0);
        builder.Property(e => e.PayrollPeriodicity).HasDefaultValue((short)0);
        builder.Property(e => e.PayrollStatus).HasMaxLength(30);

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
