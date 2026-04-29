using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class DirectDebitConfiguration : IEntityTypeConfiguration<DirectDebit>
{
    public void Configure(EntityTypeBuilder<DirectDebit> builder)
    {
        builder.ToTable("PAY_DirectDebits");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.PayrollCompanyId, e.EmployeeId, e.ConceptId, e.SequenceNumber }).IsUnique();

        builder.Property(e => e.SequenceNumber).HasPrecision(12, 0);
        builder.Property(e => e.VoucherCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.InitialAmount).HasPrecision(18, 2);
        builder.Property(e => e.InterestRate).HasPrecision(8, 4);
        builder.Property(e => e.InstallmentAmount).HasPrecision(18, 2);
        builder.Property(e => e.UserName).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EntryUser).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(2).IsRequired();

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Concept).WithMany().HasForeignKey(e => e.ConceptId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
