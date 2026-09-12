using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("COR_Companies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Companies_PublicId");

        // Basic info
        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        // Único cuando existe: los registros migrados del SOLIDO pueden venir sin código.
        builder.HasIndex(e => e.LegacyCode).IsUnique().HasFilter("[LegacyCode] IS NOT NULL");
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(60);
        builder.Property(e => e.TaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.TaxIdCheckDigit).HasMaxLength(2);
        builder.Property(e => e.Address).HasMaxLength(80);
        builder.Property(e => e.Phone).HasMaxLength(40);
        builder.Property(e => e.City).HasMaxLength(40);
        builder.Property(e => e.Department).HasMaxLength(40);
        builder.Property(e => e.Activity).HasMaxLength(10);
        builder.Property(e => e.PersonCode).HasMaxLength(20);

        // DIAN
        builder.Property(e => e.DianCode).HasMaxLength(6);
        builder.Property(e => e.DianCodeDescription).HasMaxLength(40);
        builder.Property(e => e.DianResolutionNumber).HasMaxLength(40);
        builder.Property(e => e.DianInvoiceStart).HasDefaultValue(0);
        builder.Property(e => e.DianInvoiceEnd).HasDefaultValue(0);

        // Wages
        builder.Property(e => e.LegalMinimumWage).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.CompanyMinimumWage).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.MinimumWage).HasPrecision(17, 2).HasDefaultValue(0m);

        // Portfolio config
        builder.Property(e => e.ChargesDefaultInterest).HasDefaultValue(false);
        builder.Property(e => e.LatePaymentControl).HasDefaultValue(false);
        builder.Property(e => e.PaymentControl).HasDefaultValue(false);
        builder.Property(e => e.CollectionPeriod).HasMaxLength(10);
        builder.Property(e => e.InitialDays).HasDefaultValue((short)0);
        builder.Property(e => e.FinalDays).HasDefaultValue((short)0);
        builder.Property(e => e.DefaultRate).HasPrecision(7, 4);
        builder.Property(e => e.GraceDays).HasDefaultValue((short)0);
        builder.Property(e => e.UsuryRate).HasPrecision(7, 4);
        builder.Property(e => e.EffectiveRate).HasDefaultValue(false);
        builder.Property(e => e.LiquidationBase).HasMaxLength(2);
        builder.Property(e => e.LiquidationType).HasMaxLength(2);
        builder.Property(e => e.DiscountClass).HasMaxLength(2);
        builder.Property(e => e.LiquidationClass).HasMaxLength(2);
        builder.Property(e => e.QuotaType).HasMaxLength(2);
        builder.Property(e => e.Quota).HasPrecision(5, 2).HasDefaultValue(0m);
        builder.Property(e => e.ReportClass).HasMaxLength(2);
        builder.Property(e => e.FileName).HasMaxLength(40);
        builder.Property(e => e.CreditSequenceNum).HasDefaultValue(0L);
        builder.Property(e => e.CreditSequenceCtrl).HasPrecision(15, 0).HasDefaultValue(0m);
        builder.Property(e => e.NumCodeudores).HasDefaultValue(0);

        // Due date ranges
        builder.Property(e => e.DueRangeStart01).HasDefaultValue((short)0);
        builder.Property(e => e.DueRangeEnd01).HasDefaultValue((short)0);
        builder.Property(e => e.DueRangeStart02).HasDefaultValue((short)0);
        builder.Property(e => e.DueRangeEnd02).HasDefaultValue((short)0);
        builder.Property(e => e.DueRangeStart03).HasDefaultValue((short)0);
        builder.Property(e => e.DueRangeEnd03).HasDefaultValue((short)0);
        builder.Property(e => e.DueRangeStart04).HasDefaultValue((short)0);
        builder.Property(e => e.DueRangeEnd04).HasDefaultValue((short)0);
        builder.Property(e => e.DueRangeStart05).HasDefaultValue((short)0);
        builder.Property(e => e.DueRangeEnd05).HasDefaultValue((short)0);

        // Concept codes (current)
        builder.Property(e => e.ConceptCapital).HasMaxLength(10);
        builder.Property(e => e.ConceptInterest).HasMaxLength(10);
        builder.Property(e => e.ConceptAdmin).HasMaxLength(10);
        builder.Property(e => e.ConceptInsurance).HasMaxLength(10);
        builder.Property(e => e.ConceptContributions).HasMaxLength(10);
        builder.Property(e => e.ConceptSavings).HasMaxLength(10);
        builder.Property(e => e.ConceptAffiliation).HasMaxLength(10);
        builder.Property(e => e.ConceptExtra).HasMaxLength(10);
        builder.Property(e => e.ConceptImmovable).HasMaxLength(10);
        builder.Property(e => e.ConceptService).HasMaxLength(10);
        builder.Property(e => e.ConceptOther1).HasMaxLength(10);
        builder.Property(e => e.ConceptOther2).HasMaxLength(10);
        builder.Property(e => e.ConceptRevaluation).HasMaxLength(10);
        builder.Property(e => e.ConceptContribDisp).HasMaxLength(10);
        builder.Property(e => e.Concept4Mil).HasMaxLength(10);
        builder.Property(e => e.ConceptWithholdingLate).HasMaxLength(10);
        builder.Property(e => e.ConceptCdtInterest).HasMaxLength(10);
        builder.Property(e => e.ConceptWithholding).HasMaxLength(10);
        builder.Property(e => e.ConceptCdt).HasMaxLength(10);
        builder.Property(e => e.ConceptSurplus).HasMaxLength(10);

        // Concept codes (late)
        builder.Property(e => e.ConceptCapitalLate).HasMaxLength(10);
        builder.Property(e => e.ConceptInterestLate).HasMaxLength(10);
        builder.Property(e => e.ConceptAdminLate).HasMaxLength(10);
        builder.Property(e => e.ConceptInsuranceLate).HasMaxLength(10);
        builder.Property(e => e.ConceptContribLate).HasMaxLength(10);
        builder.Property(e => e.ConceptSavingsLate).HasMaxLength(10);
        builder.Property(e => e.ConceptAffiliationLate).HasMaxLength(10);

        // Priority order
        builder.Property(e => e.PriorityCapital).HasMaxLength(4);
        builder.Property(e => e.PriorityInterest).HasMaxLength(4);
        builder.Property(e => e.PriorityServices).HasMaxLength(4);
        builder.Property(e => e.PriorityDefault).HasMaxLength(4);
        builder.Property(e => e.PriorityAdmin).HasMaxLength(4);
        builder.Property(e => e.PriorityInsurance).HasMaxLength(4);
        builder.Property(e => e.PriorityContributions).HasMaxLength(4);
        builder.Property(e => e.PrioritySavings).HasMaxLength(4);
        builder.Property(e => e.PriorityAffiliation).HasMaxLength(4);

        // AML
        builder.Property(e => e.DailyAmlLimit).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.MonthlyAmlLimit).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.AmlSequence).HasDefaultValue(0L);
        builder.Property(e => e.CausesLegalCollection).HasDefaultValue(false);

        // Accounting accounts
        builder.Property(e => e.AccountingAccount1).HasMaxLength(20);
        builder.Property(e => e.AccountingAccount2).HasMaxLength(20);
        builder.Property(e => e.AccountingAccount3).HasMaxLength(20);
        builder.Property(e => e.AccountingAccount4).HasMaxLength(20);

        // Adjustment accounts
        builder.Property(e => e.AdjInterest).HasMaxLength(20);
        builder.Property(e => e.AdjOrderAccounts).HasMaxLength(20);
        builder.Property(e => e.AdjPortfolioProvision).HasMaxLength(20);
        builder.Property(e => e.AdjInterestProvision).HasMaxLength(20);
        builder.Property(e => e.AdjProvisionPayroll).HasMaxLength(20);
        builder.Property(e => e.AdjProvisionCash).HasMaxLength(20);
        builder.Property(e => e.AdjCashContrib).HasMaxLength(10);
        builder.Property(e => e.AdjCashCapital).HasMaxLength(10);
        builder.Property(e => e.AdjCashInterest).HasMaxLength(10);
        builder.Property(e => e.AdjCashDefaultInt).HasMaxLength(10);
        builder.Property(e => e.AdjCashInsurance).HasMaxLength(10);
        builder.Property(e => e.AdjCashService).HasMaxLength(10);
        builder.Property(e => e.AdjCashSavings).HasMaxLength(10);
        builder.Property(e => e.AdjCashExtra).HasMaxLength(10);
        builder.Property(e => e.AdjCashAdmin).HasMaxLength(10);

        // Signatures
        builder.Property(e => e.RepresentativeName).HasMaxLength(80);
        builder.Property(e => e.AuditorName).HasMaxLength(80);
        builder.Property(e => e.AuditorLicense).HasMaxLength(30);
        builder.Property(e => e.AccountantName).HasMaxLength(80);
        builder.Property(e => e.AccountantLicense).HasMaxLength(30);
        builder.Property(e => e.CollectionManager).HasMaxLength(80);
        builder.Property(e => e.OtherSignerName).HasMaxLength(80);
        builder.Property(e => e.OtherSignerPosition).HasMaxLength(80);

        // Auto-create flags
        builder.Property(e => e.AutoCreateAccount).HasDefaultValue(false);
        builder.Property(e => e.AutoCreateTaxId).HasDefaultValue(false);
        builder.Property(e => e.AutoCreateBranch).HasDefaultValue(false);
        builder.Property(e => e.AutoCreateCostCenter).HasDefaultValue(false);

        // Sequence counters
        builder.Property(e => e.DepositSequence).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.CdtSequence).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.SequenceControl).HasDefaultValue((short)0);
        builder.Property(e => e.ControlDepositSeq).HasDefaultValue(false);

        // Advance payment
        builder.Property(e => e.AdvanceConceptType).HasDefaultValue((short)0);
        builder.Property(e => e.AdvanceVoucherCode).HasMaxLength(10);
        builder.Property(e => e.FavorVoucherCode).HasMaxLength(10);
        builder.Property(e => e.DebtRecoveryOption).HasDefaultValue(0);

        // Online query
        builder.Property(e => e.GenerateQueryCharge).HasDefaultValue(false);
        builder.Property(e => e.QueryChargeAmount).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.QueryVoucherCode).HasMaxLength(10);

        // Payroll & application mode
        builder.Property(e => e.PayrollClass).HasMaxLength(2);
        builder.Property(e => e.SignatureModule).HasDefaultValue(false);
        builder.Property(e => e.CalculateBalance).HasDefaultValue(false);
        builder.Property(e => e.PayrollApplicationMode).HasMaxLength(2);
        builder.Property(e => e.CashApplicationMode).HasMaxLength(2);
        builder.Property(e => e.ChargesCodebtor).HasDefaultValue(false);

        // Override flags
        builder.Property(e => e.OverrideExtra).HasDefaultValue(false);
        builder.Property(e => e.OverrideQuota).HasDefaultValue(false);
        builder.Property(e => e.OverrideTerm).HasDefaultValue(false);
        builder.Property(e => e.OverrideRate).HasDefaultValue(false);
        builder.Property(e => e.UnifiedNetwork).HasDefaultValue(false);
        builder.Property(e => e.RequiresStudy).HasDefaultValue(false);
        builder.Property(e => e.DateRestrictionRC).HasDefaultValue(false);
        builder.Property(e => e.AllowCreditQuotaMod).HasDefaultValue(false);
        builder.Property(e => e.BankReconciliation).HasDefaultValue(false);
        builder.Property(e => e.CalcContribProvision).HasDefaultValue(false);
        builder.Property(e => e.ApplyDefaultSuspension).HasDefaultValue(false);
        builder.Property(e => e.DefaultSuspensionDays).HasDefaultValue(0);
        builder.Property(e => e.DefaultForWithdrawn).HasDefaultValue(false);
        builder.Property(e => e.InvoiceSequenceCtrl).HasDefaultValue(false);
        builder.Property(e => e.AccrualParam).HasDefaultValue(false);
        builder.Property(e => e.DisableCdtRate).HasDefaultValue(false);

        // Email
        builder.Property(e => e.SmtpServer).HasMaxLength(100);
        builder.Property(e => e.SmtpSenderEmail).HasMaxLength(120);
        builder.Property(e => e.SmtpPassword).HasMaxLength(100);
        builder.Property(e => e.SmtpEnableSsl).HasDefaultValue(false);
        builder.Property(e => e.WebServiceUrl).HasMaxLength(200);

        // Promissory note
        builder.Property(e => e.PromissoryNumber).HasPrecision(10, 0).HasDefaultValue(0m);
        builder.Property(e => e.PromissoryFormat).HasMaxLength(2);
        builder.Property(e => e.PromissoryNotes).HasDefaultValue(false);

        // Legal entity
        builder.Property(e => e.LegalEntityNumber).HasMaxLength(20);

        // Web sync
        builder.Property(e => e.DownloadWeb).HasDefaultValue(false);

        // Withholding aux
        builder.Property(e => e.WithholdingAux).HasDefaultValue(false);
        builder.Property(e => e.WithholdingAuxAmount).HasPrecision(17, 2);
        builder.Property(e => e.WithholdingAuxPct).HasPrecision(17, 2);
        builder.Property(e => e.WithholdingAuxAccount).HasMaxLength(20);

        // Data package / misc
        builder.Property(e => e.DataPackageSize).HasDefaultValue(0);
        builder.Property(e => e.AgreementType).HasDefaultValue(0);
        builder.Property(e => e.FeecCode).HasMaxLength(10);
        builder.Property(e => e.LicenseType).HasMaxLength(4);
        builder.Property(e => e.NoticeDays).HasDefaultValue(0);
        builder.Property(e => e.ReportType).HasMaxLength(4);

        // Legacy audit
        builder.Property(e => e.LegacyUser).HasMaxLength(20);
        builder.Property(e => e.LegacyUserName).HasMaxLength(80);

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
