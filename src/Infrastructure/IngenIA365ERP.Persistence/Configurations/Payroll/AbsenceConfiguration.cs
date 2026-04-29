using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class AbsenceConfiguration : IEntityTypeConfiguration<Absence>
{
    public void Configure(EntityTypeBuilder<Absence> builder)
    {
        builder.ToTable("PAY_Absences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.PayrollCompanyId, e.EmployeeId, e.ConceptId, e.SequenceNumber }).IsUnique();

        builder.Property(e => e.SequenceNumber).HasPrecision(12, 0);
        builder.Property(e => e.BaseAmount).HasPrecision(18, 2);
        builder.Property(e => e.SerialNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ExtensionSequence).HasPrecision(12, 0);

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Concept).WithMany().HasForeignKey(e => e.ConceptId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
