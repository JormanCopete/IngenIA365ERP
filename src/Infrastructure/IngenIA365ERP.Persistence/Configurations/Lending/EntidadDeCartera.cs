using IngenIA365ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

/// <summary>
/// Lo que toda entidad de Cartera declara igual (feature 013, T0210–T0218; data-model §0.1, §0.13): tabla, llave de identidad,
/// <c>PublicId</c> único (<c>UK_{Tabla}_PublicId</c>, Principio VI), <c>RowVersion</c>, auditoría y el filtro de borrado lógico
/// (Principio VII). Cada configuración agrega sus columnas, índices con nombre, CHECK y FK <c>Restrict</c>. Molde:
/// <c>EntidadDeInventario</c>. (nuevo)
/// </summary>
internal static class EntidadDeCartera
{
    /// <summary>Entidad con <c>int Id</c> (<see cref="AuditableEntity"/>).</summary>
    public static EntityTypeBuilder<T> ComoEntidadDeCartera<T>(this EntityTypeBuilder<T> builder, string tabla) where T : AuditableEntity
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

    /// <summary>Hecho de alto volumen con <c>bigint Id</c> (<see cref="AuditableEntityLong"/>, data-model §0.1, C43).</summary>
    public static EntityTypeBuilder<T> ComoEntidadLargaDeCartera<T>(this EntityTypeBuilder<T> builder, string tabla) where T : AuditableEntityLong
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

    /// <summary>Longitud de toda huella SHA-256 en hexadecimal (data-model §0.13).</summary>
    public const int Huella = 64;
}
