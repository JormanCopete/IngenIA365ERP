using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_DocumentTaxLines</c> (feature 012, T22, T136; data-model §5.7). Hecho de sólo inserción; tarifa como fracción
/// (9,6), base y valor en pesos (18,2), unidades gravables (18,4). Índices por documento y por (impuesto, documento).
/// FK <c>Restrict</c> al documento, a su línea y al catálogo tributario (<c>COR_TaxDefinitions</c>, la fila de
/// <c>COR_TaxRates</c> que aplicó y <c>COR_WithholdingConcepts</c>; éstas desde la sección tributaria de la fase 3, T161).
/// </summary>
public class DocumentTaxLineConfiguration : IEntityTypeConfiguration<DocumentTaxLine>
{
    public void Configure(EntityTypeBuilder<DocumentTaxLine> builder)
    {
        builder.ToTable("INV_DocumentTaxLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_DocumentTaxLines_PublicId");

        builder.Property(e => e.TaxDefinitionId).IsRequired();
        builder.Property(e => e.TaxRateId).IsRequired();
        builder.Property(e => e.TaxRateCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Treatment).HasConversion<int>().IsRequired();
        builder.Property(e => e.MunicipalityDaneCode).HasMaxLength(5);
        builder.Property(e => e.Rate).Tarifa();
        builder.Property(e => e.AmountPerUnit).Monto();
        builder.Property(e => e.TaxableUnits).Cantidad();
        builder.Property(e => e.Base).Monto().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.DianTaxCode).HasMaxLength(3);
        builder.Property(e => e.ExplanationJson).IsRequired();

        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocumentLine>().WithMany().HasForeignKey(e => e.DocumentLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxDefinition>().WithMany().HasForeignKey(e => e.TaxDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxRate>().WithMany().HasForeignKey(e => e.TaxRateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WithholdingConcept>().WithMany().HasForeignKey(e => e.WithholdingConceptId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.DocumentId).HasDatabaseName("IX_INV_DocumentTaxLines_DocumentId");
        builder.HasIndex(e => new { e.TaxDefinitionId, e.DocumentId }).HasDatabaseName("IX_INV_DocumentTaxLines_TaxDefinition_Document");

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
