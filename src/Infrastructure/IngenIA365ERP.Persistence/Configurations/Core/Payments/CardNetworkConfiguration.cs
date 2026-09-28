using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core.Payments;

/// <summary><c>COR_CardNetworks</c> (feature 012, I3, T582; data-model §16): código único entre vivos.</summary>
public class CardNetworkConfiguration : IEntityTypeConfiguration<CardNetwork>
{
    public void Configure(EntityTypeBuilder<CardNetwork> builder)
    {
        builder.ComoEntidadDeInventario("COR_CardNetworks");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_COR_CardNetworks_Code").HasFilter("[IsDeleted] = 0");
        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.CardKind).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);
    }
}
