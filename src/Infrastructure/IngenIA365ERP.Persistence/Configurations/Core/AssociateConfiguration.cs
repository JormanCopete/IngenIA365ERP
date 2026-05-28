using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class AssociateConfiguration : IEntityTypeConfiguration<Associate>
{
    public void Configure(EntityTypeBuilder<Associate> builder)
    {
        builder.ToTable("COR_Associates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Associates_PublicId");

        builder.Property(e => e.PersonId).IsRequired();
        // Unique constraint: one associate per person
        builder.HasIndex(e => e.PersonId).IsUnique().HasDatabaseName("UK_COR_Associates_PersonId");

        // === Affiliation ===
        builder.Property(e => e.ContributionRate).HasPrecision(10, 5).HasDefaultValue(0m);
        builder.Property(e => e.CutoffDay).HasDefaultValue((short)0);
        builder.Property(e => e.Status).HasMaxLength(2);
        builder.Property(e => e.CategoryRating).HasMaxLength(2);
        builder.Property(e => e.DeductionPeriod).HasMaxLength(2);
        builder.Property(e => e.DeductionType).HasMaxLength(2);
        builder.Property(e => e.Rank).HasMaxLength(4);
        builder.Property(e => e.ReferredBy).HasMaxLength(20);
        builder.Property(e => e.IsInLegalCollection).HasDefaultValue(false);
        builder.Property(e => e.ContractNumber).HasDefaultValue((short)0);
        builder.Property(e => e.ZoneCode).HasMaxLength(20);
        builder.Property(e => e.ContributionPledged).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.AssociateClass).HasMaxLength(4);
        builder.Property(e => e.PaymentType).HasMaxLength(4);
        builder.Property(e => e.SectorCode).HasMaxLength(10);
        builder.Property(e => e.MailingOption).HasDefaultValue((short)0);
        builder.Property(e => e.ManualRating).HasMaxLength(2);
        builder.Property(e => e.PreviousClass).HasMaxLength(2);

        // === External employment ===
        builder.Property(e => e.ExternalEmployerName).HasMaxLength(80);
        builder.Property(e => e.ExternalSalary).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.ExternalSalaryType).HasMaxLength(2);
        builder.Property(e => e.ExternalSeverance).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.ExternalSeveranceFund).HasMaxLength(100);
        builder.Property(e => e.ExternalOtherIncomeDescription).HasMaxLength(120);

        // === Deposit banking ===
        builder.Property(e => e.DepositBankAccountNumber).HasMaxLength(30);
        builder.Property(e => e.DepositBankAccountType).HasMaxLength(2);

        // === Scoring / Risk ===
        builder.Property(e => e.AuthCentralRisk).HasDefaultValue(false);
        builder.Property(e => e.PosCardClass).HasMaxLength(2);
        builder.Property(e => e.PosCardLimit).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.InsuranceRiskRate).HasPrecision(9, 3).HasDefaultValue(0m);
        builder.Property(e => e.AssociateZoneTypeId).HasDefaultValue(0);
        builder.Property(e => e.AssociateZoneId).HasDefaultValue(0);
        builder.Property(e => e.IsSiplaExempt).HasDefaultValue(false);
        builder.Property(e => e.SiplaUser).HasMaxLength(20);
        builder.Property(e => e.SinglePromissoryNote).HasDefaultValue(false);
        builder.Property(e => e.PledgesContributions).HasDefaultValue(false);
        builder.Property(e => e.InManagement).HasDefaultValue(false);
        builder.Property(e => e.CreditLimit).HasPrecision(17, 2).HasDefaultValue(0m);

        // === Accounting centers ===
        builder.Property(e => e.CpAdmin).HasDefaultValue(false);
        builder.Property(e => e.CpContributions).HasDefaultValue(false);
        builder.Property(e => e.CpLocal).HasDefaultValue(false);
        builder.Property(e => e.CpCommission).HasDefaultValue(false);
        builder.Property(e => e.ProfitCenter).HasMaxLength(20);
        builder.Property(e => e.CapacityPayPct).HasDefaultValue(false);

        // === Role flags ===
        builder.Property(e => e.IsFromGovernment).HasDefaultValue(false);
        builder.Property(e => e.IsPublicResourceAdmin).HasDefaultValue(false);
        builder.Property(e => e.IsPensioner).HasDefaultValue(false);
        builder.Property(e => e.IsInsubordinate).HasDefaultValue(false);
        builder.Property(e => e.IsOnVacation).HasDefaultValue(false);
        builder.Property(e => e.IsOnUnpaidLeave).HasDefaultValue(false);
        builder.Property(e => e.AssociatePensionType).HasMaxLength(10);
        builder.Property(e => e.AssociateSeveranceType).HasMaxLength(10);

        // === Spouse employment ===
        builder.Property(e => e.SpouseEmployer).HasMaxLength(80);
        builder.Property(e => e.SpouseEmployerAddress).HasMaxLength(80);
        builder.Property(e => e.SpouseSalary).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.SpouseSalaryType).HasMaxLength(2);
        builder.Property(e => e.SpousePosition).HasMaxLength(60);
        builder.Property(e => e.SpouseProfession).HasMaxLength(10);
        builder.Property(e => e.SpouseEducationLevel).HasMaxLength(2);
        builder.Property(e => e.SpouseSeverance).HasPrecision(17, 2);
        builder.Property(e => e.SpouseOtherIncome).HasPrecision(17, 2);
        builder.Property(e => e.SpouseOtherIncomeDesc).HasMaxLength(120);
        builder.Property(e => e.SpouseCompanyCode).HasMaxLength(10);
        builder.Property(e => e.SpouseBranchCode).HasMaxLength(10);
        builder.Property(e => e.SpouseSectionCode).HasMaxLength(10);

        // === FK indexes ===
        builder.HasIndex(e => e.EmployerCompanyId).HasDatabaseName("IX_COR_Associates_EmployerCompanyId");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("IX_COR_Associates_BranchId");
        builder.HasIndex(e => e.CostCenterId).HasDatabaseName("IX_COR_Associates_CostCenterId");
        builder.HasIndex(e => e.SectionId).HasDatabaseName("IX_COR_Associates_SectionId");
        builder.HasIndex(e => e.AdvisorId).HasDatabaseName("IX_COR_Associates_AdvisorId");
        builder.HasIndex(e => e.CommitteeId).HasDatabaseName("IX_COR_Associates_CommitteeId");
        builder.HasIndex(e => e.WithdrawalReasonId).HasDatabaseName("IX_COR_Associates_WithdrawalReasonId");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_COR_Associates_Status");

        // === Relationships ===
        builder.HasOne(e => e.EmployerCompany).WithMany().HasForeignKey(e => e.EmployerCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WithdrawalReason).WithMany().HasForeignKey(e => e.WithdrawalReasonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Advisor).WithMany().HasForeignKey(e => e.AdvisorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Committee).WithMany().HasForeignKey(e => e.CommitteeId).OnDelete(DeleteBehavior.Restrict);

        // === Audit ===
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
