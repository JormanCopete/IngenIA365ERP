using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class BookBalanceConfiguration : IEntityTypeConfiguration<BookBalance>
{
    public void Configure(EntityTypeBuilder<BookBalance> builder)
    {
        builder.ToTable("PAY_BookBalances");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.PayrollCompanyId, e.EmployeeId, e.ConceptId, e.SequenceNumber, e.PeriodYear }).IsUnique();

        builder.Property(e => e.SequenceNumber).HasPrecision(12, 0);
        builder.Property(e => e.InitialAmount).HasPrecision(18, 2);
        builder.Property(e => e.JanCharge).HasPrecision(18, 2);
        builder.Property(e => e.JanPayment).HasPrecision(18, 2);
        builder.Property(e => e.FebCharge).HasPrecision(18, 2);
        builder.Property(e => e.FebPayment).HasPrecision(18, 2);
        builder.Property(e => e.MarCharge).HasPrecision(18, 2);
        builder.Property(e => e.MarPayment).HasPrecision(18, 2);
        builder.Property(e => e.AprCharge).HasPrecision(18, 2);
        builder.Property(e => e.AprPayment).HasPrecision(18, 2);
        builder.Property(e => e.MayCharge).HasPrecision(18, 2);
        builder.Property(e => e.MayPayment).HasPrecision(18, 2);
        builder.Property(e => e.JunCharge).HasPrecision(18, 2);
        builder.Property(e => e.JunPayment).HasPrecision(18, 2);
        builder.Property(e => e.JulCharge).HasPrecision(18, 2);
        builder.Property(e => e.JulPayment).HasPrecision(18, 2);
        builder.Property(e => e.AugCharge).HasPrecision(18, 2);
        builder.Property(e => e.AugPayment).HasPrecision(18, 2);
        builder.Property(e => e.SepCharge).HasPrecision(18, 2);
        builder.Property(e => e.SepPayment).HasPrecision(18, 2);
        builder.Property(e => e.OctCharge).HasPrecision(18, 2);
        builder.Property(e => e.OctPayment).HasPrecision(18, 2);
        builder.Property(e => e.NovCharge).HasPrecision(18, 2);
        builder.Property(e => e.NovPayment).HasPrecision(18, 2);
        builder.Property(e => e.DecCharge).HasPrecision(18, 2);
        builder.Property(e => e.DecPayment).HasPrecision(18, 2);

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
