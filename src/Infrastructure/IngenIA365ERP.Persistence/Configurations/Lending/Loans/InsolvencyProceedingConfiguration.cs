using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Loans;

/// <summary>
/// <c>LND_InsolvencyProceedings</c> (feature 013; data-model §25.8): un proceso en negociación por persona (único filtrado a
/// <c>Negotiating</c> y vivo). <c>AgreementNoveltyId</c> nace con K3 (<c>CarteraNovedades</c>).
/// </summary>
public class InsolvencyProceedingConfiguration : IEntityTypeConfiguration<InsolvencyProceeding>
{
    public const string Tabla = "LND_InsolvencyProceedings";

    public void Configure(EntityTypeBuilder<InsolvencyProceeding> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Regime).HasConversion<int?>();
        builder.Property(e => e.Authority).HasMaxLength(150);
        builder.Property(e => e.CaseNumber).HasMaxLength(60);
        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.CloseReason).HasMaxLength(500);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanNovelty>().WithMany().HasForeignKey(e => e.AgreementNoveltyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.PersonId).IsUnique()
            .HasDatabaseName("UK_LND_InsolvencyProceedings_Person_Negotiating").HasFilter("[Status] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.AgreementNoveltyId)
            .HasDatabaseName("IX_LND_InsolvencyProceedings_AgreementNoveltyId").HasFilter("[AgreementNoveltyId] IS NOT NULL");
    }
}
