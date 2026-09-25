using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_DocumentTypes</c> (feature 012, FR-037, T136; data-model §5.8). Código único entre vivos (inmutable), índice
/// por clase; clase como int. Sus bodegas permitidas y sus consecutivos cuelgan con FK <c>Restrict</c>. La FK al canal
/// de venta la declara la configuración del canal (US1).
/// </summary>
public class InventoryDocumentTypeConfiguration : IEntityTypeConfiguration<InventoryDocumentType>
{
    public void Configure(EntityTypeBuilder<InventoryDocumentType> builder)
    {
        builder.ToTable("INV_DocumentTypes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_DocumentTypes_PublicId");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Class).HasConversion<int>().IsRequired();
        builder.Property(e => e.FiscalPrefix).HasMaxLength(4);
        builder.Property(e => e.IsContingency).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.RequiresCounterparty).IsRequired();
        builder.Property(e => e.RequiresCostCenter).IsRequired();
        builder.Property(e => e.RequiresReason).IsRequired();
        builder.Property(e => e.RequiresExternalReference).IsRequired();
        builder.Property(e => e.IsTaxableWithdrawal).IsRequired();
        builder.Property(e => e.VatNonDeductible).IsRequired();
        builder.Property(e => e.AllowsFutureDate).IsRequired();
        builder.Property(e => e.AllWarehouses).IsRequired();
        builder.Property(e => e.IsSeeded).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasMany(e => e.Warehouses).WithOne(w => w.DocumentType).HasForeignKey(w => w.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Sequences).WithOne(s => s.DocumentType).HasForeignKey(s => s.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_DocumentTypes_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.Class).HasDatabaseName("IX_INV_DocumentTypes_Class");

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
