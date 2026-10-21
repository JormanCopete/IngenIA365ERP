using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

// Feature 014, US6 (T169; data-model §2.4, §3.3, §3.4; research E134): las condiciones selladas del CDAT, su plan de intereses, las reglas de tasa
// negociada y sus solicitudes. Migración CaptacionesAportes (regenerable hasta el congelamiento, research E35). (nuevo)

/// <summary>
/// <c>LND_MemberAccountTermsVersions</c> (data-model §3.3): una versión por cuenta y número (<c>UK_LND_MemberAccountTermsVersions_Account_Version</c>), una por
/// operación; la cadena de prórrogas por <c>PreviousVersionId</c>. Las tasas son <c>TasaAnual</c>; la diaria sellada, <c>TasaEquivalente</c>.
/// </summary>
public class MemberAccountTermsVersionConfiguration : IEntityTypeConfiguration<MemberAccountTermsVersion>
{
    public const string Tabla = "LND_MemberAccountTermsVersions";

    public void Configure(EntityTypeBuilder<MemberAccountTermsVersion> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_MemberAccountTermsVersions_Principal", "[PrincipalAmount] > 0");
            t.HasCheckConstraint("CK_LND_MemberAccountTermsVersions_Version", "[VersionNumber] >= 1");
        });

        builder.Property(e => e.Cause).HasConversion<int>().IsRequired();
        builder.Property(e => e.RulesVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InputsSha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.PrincipalAmount).Monto().IsRequired();
        builder.Property(e => e.IssueTermBand).HasConversion<int?>();
        builder.Property(e => e.TableAnnualRate).TasaAnual().IsRequired();
        builder.Property(e => e.NegotiatedSpread).TasaAnual();
        builder.Property(e => e.AppliedAnnualRate).TasaAnual().IsRequired();
        builder.Property(e => e.NegotiationReason).HasMaxLength(MemberAccountTermsVersion.LargoDelMotivo);
        builder.Property(e => e.PeriodRate).TasaEquivalente();
        builder.Property(e => e.InterestPeriodicity).HasConversion<int?>();
        builder.Property(e => e.InterestTiming).HasConversion<int?>();
        builder.Property(e => e.DayCount).HasConversion<int>().IsRequired();
        builder.Property(e => e.RateEquivalence).HasConversion<int>().IsRequired();
        builder.Property(e => e.RoundingUnit).Monto().IsRequired();
        builder.Property(e => e.RoundingMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.RoundingMoment).HasConversion<int>().IsRequired();
        builder.Property(e => e.InterestDestination).HasConversion<int?>();
        builder.Property(e => e.DestinationBankAccountNumber).HasMaxLength(25);
        builder.Property(e => e.MaturityInstruction).HasConversion<int?>();
        builder.Property(e => e.RolloverRateRule).HasConversion<int?>();
        builder.Property(e => e.AfterMaturityTreatment).HasConversion<int?>();
        builder.Property(e => e.EarlyRedemptionPenaltyMode).HasConversion<int?>();
        builder.Property(e => e.PenaltyAnnualRate).TasaAnual();
        builder.Property(e => e.PenaltyDiscountFraction).Fraccion();
        builder.Property(e => e.InstallmentAmount).Monto();
        builder.Property(e => e.InstallmentPeriodicity).HasConversion<int?>();
        builder.Property(e => e.BonusAnnualRate).TasaAnual();
        builder.Property(e => e.BreachAnnualRate).TasaAnual();
        builder.Property(e => e.BreachEffect).HasConversion<int?>();
        builder.Property(e => e.BonusRecognition).HasConversion<int?>();
        builder.Property(e => e.ProjectedFinalAmount).Monto();

        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTermsVersions_Operation");
        builder.HasOne<DepositLineVersion>().WithMany().HasForeignKey(e => e.DepositLineVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTermsVersions_LineVersion");
        builder.HasOne<NegotiatedRateRule>().WithMany().HasForeignKey(e => e.NegotiatedRateRuleId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTermsVersions_Rule");
        builder.HasOne<NegotiatedRateRequest>().WithMany().HasForeignKey(e => e.NegotiatedRateRequestId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTermsVersions_Request");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.NegotiationApprovedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTermsVersions_Approver");
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.DestinationAccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTermsVersions_DestinationAccount");
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.DestinationPersonId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTermsVersions_DestinationPerson");
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.DestinationBankId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTermsVersions_DestinationBank");
        builder.HasOne<MemberAccountTermsVersion>().WithMany().HasForeignKey(e => e.PreviousVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_MemberAccountTermsVersions_Previous");

        builder.HasIndex(e => new { e.MemberAccountId, e.VersionNumber }).IsUnique().HasDatabaseName("UK_LND_MemberAccountTermsVersions_Account_Version");
        builder.HasIndex(e => e.OperationId).IsUnique().HasDatabaseName("UK_LND_MemberAccountTermsVersions_Operation");
        builder.HasIndex(e => e.MaturityDate).HasDatabaseName("IX_LND_MemberAccountTermsVersions_MaturityDate").HasFilter("[MaturityDate] IS NOT NULL");
        builder.HasIndex(e => e.PreviousVersionId).HasDatabaseName("IX_LND_MemberAccountTermsVersions_Previous").HasFilter("[PreviousVersionId] IS NOT NULL");
    }
}

/// <summary><c>LND_TermDepositSchedules</c> (data-model §3.4): un período por versión y número.</summary>
public class TermDepositScheduleItemConfiguration : IEntityTypeConfiguration<TermDepositScheduleItem>
{
    public const string Tabla = "LND_TermDepositSchedules";

    public void Configure(EntityTypeBuilder<TermDepositScheduleItem> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_TermDepositSchedules_Period", "[PeriodEnd] > [PeriodStart]"));
        builder.Property(e => e.PeriodRate).TasaEquivalente().IsRequired();
        builder.Property(e => e.Interest).Monto().IsRequired();
        builder.HasOne(e => e.TermsVersion).WithMany().HasForeignKey(e => e.TermsVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_TermDepositSchedules_TermsVersion");
        builder.HasIndex(e => new { e.TermsVersionId, e.PeriodNumber }).IsUnique().HasDatabaseName("UK_LND_TermDepositSchedules_Terms_Period");
        builder.HasIndex(e => e.PaymentDate).HasDatabaseName("IX_LND_TermDepositSchedules_PaymentDate");
    }
}

/// <summary><c>LND_NegotiatedRateRules</c> (data-model §2.4): una versión por línea, código y número.</summary>
public class NegotiatedRateRuleConfiguration : IEntityTypeConfiguration<NegotiatedRateRule>
{
    public const string Tabla = "LND_NegotiatedRateRules";

    public void Configure(EntityTypeBuilder<NegotiatedRateRule> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_NegotiatedRateRules_MaxSpread", "[MaxSpread] > 0"));
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.MinAmount).Monto();
        builder.Property(e => e.MaxAmount).Monto();
        builder.Property(e => e.MinBaseRate).TasaAnual();
        builder.Property(e => e.MaxBaseRate).TasaAnual();
        builder.Property(e => e.MinHolderTotalBalance).Monto();
        builder.Property(e => e.MaxHolderTotalBalance).Monto();
        builder.Property(e => e.Channel).HasMaxLength(NegotiatedRateRule.LargoDelCanal);
        builder.Property(e => e.MaxSpread).TasaAnual().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(NegotiatedRateRule.LargoDelMotivo).IsRequired();

        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CreditLineId, e.Code, e.VersionNumber }).IsUnique().HasDatabaseName("UK_LND_NegotiatedRateRules_Line_Code_Version");
        builder.HasIndex(e => new { e.CreditLineId, e.ValidFrom }).HasDatabaseName("IX_LND_NegotiatedRateRules_Line_ValidFrom");
    }
}

