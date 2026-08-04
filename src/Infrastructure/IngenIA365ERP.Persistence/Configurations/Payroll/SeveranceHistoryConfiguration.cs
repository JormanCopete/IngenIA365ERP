using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class SeveranceHistoryConfiguration : IEntityTypeConfiguration<SeveranceHistory>
{
    public void Configure(EntityTypeBuilder<SeveranceHistory> builder)
    {
        builder.ToTable("PAY_SeveranceHistory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.SalaryBase).HasPrecision(18, 5);
        builder.Property(e => e.AdvanceAmount).HasPrecision(18, 3);
        builder.Property(e => e.InterestAmount).HasPrecision(18, 3);
        builder.Property(e => e.Resolution).HasMaxLength(30);
        builder.Property(e => e.Destination).HasMaxLength(100);
        builder.Property(e => e.UserName).HasMaxLength(14);

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
