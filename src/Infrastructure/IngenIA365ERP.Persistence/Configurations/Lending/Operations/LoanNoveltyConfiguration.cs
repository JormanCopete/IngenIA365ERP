using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

// Feature 013, K3 (T0875; data-model §25.1–§25.3): las novedades, sus créditos y los consentimientos de codeudores y garantes que trae la
// migración par CarteraNovedades (T0877). Índices con nombre; llaves Restrict; los JSON van sin largo (nvarchar(max)).

/// <summary>
/// <c>LND_LoanNovelties</c>: número único; la operación, las versiones y el crédito enlazado que deja al aplicarse se enlazan por navegación
/// para guardarse en el mismo <c>SaveChanges</c>.
/// </summary>
public class LoanNoveltyConfiguration : IEntityTypeConfiguration<LoanNovelty>
{
    public const string Tabla = "LND_LoanNovelties";

    public void Configure(EntityTypeBuilder<LoanNovelty> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Mechanism).HasConversion<int>().IsRequired();
        builder.Property(e => e.Cause).HasConversion<int>().IsRequired();
        builder.Property(e => e.CauseDeterminationJson).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestReason).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.RequestedAt).IsRequired();
        builder.Property(e => e.ProposedTermsJson).IsRequired();
        builder.Property(e => e.SimulationJson).IsRequired();
        builder.Property(e => e.SimulationSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.RatingAtRequest).HasConversion<int>().IsRequired();
        builder.Property(e => e.ConditionsJson);
        builder.Property(e => e.CancelReason).HasMaxLength(500);
        builder.Ignore(e => e.Editable);
        builder.Ignore(e => e.Decidida);
        builder.Ignore(e => e.CreaCreditoEnlazado);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InsolvencyProceeding>().WithMany().HasForeignKey(e => e.InsolvencyProceedingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.FinalDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PromissoryNote>().WithMany().HasForeignKey(e => e.PromissoryNoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AppliedOperation).WithMany().HasForeignKey(e => e.AppliedOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.NewTermsVersion).WithMany().HasForeignKey(e => e.NewTermsVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.NewScheduleVersion).WithMany().HasForeignKey(e => e.NewScheduleVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ResultingLoan).WithMany().HasForeignKey(e => e.ResultingLoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AppliedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_LoanNovelties_Number");
        builder.HasIndex(e => new { e.LoanId, e.Status }).HasDatabaseName("IX_LND_LoanNovelties_Loan_Status");
        builder.HasIndex(e => e.InsolvencyProceedingId).HasDatabaseName("IX_LND_LoanNovelties_InsolvencyProceedingId");
        builder.HasIndex(e => e.RequestedByUserId).HasDatabaseName("IX_LND_LoanNovelties_RequestedByUserId");
        builder.HasIndex(e => e.FinalDecisionId).HasDatabaseName("IX_LND_LoanNovelties_FinalDecisionId");
        builder.HasIndex(e => e.PromissoryNoteId).HasDatabaseName("IX_LND_LoanNovelties_PromissoryNoteId");
        builder.HasIndex(e => e.AppliedOperationId).HasDatabaseName("IX_LND_LoanNovelties_AppliedOperationId");
        builder.HasIndex(e => e.NewTermsVersionId).HasDatabaseName("IX_LND_LoanNovelties_NewTermsVersionId");
        builder.HasIndex(e => e.NewScheduleVersionId).HasDatabaseName("IX_LND_LoanNovelties_NewScheduleVersionId");
        builder.HasIndex(e => e.ResultingLoanId).HasDatabaseName("IX_LND_LoanNovelties_ResultingLoanId");
        builder.HasIndex(e => e.AppliedByUserId).HasDatabaseName("IX_LND_LoanNovelties_AppliedByUserId");
    }
}

/// <summary><c>LND_LoanNoveltyLoans</c>: <c>UK (LoanNoveltyId, LoanId)</c>; el crédito enlazado se enlaza por navegación.</summary>
public class LoanNoveltyLoanConfiguration : IEntityTypeConfiguration<LoanNoveltyLoan>
{
    public const string Tabla = "LND_LoanNoveltyLoans";

    public void Configure(EntityTypeBuilder<LoanNoveltyLoan> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Role).HasConversion<int>().IsRequired();

        builder.HasOne<LoanNovelty>().WithMany().HasForeignKey(e => e.LoanNoveltyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Loan).WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanNoveltyId, e.LoanId }).IsUnique().HasDatabaseName("UK_LND_LoanNoveltyLoans_Novelty_Loan");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_LoanNoveltyLoans_LoanId");
    }
}

/// <summary><c>LND_LoanNoveltyConsents</c>: <c>UK (LoanNoveltyId, PersonId, CollateralId)</c>, partido en dos filtrados por la garantía nula.</summary>
public class LoanNoveltyConsentConfiguration : IEntityTypeConfiguration<LoanNoveltyConsent>
{
    public const string Tabla = "LND_LoanNoveltyConsents";

    public void Configure(EntityTypeBuilder<LoanNoveltyConsent> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.PartyRole).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.RefusalResolution).HasConversion<int?>();
        builder.Property(e => e.ResolutionReason).HasMaxLength(500);
        builder.Property(e => e.CoverageAfterJson);
        builder.Ignore(e => e.Resuelto);

        builder.HasOne<LoanNovelty>().WithMany().HasForeignKey(e => e.LoanNoveltyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Collateral>().WithMany().HasForeignKey(e => e.CollateralId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.ExceptionDecisionId).OnDelete(DeleteBehavior.Restrict);

        // Dos únicos filtrados en lugar de uno sobre (novedad, persona, garantía): con la garantía nula, PostgreSQL no trata
        // los nulos como iguales y EF filtra el único de SQL Server a garantía no nula, así que ninguno impedía repetir a
        // la misma persona sin garantía.
        builder.HasIndex(e => new { e.LoanNoveltyId, e.PersonId, e.CollateralId }, "UK_LND_LoanNoveltyConsents_Novelty_Person_Collateral")
            .IsUnique().HasFilter("[CollateralId] IS NOT NULL");
        builder.HasIndex(e => new { e.LoanNoveltyId, e.PersonId }, "UK_LND_LoanNoveltyConsents_Novelty_Person_NoCollateral")
            .IsUnique().HasFilter("[CollateralId] IS NULL");
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_LND_LoanNoveltyConsents_PersonId");
        builder.HasIndex(e => e.CollateralId).HasDatabaseName("IX_LND_LoanNoveltyConsents_CollateralId");
        builder.HasIndex(e => e.RecordedByUserId).HasDatabaseName("IX_LND_LoanNoveltyConsents_RecordedByUserId");
        builder.HasIndex(e => e.ExceptionDecisionId).HasDatabaseName("IX_LND_LoanNoveltyConsents_ExceptionDecisionId");
    }
}
