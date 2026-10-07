using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary><c>LND_CreditApplicationParties</c> (feature 013; data-model §2.2): una persona una sola vez por solicitud entre vivas.</summary>
public class CreditApplicationPartyConfiguration : IEntityTypeConfiguration<CreditApplicationParty>
{
    public const string Tabla = "LND_CreditApplicationParties";

    public void Configure(EntityTypeBuilder<CreditApplicationParty> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Role).HasConversion<int>().IsRequired();
        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.MonthlyIncome).Monto();
        builder.Property(e => e.MonthlyExpenses).Monto();
        builder.Property(e => e.ExternalInstallments).Monto();

        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ApplicationId, e.PersonId }).IsUnique()
            .HasDatabaseName("UK_LND_CreditApplicationParties_App_Person").HasFilter("[IsDeleted] = 0");
    }
}
