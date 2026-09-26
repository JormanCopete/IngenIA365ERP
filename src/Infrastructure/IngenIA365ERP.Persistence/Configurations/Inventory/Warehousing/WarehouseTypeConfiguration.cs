using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Warehousing;

/// <summary><c>INV_WarehouseTypes</c> (feature 012, T207; data-model §2.1): código único entre vivos, comportamiento como int.</summary>
public class WarehouseTypeConfiguration : IEntityTypeConfiguration<WarehouseType>
{
    public void Configure(EntityTypeBuilder<WarehouseType> builder)
    {
        builder.ComoEntidadDeInventario("INV_WarehouseTypes");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Behavior).HasConversion<int>().IsRequired();
        builder.Property(e => e.IsSeeded).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_WarehouseTypes_Code").HasFilter("[IsDeleted] = 0");
    }
}
