using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

// Feature 013, K3 (T0875; data-model §25.4–§25.5): las condonaciones y sus líneas por concepto (CarteraNovedades).

/// <summary><c>LND_Waivers</c>: número único; sus líneas por navegación; la operación <c>Waiver</c> que la aplica, también.</summary>
public class WaiverConfiguration : IEntityTypeConfiguration<Waiver>
{
    public const string Tabla = "LND_Waivers";

    public void Configure(EntityTypeBuilder<Waiver> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.AsOfDate).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.DaysPastDueAtRequest).IsRequired();
        builder.Property(e => e.IsRelatedParty).IsRequired();
        builder.Property(e => e.TotalAmount).Monto().IsRequired();
        builder.Property(e => e.RequestedAt).IsRequired();
        builder.Ignore(e => e.Editable);
        builder.Ignore(e => e.CondonaCapital);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.FinalDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AppliedOperation).WithMany().HasForeignKey(e => e.AppliedOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AppliedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne().HasForeignKey(l => l.WaiverId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_Waivers_Number");
        builder.HasIndex(e => new { e.LoanId, e.Status }).HasDatabaseName("IX_LND_Waivers_Loan_Status");
        builder.HasIndex(e => e.RequestedByUserId).HasDatabaseName("IX_LND_Waivers_RequestedByUserId");
        builder.HasIndex(e => e.FinalDecisionId).HasDatabaseName("IX_LND_Waivers_FinalDecisionId");
        builder.HasIndex(e => e.AppliedOperationId).HasDatabaseName("IX_LND_Waivers_AppliedOperationId");
        builder.HasIndex(e => e.AppliedByUserId).HasDatabaseName("IX_LND_Waivers_AppliedByUserId");
    }
}

/// <summary><c>LND_WaiverLines</c>: <c>UK (WaiverId, Concept, InstallmentNumber)</c>.</summary>
public class WaiverLineConfiguration : IEntityTypeConfiguration<WaiverLine>
{
    public const string Tabla = "LND_WaiverLines";

    public void Configure(EntityTypeBuilder<WaiverLine> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Concept).HasConversion<int>().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.AvailableAtRequest).Monto().IsRequired();

        builder.HasIndex(e => new { e.WaiverId, e.Concept, e.InstallmentNumber }).IsUnique()
            .HasDatabaseName("UK_LND_WaiverLines_Waiver_Concept_Installment");
    }
}
