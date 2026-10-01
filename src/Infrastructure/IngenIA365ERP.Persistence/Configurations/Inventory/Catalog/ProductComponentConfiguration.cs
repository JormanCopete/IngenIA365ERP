using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_ProductComponents</c> (feature 012, I6, T857; data-model §1.11): único <c>(ProductId, ComponentProductId)</c> entre vivos,
/// cantidad (18,4) en la unidad base del componente y FK <c>Restrict</c> al combo o kit (declarada en
/// <see cref="ProductConfiguration"/>) y al componente. Nace con <c>ComercioAmpliado</c>.
/// </summary>
public class ProductComponentConfiguration : IEntityTypeConfiguration<ProductComponent>
{
    public void Configure(EntityTypeBuilder<ProductComponent> builder)
    {
        builder.ComoEntidadDeInventario("INV_ProductComponents");

        builder.Property(e => e.Quantity).Cantidad().IsRequired();

        builder.HasOne(e => e.ComponentProduct).WithMany().HasForeignKey(e => e.ComponentProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProductId, e.ComponentProductId }).IsUnique()
            .HasDatabaseName("UK_INV_ProductComponents_Product_Component").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.ComponentProductId).HasDatabaseName("IX_INV_ProductComponents_ComponentProductId");
    }
}
