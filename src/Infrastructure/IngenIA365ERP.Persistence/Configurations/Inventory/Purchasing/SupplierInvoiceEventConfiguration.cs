using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Purchasing;

/// <summary>
/// <c>INV_SupplierInvoiceEvents</c> (feature 012, T337; data-model §9.3): los eventos 030 y 032 de una factura del proveedor,
/// uno de cada por factura (<c>UK (DocumentId, EventCode)</c> entre los vivos). Sin migración propia: entra al par
/// <c>InventarioComercialNucleo</c> (<see cref="NucleoComercialSinMigracion"/>).
/// </summary>
public class SupplierInvoiceEventConfiguration : IEntityTypeConfiguration<SupplierInvoiceEvent>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceEvent> builder)
    {
        builder.ComoEntidadDeInventario("INV_SupplierInvoiceEvents");

        builder.Property(e => e.EventCode).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(SupplierInvoiceEvent.LargoDeLaFuente);
        builder.Property(e => e.Cude).HasMaxLength(SupplierInvoiceEvent.LargoDelCude);
        builder.Property(e => e.Notes).HasMaxLength(SupplierInvoiceEvent.LargoDeLasNotas);

        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DocumentId, e.EventCode }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_INV_SupplierInvoiceEvents_Document_EventCode");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_INV_SupplierInvoiceEvents_Status");
    }
}
