using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_SalesChannels</c> (feature 012, T206; data-model §1.5): código único entre vivos. Declara las FK del canal que la
/// fase 3 dejó para esta historia: <c>INV_DocumentTypes.SalesChannelId</c> e <c>INV_Documents.SalesChannelId</c>.
/// </summary>
public class SalesChannelConfiguration : IEntityTypeConfiguration<SalesChannel>
{
    public void Configure(EntityTypeBuilder<SalesChannel> builder)
    {
        builder.ComoEntidadDeInventario("INV_SalesChannels");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_SalesChannels_Code").HasFilter("[IsDeleted] = 0");

        builder.HasMany<InventoryDocumentType>().WithOne().HasForeignKey(t => t.SalesChannelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocument>().WithOne().HasForeignKey(d => d.SalesChannelId).OnDelete(DeleteBehavior.Restrict);
    }
}
