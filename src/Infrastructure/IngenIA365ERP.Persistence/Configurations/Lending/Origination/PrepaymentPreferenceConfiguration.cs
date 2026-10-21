using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_PrepaymentPreferences</c> (feature 013; data-model §2.14): exactamente uno de crédito y cupo
/// (<c>CK_LND_PrepaymentPreferences_Scope</c>); se lee la última por fecha de registro.
/// </summary>
public class PrepaymentPreferenceConfiguration : IEntityTypeConfiguration<PrepaymentPreference>
{
    public const string Tabla = "LND_PrepaymentPreferences";

    public void Configure(EntityTypeBuilder<PrepaymentPreference> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_PrepaymentPreferences_Scope",
            "([LoanId] IS NOT NULL AND [CreditLimitId] IS NULL) OR ([LoanId] IS NULL AND [CreditLimitId] IS NOT NULL)"));

        builder.Property(e => e.Choice).HasConversion<int?>();
        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Origin).HasConversion<int>().IsRequired();
        builder.Property(e => e.EvidenceAttachmentPublicId);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLimit>().WithMany().HasForeignKey(e => e.CreditLimitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.RegisteredAt })
            .HasDatabaseName("IX_LND_PrepaymentPreferences_Loan_RegisteredAt");
        builder.HasIndex(e => new { e.CreditLimitId, e.RegisteredAt })
            .HasDatabaseName("IX_LND_PrepaymentPreferences_Limit_RegisteredAt");
    }
}
