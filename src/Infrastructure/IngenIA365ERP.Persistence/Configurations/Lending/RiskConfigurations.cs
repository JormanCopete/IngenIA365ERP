using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

// Feature 013, US9 (T0697; data-model §23.1–§23.6 y §23.10): las siete tablas de riesgo y cierres que trae la migración par
// CarteraRiesgo (T0698). Precisión por los alias de PrecisionDeCartera; índices con nombre; llaves Restrict. Las FK de
// LND_AccountingFactParts hacia el cierre y su reapertura (C66) las declara AccountingFactPartConfiguration.

/// <summary>
/// <c>LND_MonthlyCloses</c>: <c>UK_LND_MonthlyCloses_Period (Kind, PeriodYear, PeriodMonth, Version)</c>, un solo base por cooperativa
/// (<c>UK_LND_MonthlyCloses_Base</c> filtrado) y el índice por fecha de cierre.
/// </summary>
public class MonthlyCloseConfiguration : IEntityTypeConfiguration<MonthlyClose>
{
    public const string Tabla = "LND_MonthlyCloses";

    public void Configure(EntityTypeBuilder<MonthlyClose> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.PeriodYear).IsRequired();
        builder.Property(e => e.PeriodMonth).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.CloseDate).IsRequired();
        builder.Property(e => e.InformationCutoffAt).IsRequired();
        builder.Property(e => e.NationalValuesWatermark).IsRequired();
        builder.Property(e => e.DocumentsUsedJson).IsRequired();
        builder.Property(e => e.ModelBySegmentJson).IsRequired();
        builder.Property(e => e.ObligatoryModelUnavailable).IsRequired();
        builder.Property(e => e.UnvalidatedParameters).IsRequired();
        builder.Property(e => e.UnvalidatedParametersJson);
        builder.Property(e => e.IncompleteHistory).IsRequired();
        builder.Property(e => e.IncompleteHistoryJson);
        builder.Property(e => e.LoanCount).IsRequired();
        builder.Property(e => e.TotalPrincipal).Monto().IsRequired();
        builder.Property(e => e.TotalInterest).Monto().IsRequired();
        builder.Property(e => e.TotalIndividualImpairment).Monto().IsRequired();
        builder.Property(e => e.TotalGeneralImpairment).Monto().IsRequired();
        builder.Property(e => e.TotalsJson).IsRequired();
        builder.Property(e => e.LinesSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.OrderedAt).IsRequired();
        builder.Property(e => e.ClosedAt).IsRequired();

        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingActivation>().WithMany().HasForeignKey(e => e.ActivationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MonthlyClose>().WithMany().HasForeignKey(e => e.PreviousCloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MonthlyClose>().WithMany().HasForeignKey(e => e.SupersedesCloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RiskPolicyVersion>().WithMany().HasForeignKey(e => e.RiskPolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OrderedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Kind, e.PeriodYear, e.PeriodMonth, e.Version }).IsUnique()
            .HasDatabaseName("UK_LND_MonthlyCloses_Period");
        // Un hecho nunca se da de baja (data-model §0.1): el único del base filtra sólo por la clase.
        builder.HasIndex(e => e.Kind).IsUnique()
            .HasDatabaseName("UK_LND_MonthlyCloses_Base").HasFilter("[Kind] = 1");
        builder.HasIndex(e => e.CloseDate).HasDatabaseName("IX_LND_MonthlyCloses_CloseDate");
        builder.HasIndex(e => e.ProcessRunId).HasDatabaseName("IX_LND_MonthlyCloses_ProcessRunId");
        builder.HasIndex(e => e.ActivationId).HasDatabaseName("IX_LND_MonthlyCloses_ActivationId");
        builder.HasIndex(e => e.PreviousCloseId).HasDatabaseName("IX_LND_MonthlyCloses_PreviousCloseId");
        builder.HasIndex(e => e.SupersedesCloseId).HasDatabaseName("IX_LND_MonthlyCloses_SupersedesCloseId");
        builder.HasIndex(e => e.RiskPolicyVersionId).HasDatabaseName("IX_LND_MonthlyCloses_RiskPolicyVersionId");
        builder.HasIndex(e => e.OrderedByUserId).HasDatabaseName("IX_LND_MonthlyCloses_OrderedByUserId");
    }
}

