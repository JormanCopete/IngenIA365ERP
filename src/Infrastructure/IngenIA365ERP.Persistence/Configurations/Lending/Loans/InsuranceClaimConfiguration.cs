using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Loans;

/// <summary>
/// <c>LND_InsuranceClaims</c> (feature 013; data-model §25.7): una reclamación en trámite por crédito (único filtrado a
/// <c>InProcess</c> y viva); aseguradora y fallecido son personas.
/// </summary>
public class InsuranceClaimConfiguration : IEntityTypeConfiguration<InsuranceClaim>
{
    public const string Tabla = "LND_InsuranceClaims";

    public void Configure(EntityTypeBuilder<InsuranceClaim> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.DeathDate).IsRequired();
        builder.Property(e => e.ClaimNumber).HasMaxLength(40);
        builder.Property(e => e.ClaimedAmount).Monto();
        builder.Property(e => e.PaidAmount).Monto();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.CloseReason).HasMaxLength(500);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.InsurerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.DeceasedPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.PaidOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.LoanId).IsUnique()
            .HasDatabaseName("UK_LND_InsuranceClaims_Loan_InProcess").HasFilter("[Status] = 1 AND [IsDeleted] = 0");
    }
}
