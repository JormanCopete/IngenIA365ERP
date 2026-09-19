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

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        // Un tramo por plan y rango (2026-09-19: los tramos son del plan de nómina, no de la empresa del legado).
        builder.HasIndex(e => new { e.PayrollPlanId, e.UvtRangeStart, e.UvtRangeEnd }).IsUnique().HasDatabaseName("UK_PAY_WithholdingParameters_Plan_Range");
        builder.HasOne(e => e.PayrollPlan)
            .WithMany()
            .HasForeignKey(e => e.PayrollPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Rate).HasPrecision(17, 4);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
