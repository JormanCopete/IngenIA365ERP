using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pricing;

/// <summary>
/// <c>INV_PriceLists</c> (feature 012, I3, T584; FR-053, T51; data-model §14): código único entre vivas y único
/// <c>(ScopeKey, ValidFrom)</c> entre vivas (la unicidad portable del ámbito); FK <c>Restrict</c> a persona, canal y sucursal.
/// Declara también la FK de <c>INV_DocumentLines.PriceListId</c>.
/// </summary>
public class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ComoEntidadDeInventario("INV_PriceLists");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_PriceLists_Code").HasFilter("[IsDeleted] = 0");
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Segment).HasMaxLength(4);
        builder.Property(e => e.ScopeKey).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.Notes).HasMaxLength(300);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesChannel>().WithMany().HasForeignKey(e => e.SalesChannelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocumentLine>().WithOne().HasForeignKey(l => l.PriceListId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ScopeKey, e.ValidFrom }).IsUnique()
            .HasDatabaseName("UK_INV_PriceLists_Scope_ValidFrom").HasFilter("[IsDeleted] = 0");
    }
}
