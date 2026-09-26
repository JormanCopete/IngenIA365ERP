using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Catalog;

/// <summary>
/// <c>INV_Products</c> (feature 012, T206; data-model §1.6): código de 20 único entre vivos, enums como int, FK
/// <c>Restrict</c> a categoría, marca, unidad base, grupo contable y concepto de retención; peso y volumen como cantidad.
/// Declara también la FK del producto de la línea del documento (<c>INV_DocumentLines.ProductId</c>). El índice de la
/// búsqueda sobre <c>SearchText</c> depende del motor y lo declara <see cref="IndiceDeBusquedaDeProductos"/>.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ComoEntidadDeInventario("INV_Products");

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(40);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.VatSaleTreatment).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(60);
        builder.Property(e => e.Weight).Cantidad();
        builder.Property(e => e.Volume).Cantidad();
        builder.Property(e => e.TracksLot).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.TracksSerial).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.TracksExpiry).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.IsPurchasable).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.IsSellable).HasDefaultValue(true).IsRequired();
        builder.Property(e => e.SearchText).HasMaxLength(Product.LargoDeTextoDeBusqueda).IsRequired();

        builder.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Brand).WithMany().HasForeignKey(e => e.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.BaseUnit).WithMany().HasForeignKey(e => e.BaseUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AccountingGroup).WithMany().HasForeignKey(e => e.AccountingGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WithholdingConcept).WithMany().HasForeignKey(e => e.WithholdingConceptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Units).WithOne(u => u.Product).HasForeignKey(u => u.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Barcodes).WithOne(b => b.Product).HasForeignKey(b => b.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Taxes).WithOne(t => t.Product).HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<InventoryDocumentLine>().WithOne().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_INV_Products_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.CategoryId).HasDatabaseName("IX_INV_Products_CategoryId");
        builder.HasIndex(e => e.BrandId).HasDatabaseName("IX_INV_Products_BrandId");
        builder.HasIndex(e => e.AccountingGroupId).HasDatabaseName("IX_INV_Products_AccountingGroupId");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_INV_Products_Status");
    }
}

/// <summary>
/// El índice de la búsqueda mientras se escribe sobre <c>INV_Products.SearchText</c> (feature 012, T206; T43, SC-009;
/// research, decisión de búsqueda T006): el <c>LIKE '%término%'</c> de cada término no usa un índice B-tree, así que cada
/// motor lleva el suyo. En PostgreSQL, GIN con <c>gin_trgm_ops</c> (trigramas; la extensión <c>pg_trgm</c> la crea el par
/// <c>InventarioComercialNucleo</c>, T440, con <c>CREATE EXTENSION IF NOT EXISTS</c>: declararla aquí en el modelo sería un
/// cambio pendiente en la base de hoy, cuya tabla todavía no existe); en SQL Server, no agrupado con
/// <c>INCLUDE (Code, Name, Status)</c>, que recorre un índice angosto en vez de la tabla. Se aplica desde
/// <c>ApplicationDbContext.OnModelCreating</c>, que conoce el motor. (nuevo)
/// </summary>
public static class IndiceDeBusquedaDeProductos
{
    public const string Nombre = "IX_INV_Products_SearchText";

    public static void Aplicar(ModelBuilder modelBuilder, string? providerName)
    {
        var indice = modelBuilder.Entity<Product>().HasIndex(e => e.SearchText).HasDatabaseName(Nombre);
        if (providerName == ProviderModelConventions.PostgreSqlProviderName)
            indice.HasMethod("gin").HasOperators("gin_trgm_ops");
        else if (providerName == ProviderModelConventions.SqlServerProviderName)
            Microsoft.EntityFrameworkCore.SqlServerIndexBuilderExtensions.IncludeProperties(indice, e => new { e.Code, e.Name, e.Status });
    }
}
