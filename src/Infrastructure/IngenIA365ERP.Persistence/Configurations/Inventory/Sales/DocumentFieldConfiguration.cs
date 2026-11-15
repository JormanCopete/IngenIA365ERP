using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Sales;

/// <summary><c>INV_DocumentFieldDefinitions</c> (2026-11-15): los campos adicionales de venta de la cooperativa, código único entre vivos.</summary>
public class DocumentFieldDefinitionConfiguration : IEntityTypeConfiguration<DocumentFieldDefinition>
{
    public void Configure(EntityTypeBuilder<DocumentFieldDefinition> builder)
    {
        builder.ComoEntidadDeInventario("INV_DocumentFieldDefinitions");
        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(DocumentFieldDefinition.LargoDelNombre).IsRequired();
        builder.Property(e => e.DataType).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_DocumentFieldDefinitions_Code").HasFilter("[IsDeleted] = 0");
    }
}

/// <summary><c>INV_DocumentFieldValues</c> (2026-11-15): el valor sellado de cada campo en un documento; uno por campo y documento entre vivos.</summary>
public class DocumentFieldValueConfiguration : IEntityTypeConfiguration<DocumentFieldValue>
{
    public void Configure(EntityTypeBuilder<DocumentFieldValue> builder)
    {
        builder.ComoEntidadDeInventario("INV_DocumentFieldValues");
        builder.Property(e => e.FieldCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FieldName).HasMaxLength(DocumentFieldDefinition.LargoDelNombre).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(DocumentFieldDefinition.TamanoMaximo).IsRequired();
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Definition).WithMany().HasForeignKey(e => e.DefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.DocumentId, e.DefinitionId }).IsUnique()
            .HasDatabaseName("UK_INV_DocumentFieldValues_Document_Definition").HasFilter("[IsDeleted] = 0");
    }
}
