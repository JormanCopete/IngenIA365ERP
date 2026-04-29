using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("ACC_Budgets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_Budgets_PublicId");

        builder.Property(e => e.JanBudget).HasPrecision(18, 2);
        builder.Property(e => e.FebBudget).HasPrecision(18, 2);
        builder.Property(e => e.MarBudget).HasPrecision(18, 2);
        builder.Property(e => e.AprBudget).HasPrecision(18, 2);
        builder.Property(e => e.MayBudget).HasPrecision(18, 2);
        builder.Property(e => e.JunBudget).HasPrecision(18, 2);
        builder.Property(e => e.JulBudget).HasPrecision(18, 2);
        builder.Property(e => e.AugBudget).HasPrecision(18, 2);
        builder.Property(e => e.SepBudget).HasPrecision(18, 2);
        builder.Property(e => e.OctBudget).HasPrecision(18, 2);
        builder.Property(e => e.NovBudget).HasPrecision(18, 2);
        builder.Property(e => e.DecBudget).HasPrecision(18, 2);
        builder.Property(e => e.TotalBudget).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.PeriodYear, e.AccountId, e.BranchId, e.CostCenterId }).IsUnique().HasDatabaseName("UK_ACC_Budgets_Natural");
        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_ACC_Budgets_AccountId");

        builder.HasOne(e => e.Account).WithMany(a => a.Budgets).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