/// <summary>
/// <c>LND_MonthlyCloseLoans</c>: la línea del mensual es única por <c>(ProcessRunId, LoanId)</c> y la del base por <c>(MonthlyCloseId,
/// LoanId)</c>, ambos filtrados; <c>CK_LND_MonthlyCloseLoans_Link</c> exige exactamente uno de los dos enlaces.
/// </summary>
public class MonthlyCloseLoanConfiguration : IEntityTypeConfiguration<MonthlyCloseLoan>
{
    public const string Tabla = "LND_MonthlyCloseLoans";

    public void Configure(EntityTypeBuilder<MonthlyCloseLoan> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_MonthlyCloseLoans_Link",
            "([ProcessRunId] IS NOT NULL AND [MonthlyCloseId] IS NULL) OR ([ProcessRunId] IS NULL AND [MonthlyCloseId] IS NOT NULL)"));
        builder.Ignore(e => e.EnlaceValido);

        builder.Property(e => e.OfficePublicId).IsRequired();
        builder.Property(e => e.ModalityCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SubsegmentCode).HasMaxLength(40);
        builder.Property(e => e.SarcSegmentCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ExpectedLossSegmentCode).HasMaxLength(30);
        builder.Property(e => e.ImpairmentModel).HasConversion<int>().IsRequired();
        builder.Property(e => e.ModelBasis).HasConversion<int>().IsRequired();
        builder.Property(e => e.ModelRating).HasConversion<int?>();
        builder.Property(e => e.ArrearsRating).HasConversion<int>().IsRequired();
        builder.Property(e => e.FinalRating).HasConversion<int>().IsRequired();
        builder.Property(e => e.FinalSubcategory).HasConversion<int>().IsRequired();
        builder.Property(e => e.AccountingRating).HasConversion<int>().IsRequired();
        builder.Property(e => e.RatingCause).HasConversion<int>().IsRequired();
        builder.Property(e => e.PreviousAccountingRating).HasConversion<int?>();
        builder.Property(e => e.CandidatesJson).IsRequired();
        builder.Property(e => e.PrincipalBalance).Monto().IsRequired();
        builder.Property(e => e.CurrentInterestBalance).Monto().IsRequired();
        builder.Property(e => e.DefaultInterestBalance).Monto().IsRequired();
        builder.Property(e => e.SuspendedInterestBalance).Monto().IsRequired();
        builder.Property(e => e.OtherBalance).Monto().IsRequired();
        builder.Property(e => e.BalancesByConceptJson).IsRequired();
        builder.Property(e => e.CollateralAdmissibleValue).Monto().IsRequired();
        builder.Property(e => e.ContributionsDeducted).Monto().IsRequired();
        builder.Property(e => e.ImpairmentBase).Monto().IsRequired();
        builder.Property(e => e.ImpairmentPercentage).Fraccion().IsRequired();
        builder.Property(e => e.PrincipalImpairment).Monto().IsRequired();
        builder.Property(e => e.InterestImpairment).Monto().IsRequired();
        builder.Property(e => e.OtherImpairment).Monto().IsRequired();
        builder.Property(e => e.TotalImpairment).Monto().IsRequired();
        builder.Property(e => e.PreviousTotalImpairment).Monto().IsRequired();
        builder.Property(e => e.ImpairmentDelta).Monto().IsRequired();
        builder.Property(e => e.ProbabilityOfDefault).TasaEquivalente();
        builder.Property(e => e.LossGivenDefault).Fraccion();
        builder.Property(e => e.ExposureAtDefault).Monto();
        builder.Property(e => e.ExplanationJson).IsRequired();

        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MonthlyClose>().WithMany().HasForeignKey(e => e.MonthlyCloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProcessRunId, e.LoanId }).IsUnique()
            .HasDatabaseName("UK_LND_MonthlyCloseLoans_Run_Loan").HasFilter("[ProcessRunId] IS NOT NULL");
        builder.HasIndex(e => new { e.MonthlyCloseId, e.LoanId }).IsUnique()
            .HasDatabaseName("UK_LND_MonthlyCloseLoans_Close_Loan").HasFilter("[MonthlyCloseId] IS NOT NULL");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_MonthlyCloseLoans_LoanId");
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_LND_MonthlyCloseLoans_PersonId");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("IX_LND_MonthlyCloseLoans_BranchId");
        builder.HasIndex(e => e.CreditLineId).HasDatabaseName("IX_LND_MonthlyCloseLoans_CreditLineId");
    }
}

