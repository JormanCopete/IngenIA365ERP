using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Loans;

/// <summary>
/// <c>LND_LoanParties</c> (feature 013; data-model §4.2): una persona con un rol por crédito entre las vivas; la operación que la
/// libera es <c>bigint</c>.
/// </summary>
public class LoanPartyConfiguration : IEntityTypeConfiguration<LoanParty>
{
    public const string Tabla = "LND_LoanParties";

    public void Configure(EntityTypeBuilder<LoanParty> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Role).HasConversion<int>().IsRequired();
        builder.Property(e => e.JoinedOn).IsRequired();
        builder.Property(e => e.ExtinguishReason).HasMaxLength(300);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditApplicationParty>().WithMany().HasForeignKey(e => e.SourceApplicationPartyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.ReleaseOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.ExtinguishDecisionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.PersonId, e.Role }).IsUnique()
            .HasDatabaseName("UK_LND_LoanParties_Loan_Person_Role").HasFilter("[IsDeleted] = 0");
    }
}
