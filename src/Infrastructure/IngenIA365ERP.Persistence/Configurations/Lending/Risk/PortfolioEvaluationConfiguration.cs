using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Risk;

// Feature 013, K3 (T0875; data-model §23.7): la evaluación de cartera y sus créditos. Van en CarteraNovedades con el castigo (su
// comportamiento lo construye US14).

/// <summary><c>LND_PortfolioEvaluations</c>: número único; los puntos del comité de riesgos y del consejo.</summary>
public class PortfolioEvaluationConfiguration : IEntityTypeConfiguration<PortfolioEvaluation>
{
    public const string Tabla = "LND_PortfolioEvaluations";

    public void Configure(EntityTypeBuilder<PortfolioEvaluation> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CutoffDate).IsRequired();
        builder.Property(e => e.EffectiveYear).IsRequired();
        builder.Property(e => e.EffectiveMonth).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.CriteriaJson).IsRequired();
        builder.Property(e => e.PreparedAt).IsRequired();
        builder.Property(e => e.LoanCount).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.PreparedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.RecommendationSessionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.ApprovalSessionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AppliedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_PortfolioEvaluations_Number");
        builder.HasIndex(e => new { e.CutoffDate, e.Status }).HasDatabaseName("IX_LND_PortfolioEvaluations_Cutoff_Status");
        builder.HasIndex(e => new { e.EffectiveYear, e.EffectiveMonth }).HasDatabaseName("IX_LND_PortfolioEvaluations_Effective");
        builder.HasIndex(e => e.PreparedByUserId).HasDatabaseName("IX_LND_PortfolioEvaluations_PreparedByUserId");
        builder.HasIndex(e => e.RecommendationSessionItemId).HasDatabaseName("IX_LND_PortfolioEvaluations_RecommendationItemId");
        builder.HasIndex(e => e.ApprovalSessionItemId).HasDatabaseName("IX_LND_PortfolioEvaluations_ApprovalItemId");
        builder.HasIndex(e => e.AppliedByUserId).HasDatabaseName("IX_LND_PortfolioEvaluations_AppliedByUserId");
    }
}

/// <summary><c>LND_PortfolioEvaluationLoans</c>: <c>UK (PortfolioEvaluationId, LoanId)</c>.</summary>
public class PortfolioEvaluationLoanConfiguration : IEntityTypeConfiguration<PortfolioEvaluationLoan>
{
    public const string Tabla = "LND_PortfolioEvaluationLoans";

    public void Configure(EntityTypeBuilder<PortfolioEvaluationLoan> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RuleRatingAtCutoff).HasConversion<int>().IsRequired();
        builder.Property(e => e.ProposedRating).HasConversion<int>().IsRequired();
        builder.Property(e => e.ProposedSubcategory).HasConversion<int?>();
        builder.Property(e => e.Justification).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.CriteriaScoresJson);

        builder.HasOne<PortfolioEvaluation>().WithMany().HasForeignKey(e => e.PortfolioEvaluationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PortfolioEvaluationId, e.LoanId }).IsUnique()
            .HasDatabaseName("UK_LND_PortfolioEvaluationLoans_Evaluation_Loan");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_PortfolioEvaluationLoans_LoanId");
    }
}
