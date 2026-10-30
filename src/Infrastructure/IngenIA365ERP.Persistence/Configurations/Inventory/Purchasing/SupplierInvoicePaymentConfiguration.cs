using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Purchasing;

/// <summary>
/// <c>INV_SupplierInvoicePayments</c> (feature 015, T2, fase 42, T0454; E-03): el pago informado por Tesorería; el mensaje es único y cada pago cuelga de su factura. Entra
/// al par <c>TesoreriaAmpliada</c> (como la columna de la remesa del cierre del día de T1): no toca los pares congelados de la 012. (nuevo)
/// </summary>
public class SupplierInvoicePaymentConfiguration : IEntityTypeConfiguration<SupplierInvoicePayment>
{
    public const string Tabla = "INV_SupplierInvoicePayments";

    public void Configure(EntityTypeBuilder<SupplierInvoicePayment> builder)
    {
        builder.ComoEntidadDeInventario(Tabla);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.TreasuryReference).HasMaxLength(60);
        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_Message");
        builder.HasIndex(e => e.DocumentId).HasDatabaseName($"IX_{Tabla}_Document");
    }
}
