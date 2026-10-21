using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary><c>LND_CreditScores</c> (feature 013; data-model §2.5): hecho de puntaje con su precisión <c>Puntaje</c>.</summary>
public class CreditScoreConfiguration : IEntityTypeConfiguration<CreditScore>
{
    public const string Tabla = "LND_CreditScores";

    public void Configure(EntityTypeBuilder<CreditScore> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Score).Puntaje().IsRequired();
        builder.Property(e => e.MinimumScore).Puntaje();
        builder.Property(e => e.BelowMinimum).IsRequired();
        builder.Property(e => e.Explanation).IsRequired();
        builder.Property(e => e.InputsSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.CalculatedAt).IsRequired();

        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLineVersion>().WithMany().HasForeignKey(e => e.CreditLineVersionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ApplicationId, e.CalculatedAt })
            .HasDatabaseName("IX_LND_CreditScores_App_CalculatedAt");
    }
}
