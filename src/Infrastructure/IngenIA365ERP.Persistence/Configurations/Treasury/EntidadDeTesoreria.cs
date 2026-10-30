using IngenIA365ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury;

/// <summary>
/// Lo que toda entidad de la Tesorería nueva declara igual (el mismo molde que <c>EntidadDeRecaudo</c>, en su espacio para que las configuraciones de Tesorería no vean los alias de Recaudo) (feature 015, T0036; data-model §0.1): tabla, llave de identidad,
/// <c>PublicId</c> único (<c>UK_{Tabla}_PublicId</c>, Principio VI), <c>RowVersion</c>, auditoría y el filtro de borrado lógico (Principio VII).
/// Cada configuración agrega sus columnas, índices con nombre y FK <c>Restrict</c>. Molde: <c>EntidadDeCartera</c>. (nuevo)
/// </summary>
internal static class EntidadDeTesoreria
{
    /// <summary>Entidad con <c>int Id</c> (<see cref="AuditableEntity"/>).</summary>
    public static EntityTypeBuilder<T> ComoEntidadDeTesoreria<T>(this EntityTypeBuilder<T> builder, string tabla) where T : AuditableEntity
    {
        builder.ToTable(tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{tabla}_PublicId");

        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
        return builder;
    }

    /// <summary>Hecho de volumen con <c>bigint Id</c> (<see cref="AuditableEntityLong"/>; data-model §0.1).</summary>
    public static EntityTypeBuilder<T> ComoEntidadLargaDeTesoreria<T>(this EntityTypeBuilder<T> builder, string tabla) where T : AuditableEntityLong
    {
        builder.ToTable(tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{tabla}_PublicId");

        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
        return builder;
    }
}
