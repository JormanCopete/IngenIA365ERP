using IngenIA365ERP.Domain.Entities.Inventory.Security;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_UserPointOfSaleScopes</c> (feature 012, I3, T583; T35; data-model §21): una asignación viva por (usuario, punto) y a lo
/// sumo una por defecto viva por usuario. FK <c>Restrict</c> a <c>SEC_Users</c> e <c>INV_PointsOfSale</c>. Entra en el par
/// <c>VentasYPuntoDeVenta</c>.
/// </summary>
public class UserPointOfSaleScopeConfiguration : IEntityTypeConfiguration<UserPointOfSaleScope>
{
    public void Configure(EntityTypeBuilder<UserPointOfSaleScope> builder)
    {
        builder.ComoEntidadDeInventario("INV_UserPointOfSaleScopes");

        builder.Property(e => e.IsDefault).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PointOfSale).WithMany().HasForeignKey(e => e.PointOfSaleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.UserId, e.PointOfSaleId })
            .IsUnique().HasDatabaseName("UK_INV_UserPointOfSaleScopes_User_Point").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.UserId, "UK_INV_UserPointOfSaleScopes_Default")
            .IsUnique().HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");
    }
}
