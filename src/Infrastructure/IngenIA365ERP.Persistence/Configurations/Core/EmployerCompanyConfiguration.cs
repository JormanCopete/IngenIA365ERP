using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class EmployerCompanyConfiguration : IEntityTypeConfiguration<EmployerCompany>
{
    public void Configure(EntityTypeBuilder<EmployerCompany> builder)
    {
        builder.ToTable("COR_EmployerCompanies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_EmployerCompanies_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(40);
        builder.Property(e => e.TaxId).HasMaxLength(20);
        builder.Property(e => e.PayerName).HasMaxLength(60);
        builder.Property(e => e.Address).HasMaxLength(120);
        builder.Property(e => e.City).HasMaxLength(40);
        builder.Property(e => e.Phone).HasMaxLength(40);
        builder.Property(e => e.Fax).HasMaxLength(40);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.CutoffDay1).HasDefaultValue((short)0);
        builder.Property(e => e.CutoffDay2).HasDefaultValue((short)0);
        builder.Property(e => e.CutoffDay3).HasDefaultValue((short)0);
        builder.Property(e => e.Term).HasDefaultValue((short)0);
        builder.Property(e => e.PayrollConceptCode).HasMaxLength(10);
        builder.Property(e => e.SubmissionFormat).HasMaxLength(30);
        builder.Property(e => e.DiscountPercentage).HasPrecision(10, 3).HasDefaultValue(0m);
        builder.Property(e => e.DiscountType).HasMaxLength(2);
        builder.Property(e => e.IsBlocked).HasDefaultValue(false);
        builder.Property(e => e.GroceryPercentage).HasPrecision(6, 3).HasDefaultValue(0m);
        builder.Property(e => e.ArpRate).HasPrecision(6, 3).HasDefaultValue(0m);
        builder.Property(e => e.AccountType).HasDefaultValue(0);
        builder.Property(e => e.CompanyType).HasDefaultValue(0);
        builder.Property(e => e.FundSourceAccount).HasMaxLength(60);
        builder.Property(e => e.FixedProvisionAmount).HasPrecision(12, 2).HasDefaultValue(0m);
        builder.Property(e => e.MinimumWageAmount).HasPrecision(12, 2).HasDefaultValue(0m);
        builder.Property(e => e.AdminLiquidationType).HasDefaultValue(0);
        builder.Property(e => e.AccountingVoucherId).HasMaxLength(10);
        builder.Property(e => e.OffsettingAccount).HasMaxLength(20);
        builder.Property(e => e.AccountingUpdateType).HasDefaultValue(0);
        builder.Property(e => e.BaseSalary).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityBracket1).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityBracket2).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityBracket3).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityBracket4).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityBracket5).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityRate1).HasPrecision(6, 3).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityRate2).HasPrecision(6, 3).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityRate3).HasPrecision(6, 3).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityRate4).HasPrecision(6, 3).HasDefaultValue(0m);
        builder.Property(e => e.SolidarityRate5).HasPrecision(6, 3).HasDefaultValue(0m);
        builder.Property(e => e.MaxDaysCap).HasDefaultValue(0);
        builder.Property(e => e.DisabilityCxcConcept).HasDefaultValue(0);
        builder.Property(e => e.ProbationDays).HasDefaultValue(0);
        builder.Property(e => e.ConsolidatedVacId2).HasDefaultValue(0);
        builder.Property(e => e.DisabilityFactor).HasPrecision(12, 2).HasDefaultValue(0m);
        builder.Property(e => e.UvtValue).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.IncomePercentage).HasPrecision(17, 4).HasDefaultValue(0m);
        builder.Property(e => e.DeductionPercentage).HasPrecision(17, 4).HasDefaultValue(0m);
        builder.Property(e => e.MaxDeductionPct).HasPrecision(6, 2).HasDefaultValue(0m);
        builder.Property(e => e.IncludeProvision).HasDefaultValue(0);
        builder.Property(e => e.EmitsInvoice).HasDefaultValue(false);
        builder.Property(e => e.ThirdPartyTransfer).HasDefaultValue(0);

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
