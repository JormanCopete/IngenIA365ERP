using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

// Los tres conjuntos de disponibilidad de un medio de pago (feature 012, I3, T583; T25; data-model §15): cada par es único entre
// vivos y las FK son Restrict. Van juntos porque son la misma forma con otra segunda columna.

/// <summary><c>INV_PaymentMeansPointsOfSale</c>.</summary>
public class PaymentMeansPointOfSaleConfiguration : IEntityTypeConfiguration<PaymentMeansPointOfSale>
{
    public void Configure(EntityTypeBuilder<PaymentMeansPointOfSale> builder)
    {
        builder.ComoEntidadDeInventario("INV_PaymentMeansPointsOfSale");

        builder.HasOne(e => e.PaymentMeans).WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PointOfSale).WithMany().HasForeignKey(e => e.PointOfSaleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PaymentMeansId, e.PointOfSaleId }).IsUnique()
            .HasDatabaseName("UK_INV_PaymentMeansPointsOfSale_Means_Point").HasFilter("[IsDeleted] = 0");
    }
}

/// <summary><c>INV_PaymentMeansChannels</c>.</summary>
public class PaymentMeansChannelConfiguration : IEntityTypeConfiguration<PaymentMeansChannel>
{
    public void Configure(EntityTypeBuilder<PaymentMeansChannel> builder)
    {
        builder.ComoEntidadDeInventario("INV_PaymentMeansChannels");

        builder.HasOne(e => e.PaymentMeans).WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesChannel>().WithMany().HasForeignKey(e => e.SalesChannelId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PaymentMeansId, e.SalesChannelId }).IsUnique()
            .HasDatabaseName("UK_INV_PaymentMeansChannels_Means_Channel").HasFilter("[IsDeleted] = 0");
    }
}

/// <summary><c>INV_PaymentMeansDocumentTypes</c>.</summary>
public class PaymentMeansDocumentTypeConfiguration : IEntityTypeConfiguration<PaymentMeansDocumentType>
{
    public void Configure(EntityTypeBuilder<PaymentMeansDocumentType> builder)
    {
        builder.ComoEntidadDeInventario("INV_PaymentMeansDocumentTypes");

        builder.HasOne(e => e.PaymentMeans).WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryDocumentType>().WithMany().HasForeignKey(e => e.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PaymentMeansId, e.DocumentTypeId }).IsUnique()
            .HasDatabaseName("UK_INV_PaymentMeansDocumentTypes_Means_Type").HasFilter("[IsDeleted] = 0");
    }
}
