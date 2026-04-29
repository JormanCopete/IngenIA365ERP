using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class VacationLiquidationConfiguration : IEntityTypeConfiguration<VacationLiquidation>
{
    public void Configure(EntityTypeBuilder<VacationLiquidation> builder)
    {
        builder.ToTable("PAY_VacationLiquidations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.ConceptId).HasPrecision(6, 0);
        builder.Property(e => e.SequenceNumber).HasPrecision(12, 0);
        builder.Property(e => e.Time).HasPrecision(10, 0);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.CostCenterCode).HasMaxLength(8).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RecordType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IsAccountingPosted).HasMaxLength(1).IsRequired();

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
