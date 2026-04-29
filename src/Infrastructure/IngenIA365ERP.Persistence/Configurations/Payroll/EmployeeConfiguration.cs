using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("PAY_Employees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CostCenterId).HasMaxLength(8).IsRequired();
        builder.Property(e => e.IdentificationNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(30).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(30).IsRequired();
        builder.Property(e => e.IssuedAt).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(14).IsRequired();
        builder.Property(e => e.Mobile).HasMaxLength(14).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(60).IsRequired();
        builder.Property(e => e.BankId).HasMaxLength(4).IsRequired();
        builder.Property(e => e.BankAccountNumber).HasMaxLength(25).IsRequired();
        builder.Property(e => e.TerminationCause).HasMaxLength(4).IsRequired();
        builder.Property(e => e.Salary).HasPrecision(18, 2);
        builder.Property(e => e.WithholdingTaxRate).HasPrecision(7, 4);

        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.PersonId);
        builder.HasIndex(e => e.PayrollCompanyId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
