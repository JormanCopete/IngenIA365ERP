using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class PayrollEntryConfiguration : IEntityTypeConfiguration<PayrollEntry>
{
    public void Configure(EntityTypeBuilder<PayrollEntry> builder)
    {
        builder.ToTable("PAY_PayrollEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.Cycle, e.PayrollCompanyId, e.EmployeeId }).IsUnique();

        builder.Property(e => e.EntryType).HasPrecision(3, 0);
        builder.Property(e => e.AuthorizationNumber).HasPrecision(12, 0);
        builder.Property(e => e.IncapacityAmount).HasPrecision(18, 2);
        builder.Property(e => e.UpcAmount).HasPrecision(18, 2);
        builder.Property(e => e.NewEntity).HasMaxLength(10).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(20).IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
