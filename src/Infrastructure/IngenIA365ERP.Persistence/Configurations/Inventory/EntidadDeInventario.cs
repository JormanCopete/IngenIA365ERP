using IngenIA365ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// Lo que toda entidad del catálogo y de las bodegas del comercio declara igual (feature 012, T206–T208; data-model §0):
/// tabla, llave de identidad, <c>PublicId</c> único (<c>UK_{Tabla}_PublicId</c>, Principio VI), <c>RowVersion</c>,
/// auditoría y el filtro de borrado lógico (Principio VII). Cada configuración agrega sus columnas, índices y FK. (nuevo)
/// </summary>
internal static class EntidadDeInventario
{
    public static EntityTypeBuilder<T> ComoEntidadDeInventario<T>(this EntityTypeBuilder<T> builder, string tabla) where T : AuditableEntity
    {
        builder.ToTable(tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{tabla}_PublicId");

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
        return builder;
    }
}