/// <summary><c>LND_MonthlyCloseGeneralImpairments</c>: único por cierre y dimensiones.</summary>
public class MonthlyCloseGeneralImpairmentConfiguration : IEntityTypeConfiguration<MonthlyCloseGeneralImpairment>
{
    public const string Tabla = "LND_MonthlyCloseGeneralImpairments";

    public void Configure(EntityTypeBuilder<MonthlyCloseGeneralImpairment> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ModalityCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SubsegmentCode).HasMaxLength(40);
        builder.Property(e => e.ImpairmentModel).HasConversion<int>().IsRequired();
        builder.Property(e => e.GrossPortfolio).Monto().IsRequired();
        builder.Property(e => e.MinimumPercentage).Fraccion().IsRequired();
        builder.Property(e => e.AdditionalPercentage).Fraccion().IsRequired();
        builder.Property(e => e.MinimumAmount).Monto().IsRequired();
        builder.Property(e => e.AdditionalAmount).Monto().IsRequired();
        builder.Property(e => e.CompensationOfIndividual).Monto().IsRequired();
        builder.Property(e => e.TotalAmount).Monto().IsRequired();
        builder.Property(e => e.PreviousAmount).Monto().IsRequired();
        builder.Property(e => e.Delta).Monto().IsRequired();
        builder.Property(e => e.SourceJson).IsRequired();

        builder.HasOne<MonthlyClose>().WithMany().HasForeignKey(e => e.MonthlyCloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MonthlyCloseId, e.BranchId, e.ModalityCode, e.SubsegmentCode, e.IsEmployee, e.ImpairmentModel }).IsUnique()
            .HasDatabaseName("UK_LND_MonthlyCloseGeneralImpairments_Dimensions");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("IX_LND_MonthlyCloseGeneralImpairments_BranchId");
    }
}

/// <summary><c>LND_MonthlyCloseReopenings</c>: un cierre se reabre una vez.</summary>
public class MonthlyCloseReopeningConfiguration : IEntityTypeConfiguration<MonthlyCloseReopening>
{
    public const string Tabla = "LND_MonthlyCloseReopenings";

    public void Configure(EntityTypeBuilder<MonthlyCloseReopening> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ReopenedAt).IsRequired();
        builder.Property(e => e.AccountingPeriodStatusJson).IsRequired();
        builder.Property(e => e.ReversalPartCount).IsRequired();

        builder.HasOne<MonthlyClose>().WithMany().HasForeignKey(e => e.MonthlyCloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReopenedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MonthlyCloseId).IsUnique().HasDatabaseName("UK_LND_MonthlyCloseReopenings_MonthlyCloseId");
        builder.HasIndex(e => e.ProcessRunId).HasDatabaseName("IX_LND_MonthlyCloseReopenings_ProcessRunId");
        builder.HasIndex(e => e.ReopenedByUserId).HasDatabaseName("IX_LND_MonthlyCloseReopenings_ReopenedByUserId");
    }
}

