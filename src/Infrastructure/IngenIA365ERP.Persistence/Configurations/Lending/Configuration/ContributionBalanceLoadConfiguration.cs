using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary><c>LND_ContributionBalanceLoads</c> (feature 013, T0210; data-model §2.15): único <c>(PersonId, AsOfDate, ImportPublicId)</c>.</summary>
public class ContributionBalanceLoadConfiguration : IEntityTypeConfiguration<ContributionBalanceLoad>
{
    public const string Tabla = "LND_ContributionBalanceLoads";

    public void Configure(EntityTypeBuilder<ContributionBalanceLoad> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.AsOfDate).IsRequired();
        builder.Property(e => e.ContributionsBalance).Monto().IsRequired();
        builder.Property(e => e.PermanentSavingsBalance).Monto().IsRequired();
        builder.Property(e => e.ImportPublicId).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(120);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PersonId, e.AsOfDate, e.ImportPublicId }).IsUnique()
            .HasDatabaseName("UK_LND_ContributionBalanceLoads_Person_Date_Import");
    }
}
