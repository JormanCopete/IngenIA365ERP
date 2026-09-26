using IngenIA365ERP.Domain.Entities.Inventory.Security;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_UserWarehouseScopes</c> (feature 012, T208; T35; data-model §21): una asignación viva por (usuario, bodega) y a lo
/// sumo una por defecto viva por usuario. FK <c>Restrict</c> a <c>SEC_Users</c> e <c>INV_Warehouses</c>. Entra en el par
/// <c>InventarioComercialNucleo</c>.
/// </summary>
public class UserWarehouseScopeConfiguration : IEntityTypeConfiguration<UserWarehouseScope>
{
    public void Configure(EntityTypeBuilder<UserWarehouseScope> builder)
    {
        builder.ComoEntidadDeInventario("INV_UserWarehouseScopes");

        builder.Property(e => e.IsDefault).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.UserId, e.WarehouseId })
            .IsUnique().HasDatabaseName("UK_INV_UserWarehouseScopes_User_Warehouse").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.UserId, "UK_INV_UserWarehouseScopes_Default")
            .IsUnique().HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");
    }
}
