using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class WithholdingParameterConfiguration : IEntityTypeConfiguration<WithholdingParameter>
{
    public void Configure(EntityTypeBuilder<WithholdingParameter> builder)
    {
        builder.ToTable("PAY_WithholdingParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.PayrollCompanyId, e.UvtRangeStart, e.UvtRangeEnd }).IsUnique();

        builder.Property(e => e.Rate).HasPrecision(17, 4);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
