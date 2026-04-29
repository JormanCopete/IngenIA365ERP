using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class AmortizationConfiguration : IEntityTypeConfiguration<Amortization>
{
    public void Configure(EntityTypeBuilder<Amortization> builder)
    {
        builder.ToTable("ACC_Amortizations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_Amortizations_PublicId");

        builder.Property(e => e.CrossInitialBalance).HasPrecision(18, 2);
        builder.Property(e => e.MovementInitialBalance).HasPrecision(18, 2);
        builder.Property(e => e.OriginalAmount).HasPrecision(18, 2);
        builder.Property(e => e.MonthlyAmount).HasPrecision(18, 2);
        builder.Property(e => e.RemainingBalance).HasPrecision(18, 2);
        builder.Property(e => e.Rate).HasPrecision(5, 2);
        builder.Property(e => e.DocumentCode).HasMaxLength(10);
        builder.Property(e => e.CrossCostCenterCode).HasMaxLength(10);
        builder.Property(e => e.CrossPersonTaxId).HasMaxLength(20);
        builder.Property(e => e.MovementCostCenterCode).HasMaxLength(10);
        builder.Property(e => e.MovementPersonTaxId).HasMaxLength(20);

        builder.HasIndex(e => new { e.PeriodYear, e.AccountId, e.BranchId, e.CostCenterId, e.PersonId, e.DocumentCode }).IsUnique().HasDatabaseName("UK_ACC_Amortizations_Natural");
        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_ACC_Amortizations_AccountId");

        builder.HasOne(e => e.Account).WithMany(a => a.Amortizations).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
