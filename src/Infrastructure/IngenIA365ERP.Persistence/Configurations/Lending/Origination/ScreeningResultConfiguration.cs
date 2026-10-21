using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_ScreeningResults</c> (feature 013; data-model §2.6): hecho de consultas a listas; dos índices filtrados sobre la persona
/// encuentran las coincidencias (<c>Match</c>, <c>PossibleMatch</c>) sin liberar.
/// </summary>
public class ScreeningResultConfiguration : IEntityTypeConfiguration<ScreeningResult>
{
    public const string Tabla = "LND_ScreeningResults";

    public void Configure(EntityTypeBuilder<ScreeningResult> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Context).HasConversion<int>().IsRequired();
        builder.Property(e => e.SalePaymentPublicId);
        builder.Property(e => e.ChannelCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ListVersions).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.Matches);
        builder.Property(e => e.IsSimulated).IsRequired();
        builder.Property(e => e.QueriedAt).IsRequired();
        builder.Property(e => e.ReleasedAt);
        builder.Property(e => e.ReleaseReason).HasMaxLength(500);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLimit>().WithMany().HasForeignKey(e => e.CreditLimitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.QueriedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PersonId, e.QueriedAt })
            .HasDatabaseName("IX_LND_ScreeningResults_Person_QueriedAt");
        builder.HasIndex(e => e.PersonId, "IX_LND_ScreeningResults_PendingMatch")
            .HasFilter("[Outcome] = 3 AND [ReleasedAt] IS NULL");
        builder.HasIndex(e => e.PersonId, "IX_LND_ScreeningResults_PendingPossible")
            .HasFilter("[Outcome] = 2 AND [ReleasedAt] IS NULL");
    }
}
