using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class PayrollPlanConfiguration : IEntityTypeConfiguration<PayrollPlan>
{
    public void Configure(EntityTypeBuilder<PayrollPlan> builder)
    {
        builder.ToTable("PAY_PayrollPlans");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_PAY_PayrollPlans_Code");
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();

        // Exactamente un plan por defecto por cooperativa. El filtro se escribe en
        // T-SQL y ProviderModelConventions lo traduce al motor activo.
        builder.HasIndex(e => e.IsDefault)
            .IsUnique()
            .HasFilter("[IsDefault] = 1")
            .HasDatabaseName("UX_PAY_PayrollPlans_Default");

        builder.Ignore(e => e.DaysInPeriod);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
