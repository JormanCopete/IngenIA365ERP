using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class PayrollTransactionConfiguration : IEntityTypeConfiguration<PayrollTransaction>
{
    public void Configure(EntityTypeBuilder<PayrollTransaction> builder)
    {
        builder.ToTable("PAY_PayrollTransactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.PayPeriodId, e.PayrollCompanyId, e.EmployeeId, e.ConceptId, e.SequenceNumber }).IsUnique();

        builder.Property(e => e.SequenceNumber).HasPrecision(12, 0);
        builder.Property(e => e.Time).HasPrecision(10, 0);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.PaymentMethod).HasPrecision(1, 0);
        builder.Property(e => e.UserName).HasMaxLength(50);
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Concept).WithMany().HasForeignKey(e => e.ConceptId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.EmployeeId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
