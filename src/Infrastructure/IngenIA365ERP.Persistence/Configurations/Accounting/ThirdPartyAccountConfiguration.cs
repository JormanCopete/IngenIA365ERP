using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class ThirdPartyAccountConfiguration : IEntityTypeConfiguration<ThirdPartyAccount>
{
    public void Configure(EntityTypeBuilder<ThirdPartyAccount> builder)
    {
        builder.ToTable("ACC_ThirdPartyAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_ThirdPartyAccounts_PublicId");

        builder.Property(e => e.InitialBalance).HasPrecision(18, 2);
        builder.Property(e => e.JanDebit).HasPrecision(18, 2);
        builder.Property(e => e.JanCredit).HasPrecision(18, 2);
        builder.Property(e => e.FebDebit).HasPrecision(18, 2);
        builder.Property(e => e.FebCredit).HasPrecision(18, 2);
        builder.Property(e => e.MarDebit).HasPrecision(18, 2);
        builder.Property(e => e.MarCredit).HasPrecision(18, 2);
        builder.Property(e => e.AprDebit).HasPrecision(18, 2);
        builder.Property(e => e.AprCredit).HasPrecision(18, 2);
        builder.Property(e => e.MayDebit).HasPrecision(18, 2);
        builder.Property(e => e.MayCredit).HasPrecision(18, 2);
        builder.Property(e => e.JunDebit).HasPrecision(18, 2);
        builder.Property(e => e.JunCredit).HasPrecision(18, 2);
        builder.Property(e => e.JulDebit).HasPrecision(18, 2);
        builder.Property(e => e.JulCredit).HasPrecision(18, 2);
        builder.Property(e => e.AugDebit).HasPrecision(18, 2);
        builder.Property(e => e.AugCredit).HasPrecision(18, 2);
        builder.Property(e => e.SepDebit).HasPrecision(18, 2);
        builder.Property(e => e.SepCredit).HasPrecision(18, 2);
        builder.Property(e => e.OctDebit).HasPrecision(18, 2);
        builder.Property(e => e.OctCredit).HasPrecision(18, 2);
        builder.Property(e => e.NovDebit).HasPrecision(18, 2);
        builder.Property(e => e.NovCredit).HasPrecision(18, 2);
        builder.Property(e => e.DecDebit).HasPrecision(18, 2);
        builder.Property(e => e.DecCredit).HasPrecision(18, 2);
        builder.Property(e => e.Period13Debit).HasPrecision(18, 2);
        builder.Property(e => e.Period13Credit).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.PeriodYear, e.AccountId, e.PersonId, e.BranchId, e.CostCenterId }).IsUnique().HasDatabaseName("UK_ACC_ThirdPartyAccounts_Natural");
        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_ACC_ThirdPartyAccounts_AccountId");
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_ACC_ThirdPartyAccounts_PersonId");

        builder.HasOne(e => e.Account).WithMany(a => a.ThirdPartyAccounts).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
