using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class DepreciationConfiguration : IEntityTypeConfiguration<Depreciation>
{
    public void Configure(EntityTypeBuilder<Depreciation> builder)
    {
        builder.ToTable("ACC_Depreciations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_Depreciations_PublicId");

        builder.Property(e => e.CrossInitialBalance).HasPrecision(18, 2);
        builder.Property(e => e.MovementInitialBalance).HasPrecision(18, 2);
        builder.Property(e => e.OriginalValue).HasPrecision(18, 2);
        builder.Property(e => e.MonthlyDepreciation).HasPrecision(18, 2);
        builder.Property(e => e.AccumulatedDepreciation).HasPrecision(18, 2);
        builder.Property(e => e.NetValue).HasPrecision(18, 2);
        builder.Property(e => e.DepreciationRate).HasPrecision(5, 2);
        builder.Property(e => e.CrossCostCenterCode).HasMaxLength(10);
        builder.Property(e => e.CrossPersonTaxId).HasMaxLength(20);
        builder.Property(e => e.MovementCostCenterCode).HasMaxLength(10);
        builder.Property(e => e.MovementPersonTaxId).HasMaxLength(20);

        builder.HasIndex(e => new { e.PeriodYear, e.AccountId, e.BranchId, e.CostCenterId, e.PersonId }).IsUnique().HasDatabaseName("UK_ACC_Depreciations_Natural");
        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_ACC_Depreciations_AccountId");

        builder.HasOne(e => e.Account).WithMany(a => a.Depreciations).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
