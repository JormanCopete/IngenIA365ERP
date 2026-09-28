using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pricing;

/// <summary>
/// <c>INV_DiscountCaps</c> (feature 012, I3, T584; FR-054, T51; data-model §14): único <c>(RoleId, ValidFrom)</c> entre vivos;
/// topes como fracción (9,6); motivo obligatorio.
/// </summary>
public class DiscountCapConfiguration : IEntityTypeConfiguration<DiscountCap>
{
    public void Configure(EntityTypeBuilder<DiscountCap> builder)
    {
        builder.ComoEntidadDeInventario("INV_DiscountCaps");

        builder.Property(e => e.MaxLineRate).Tarifa().IsRequired();
        builder.Property(e => e.MaxDocumentRate).Tarifa().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.RoleId, e.ValidFrom }).IsUnique()
            .HasDatabaseName("UK_INV_DiscountCaps_Role_ValidFrom").HasFilter("[IsDeleted] = 0");
    }
}
