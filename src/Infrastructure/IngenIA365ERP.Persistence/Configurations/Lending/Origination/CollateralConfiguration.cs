using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_Collaterals</c> (feature 013; data-model §2.8): sólo las clases reales y de fondo (<c>CK_LND_Collaterals_Kind</c>); la
/// liberación apunta a la operación (bigint) que canceló.
/// </summary>
public class CollateralConfiguration : IEntityTypeConfiguration<Collateral>
{
    public const string Tabla = "LND_Collaterals";

    public void Configure(EntityTypeBuilder<Collateral> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_Collaterals_Kind",
            "[Kind] = 3 OR [Kind] = 4 OR [Kind] = 5 OR [Kind] = 6"));

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Description).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Value).Monto().IsRequired();
        builder.Property(e => e.AdmissibleValue).Monto().IsRequired();
        builder.Property(e => e.ValuationDate);
        builder.Property(e => e.PolicyNumber).HasMaxLength(40);
        builder.Property(e => e.PolicyExpiresOn);
        builder.Property(e => e.RegistrationReference).HasMaxLength(60);
        builder.Property(e => e.MortgageRank);
        builder.Property(e => e.FundCoverage).Fraccion();
        builder.Property(e => e.FundFee).Fraccion();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ConstitutedOn).IsRequired();
        builder.Property(e => e.ValidUntil);
        builder.Property(e => e.ReleasedOn);
        builder.Property(e => e.ExtinguishedOn);
        builder.Property(e => e.ExtinguishReason).HasMaxLength(300);
        builder.Property(e => e.ExecutedOn);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.OwnerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.AppraiserPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PolicyInsurerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.ReleaseOperationId).OnDelete(DeleteBehavior.Restrict);
    }
}
