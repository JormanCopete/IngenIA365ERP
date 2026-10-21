using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary><c>LND_CreditLineDocumentRequirements</c> (feature 013, T0210; data-model §1.5): único <c>(Version, Code)</c>.</summary>
public class CreditLineDocumentRequirementConfiguration : IEntityTypeConfiguration<CreditLineDocumentRequirement>
{
    public const string Tabla = "LND_CreditLineDocumentRequirements";

    public void Configure(EntityTypeBuilder<CreditLineDocumentRequirement> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.AppliesTo).HasConversion<int?>();
        builder.Property(e => e.MinAmount).Monto();
        builder.Property(e => e.MaxAmount).Monto();
        builder.Property(e => e.DebtorKind).HasConversion<int?>();
        builder.Property(e => e.RepaymentChannel).HasConversion<int?>();
        builder.Property(e => e.Order).IsRequired();

        builder.HasOne<CreditLineVersion>().WithMany().HasForeignKey(e => e.CreditLineVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CreditLineVersionId, e.Code }).IsUnique()
            .HasDatabaseName("UK_LND_CreditLineDocumentRequirements_Version_Code");
    }
}