/// <summary>
/// <c>LND_LoanArrearsHistory</c>: índice por crédito y mes, y único <c>(ProcessRunId, LoanId)</c> filtrado (una fila por crédito en cada
/// corrida del cierre). <c>ArrearsHistoryLoadId</c> no lleva FK hasta K6a.
/// </summary>
public class LoanArrearsHistoryEntryConfiguration : IEntityTypeConfiguration<LoanArrearsHistoryEntry>
{
    public const string Tabla = "LND_LoanArrearsHistory";

    public void Configure(EntityTypeBuilder<LoanArrearsHistoryEntry> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.PeriodYear).IsRequired();
        builder.Property(e => e.PeriodMonth).IsRequired();
        builder.Property(e => e.MaxDaysPastDue).IsRequired();
        builder.Property(e => e.Origin).HasConversion<int>().IsRequired();
        builder.Property(e => e.PredecessorLoanNumber).HasMaxLength(30);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.PeriodYear, e.PeriodMonth }).HasDatabaseName("IX_LND_LoanArrearsHistory_Loan_Period");
        builder.HasIndex(e => new { e.ProcessRunId, e.LoanId }).IsUnique()
            .HasDatabaseName("UK_LND_LoanArrearsHistory_Run_Loan").HasFilter("[ProcessRunId] IS NOT NULL");
        builder.HasIndex(e => e.MigrationLoadId).HasDatabaseName("IX_LND_LoanArrearsHistory_MigrationLoadId");
    }
}

/// <summary><c>LND_AnalysisRatings</c>: una vigente por crédito (único filtrado a las no levantadas).</summary>
public class AnalysisRatingConfiguration : IEntityTypeConfiguration<AnalysisRating>
{
    public const string Tabla = "LND_AnalysisRatings";

    public void Configure(EntityTypeBuilder<AnalysisRating> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.Ignore(e => e.Vigente);

        builder.Property(e => e.Rating).HasConversion<int>().IsRequired();
        builder.Property(e => e.Subcategory).HasConversion<int>().IsRequired();
        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.SetAt).IsRequired();
        builder.Property(e => e.LiftReason).HasMaxLength(500);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SetByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LiftedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.LoanId).IsUnique()
            .HasDatabaseName("UK_LND_AnalysisRatings_Loan_Active").HasFilter("[LiftedAt] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => e.SetByUserId).HasDatabaseName("IX_LND_AnalysisRatings_SetByUserId");
        builder.HasIndex(e => e.LiftedByUserId).HasDatabaseName("IX_LND_AnalysisRatings_LiftedByUserId");
    }
}

/// <summary><c>LND_RiskPolicyVersions</c>: único por número de versión e índice de vigencia <c>(ValidFrom, VersionNumber)</c>.</summary>
public class RiskPolicyVersionConfiguration : IEntityTypeConfiguration<RiskPolicyVersion>
{
    public const string Tabla = "LND_RiskPolicyVersions";

    public void Configure(EntityTypeBuilder<RiskPolicyVersion> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.Ignore(e => e.Status);

        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Overrides).IsRequired();
        builder.Property(e => e.OverridesSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.BaseDocumentPublicId).IsRequired();
        builder.Property(e => e.BaseDocumentSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ValidatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.SessionItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.VersionNumber).IsUnique().HasDatabaseName("UK_LND_RiskPolicyVersions_VersionNumber");
        builder.HasIndex(e => new { e.ValidFrom, e.VersionNumber }).HasDatabaseName("IX_LND_RiskPolicyVersions_ValidFrom_VersionNumber");
        builder.HasIndex(e => e.RegisteredByUserId).HasDatabaseName("IX_LND_RiskPolicyVersions_RegisteredByUserId");
        builder.HasIndex(e => e.ValidatedByUserId).HasDatabaseName("IX_LND_RiskPolicyVersions_ValidatedByUserId");
        builder.HasIndex(e => e.SessionItemId).HasDatabaseName("IX_LND_RiskPolicyVersions_SessionItemId");
    }
}