/// <summary><c>LND_NegotiatedRateRequests</c> (research E134): la solicitud que espera decisión, una viva por cuenta.</summary>
public class NegotiatedRateRequestConfiguration : IEntityTypeConfiguration<NegotiatedRateRequest>
{
    public const string Tabla = "LND_NegotiatedRateRequests";

    public void Configure(EntityTypeBuilder<NegotiatedRateRequest> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_NegotiatedRateRequests_Spread", "[RequestedSpread] > 0"));
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.TableAnnualRate).TasaAnual().IsRequired();
        builder.Property(e => e.RequestedSpread).TasaAnual().IsRequired();
        builder.Property(e => e.MaxSpread).TasaAnual().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Channel).HasMaxLength(NegotiatedRateRule.LargoDelCanal);
        builder.Property(e => e.Reason).HasMaxLength(NegotiatedRateRequest.LargoDelMotivo).IsRequired();
        builder.Property(e => e.DecisionReason).HasMaxLength(NegotiatedRateRequest.LargoDelMotivo);
        builder.Property(e => e.RequestedAt).IsRequired();

        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NegotiatedRateRule>().WithMany().HasForeignKey(e => e.NegotiatedRateRuleId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_NegotiatedRateRequests_Rule");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MemberAccountId).IsUnique().HasDatabaseName("UK_LND_NegotiatedRateRequests_LivePerAccount")
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Status, e.RequestedAt }).HasDatabaseName("IX_LND_NegotiatedRateRequests_Status");
    }
}
