using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplications</c> (feature 013; data-model §2.1): radicado único cuando existe, índices de bandeja por solicitante, oficina
/// y vencimiento de la aprobación. La libranza de pagaduría externa (K2) no tiene columnas todavía.
/// </summary>
public class CreditApplicationConfiguration : IEntityTypeConfiguration<CreditApplication>
{
    public const string Tabla = "LND_CreditApplications";

    public void Configure(EntityTypeBuilder<CreditApplication> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20);
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.DebtorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestedAmount).Monto().IsRequired();
        builder.Property(e => e.Periodicity).HasConversion<int?>();
        builder.Property(e => e.AmortizationSystem).HasConversion<int?>();
        builder.Property(e => e.GraceKind).HasConversion<int?>();
        builder.Property(e => e.ExtraordinaryInstallments);
        builder.Property(e => e.SelectedInsurances);
        builder.Property(e => e.SimulationSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.Purpose).HasMaxLength(300);
        builder.Property(e => e.RepaymentChannel).HasConversion<int>().IsRequired();
        builder.Property(e => e.PayrollEmployeeIdentifier).HasMaxLength(30);
        builder.Property(e => e.PrepaymentChoice).HasConversion<int?>();
        builder.Property(e => e.PayoffRequest);
        builder.Property(e => e.PolicyExceptionRequested).IsRequired();
        builder.Property(e => e.PolicyExceptionReason).HasMaxLength(500);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.RoutingSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.ConsolidatedExposure).Monto();
        builder.Property(e => e.IsRelatedParty).IsRequired();
        builder.Property(e => e.ApprovedAmount).Monto();
        builder.Property(e => e.ApprovedTerms);
        builder.Property(e => e.CancelReason).HasMaxLength(500);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.ApplicantPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLineVersion>().WithMany().HasForeignKey(e => e.CreditLineVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttributionMatrix>().WithMany().HasForeignKey(e => e.AttributionMatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttributionLevel>().WithMany().HasForeignKey(e => e.RequiredLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.ApprovalDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLimit>().WithMany().HasForeignKey(e => e.CreditLimitId).OnDelete(DeleteBehavior.Restrict);
        // K2 (CarteraLibranza): el convenio de la libranza de pagaduría externa (FR-109).
        builder.HasOne<PayrollAgreement>().WithMany().HasForeignKey(e => e.PayrollAgreementId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique()
            .HasDatabaseName("UK_LND_CreditApplications_Number").HasFilter("[Number] IS NOT NULL");
        builder.HasIndex(e => new { e.ApplicantPersonId, e.Status })
            .HasDatabaseName("IX_LND_CreditApplications_Applicant_Status");
        builder.HasIndex(e => new { e.BranchId, e.Status })
            .HasDatabaseName("IX_LND_CreditApplications_Branch_Status");
        builder.HasIndex(e => new { e.Status, e.ApprovalValidUntil })
            .HasDatabaseName("IX_LND_CreditApplications_Status_ApprovalValidUntil");
    }
}
