using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Purchasing;

/// <summary>
/// <c>INV_SupplierInvoiceDetails</c> (feature 012, T337; data-model §9.2): 1:1 con la factura o nota del proveedor
/// (<c>UK (DocumentId)</c>), y la misma factura no se registra dos veces entre los no liberados: por (proveedor, clase, prefijo,
/// número) y por CUFE. Los nombres de los dos índices los conoce <see cref="ColisionDeFacturaDeProveedor"/>, que traduce la
/// violación (una carrera entre dos usuarios) a <c>Inventory.SupplierInvoice.Duplicate</c> / <c>.CufeDuplicate</c>. Sin
/// migración propia: entra al par <c>InventarioComercialNucleo</c> (<see cref="NucleoComercialSinMigracion"/>).
/// </summary>
public class SupplierInvoiceDetailConfiguration : IEntityTypeConfiguration<SupplierInvoiceDetail>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceDetail> builder)
    {
        builder.ComoEntidadDeInventario("INV_SupplierInvoiceDetails");

        builder.Property(e => e.DocumentClass).IsRequired();
        builder.Property(e => e.SupplierPrefix).HasMaxLength(SupplierInvoiceDetail.LargoDelPrefijo).IsRequired();
        builder.Property(e => e.SupplierNumber).HasMaxLength(SupplierInvoiceDetail.LargoDelNumero).IsRequired();
        builder.Property(e => e.Cufe).HasMaxLength(SupplierInvoiceDetail.LargoDelCufe);
        builder.Property(e => e.IssueDate).IsRequired();
        builder.Property(e => e.IsCredit).IsRequired();
        builder.Property(e => e.IsElectronic).IsRequired();
        builder.Property(e => e.IsDebitNote).IsRequired();
        builder.Property(e => e.IsReleased).IsRequired().HasDefaultValue(false);
        builder.Ignore(e => e.PaymentForm);
        builder.Ignore(e => e.NumeroVisible);

        builder.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.SupplierPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.DocumentId).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_INV_SupplierInvoiceDetails_Document");
        builder.HasIndex(e => new { e.SupplierPersonId, e.DocumentClass, e.SupplierPrefix, e.SupplierNumber }).IsUnique()
            .HasFilter("[IsReleased] = 0 AND [IsDeleted] = 0")
            .HasDatabaseName(ColisionDeFacturaDeProveedor.IndiceDelNumero);
        builder.HasIndex(e => e.Cufe).IsUnique()
            .HasFilter("[Cufe] IS NOT NULL AND [IsReleased] = 0 AND [IsDeleted] = 0")
            .HasDatabaseName(ColisionDeFacturaDeProveedor.IndiceDelCufe);
    }
}
