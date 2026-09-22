using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>Consignación de cesantías por fondo (feature 010, FR-012): una por corrida y fondo.</summary>
public class SeveranceFundDepositConfiguration : IEntityTypeConfiguration<SeveranceFundDeposit>
{
    public void Configure(EntityTypeBuilder<SeveranceFundDeposit> builder)
    {
        builder.ToTable("PAY_SeveranceFundDeposits");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.DepositedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(60);
        builder.Property(e => e.Amount).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.PayrollRunId, e.SeveranceFundId }).IsUnique()
            .HasDatabaseName("UK_PAY_SeveranceFundDeposits_Run_Fund");

        builder.HasOne(e => e.PayrollRun).WithMany().HasForeignKey(e => e.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SeveranceFund).WithMany().HasForeignKey(e => e.SeveranceFundId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
