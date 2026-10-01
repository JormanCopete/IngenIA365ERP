using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary><c>INV_AccountingGroups</c> (feature 012, T206; data-model §1.4): código único entre vivos e inmutable (T27).</summary>
public class AccountingGroupConfiguration : IEntityTypeConfiguration<AccountingGroup>
{
    public void Configure(EntityTypeBuilder<AccountingGroup> builder)
    {
        builder.ComoEntidadDeInventario("INV_AccountingGroups");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(300);
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_AccountingGroups_Code").HasFilter("[IsDeleted] = 0");
    }
}
