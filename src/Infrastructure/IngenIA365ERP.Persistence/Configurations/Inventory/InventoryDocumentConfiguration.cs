using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_Documents</c> (feature 012, T17, T136; data-model §5.1). Enums como int; montos con
/// <see cref="PrecisionDeInventario"/>. El número es único por (tipo, prefijo) entre los que no liberaron su número
/// fiscal (T16) y un documento se anula una sola vez (el descartado no cuenta). FK <c>Restrict</c> a tipo, sucursal,
/// centro de costo, persona, vendedor, usuarios y a sí mismo (anulación); las de bodega y canal las declara la
/// configuración de esas entidades (US1). La tabla llega con <c>InventarioComercialNucleo</c>.
/// </summary>
public class InventoryDocumentConfiguration : IEntityTypeConfiguration<InventoryDocument>
{
    public void Configure(EntityTypeBuilder<InventoryDocument> builder)
    {
        builder.ToTable("INV_Documents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_Documents_PublicId");

        builder.Property(e => e.Class).HasConversion<int>().IsRequired();
        builder.Property(e => e.Prefix).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Number);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.OperationDate).IsRequired();
        builder.Property(e => e.DiscardReason).HasMaxLength(300);
        builder.Property(e => e.ExternalReference).HasMaxLength(60);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        // Tasa de cambio: 1 mientras la moneda sea COP (FR-018); misma precisión que un factor.
        builder.Property(e => e.ExchangeRate).Factor().IsRequired();
        builder.Property(e => e.PostingMode).HasConversion<int?>();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.FiscalNumberReleased).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.OperationMunicipalityDaneCode).HasMaxLength(5);

        builder.Property(e => e.Subtotal).Monto().IsRequired();
        builder.Property(e => e.DiscountTotal).Monto().IsRequired();
        builder.Property(e => e.TaxTotal).Monto().IsRequired();
        builder.Property(e => e.WithholdingTotal).Monto().IsRequired();
        builder.Property(e => e.Total).Monto().IsRequired();
        builder.Property(e => e.AmountDue).Monto().IsRequired();
        builder.Property(e => e.CostTotal).Monto().IsRequired();

        builder.Property(e => e.CountKind).HasConversion<int?>();
        builder.Property(e => e.CountScope).HasConversion<int?>();
        builder.Property(e => e.CountScopeJson).HasMaxLength(4000);
        builder.Property(e => e.IsBlindCount).HasDefaultValue(false).IsRequired();

        builder.Property(e => e.ReturnsGoods).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.IsFullReversal).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.CorrectionConceptCode).HasMaxLength(2);

        builder.HasOne(e => e.DocumentType).WithMany().HasForeignKey(e => e.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.CounterpartyPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Salesperson>().WithMany().HasForeignKey(e => e.SalespersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DiscardedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.VoidsDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.VoidedByDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne(l => l.Document).HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Restrict);

        // T16: el número es único por tipo y prefijo; el rechazado del caso b de FR-066 libera el suyo.
        builder.HasIndex(e => new { e.DocumentTypeId, e.Prefix, e.Number })
            .IsUnique()
            .HasDatabaseName("UK_INV_Documents_Type_Prefix_Number")
            .HasFilter("[Number] IS NOT NULL AND [FiscalNumberReleased] = 0");
        // Un documento se anula una sola vez; la anulación descartada (Status 4) no cuenta.
        builder.HasIndex(e => e.VoidsDocumentId)
            .IsUnique()
            .HasDatabaseName("UK_INV_Documents_VoidsDocumentId")
            .HasFilter("[VoidsDocumentId] IS NOT NULL AND [Status] <> 4");
        builder.HasIndex(e => new { e.Class, e.Status, e.OperationDate }).HasDatabaseName("IX_INV_Documents_Class_Status_OperationDate");
        builder.HasIndex(e => new { e.WarehouseId, e.OperationDate }).HasDatabaseName("IX_INV_Documents_Warehouse_OperationDate");
        builder.HasIndex(e => e.DestinationWarehouseId)
            .HasDatabaseName("IX_INV_Documents_DestinationWarehouseId")
            .HasFilter("[DestinationWarehouseId] IS NOT NULL");
        builder.HasIndex(e => e.CounterpartyPersonId).HasDatabaseName("IX_INV_Documents_CounterpartyPersonId");
        builder.HasIndex(e => new { e.Status, e.OperationDate }).HasDatabaseName("IX_INV_Documents_Status_OperationDate");

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
