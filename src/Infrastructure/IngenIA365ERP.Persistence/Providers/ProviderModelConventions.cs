using IngenIA365ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IngenIA365ERP.Persistence.Providers;

/// <summary>
/// Convenciones de modelo que absorben las diferencias entre motores (feature
/// 004, D-06). Este es el UNICO lugar donde el modelo conoce el proveedor;
/// las IEntityTypeConfiguration individuales permanecen neutras.
/// </summary>
public static class ProviderModelConventions
{
    public const string SqlServerProviderName = "Microsoft.EntityFrameworkCore.SqlServer";
    public const string PostgreSqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>
    /// Concurrencia optimista portable sobre toda entidad con una propiedad
    /// <c>byte[] RowVersion</c> (BaseEntity/BaseEntityLong y entidades Admin):
    ///  - SQL Server → columna nativa <c>ROWVERSION</c> (comportamiento Fase 0/1).
    ///  - PostgreSQL → la propiedad CLR se des-mapea y el token pasa a ser la
    ///    columna de sistema <c>xmin</c> (uint, shadow property).
    /// En ambos casos un conflicto aflora como <c>DbUpdateConcurrencyException</c>,
    /// que los DbContext ya traducen a <c>ConcurrencyConflictException</c> — la
    /// semantica hacia Application/Domain es identica (FR-005).
    /// </summary>
    public static void ApplyPortableRowVersion(ModelBuilder modelBuilder, string? providerName)
    {
        var isPostgres = providerName == PostgreSqlProviderName;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // DECLARED y no Find a secas: en una jerarquía TPH la propiedad la
            // declara la raíz, y FindProperty también la encuentra desde las
            // derivadas. Quitarla o ignorarla DESDE una derivada no es legal
            // («cannot be ignored on type … because it's declared on the base
            // type»), y que hoy funcionara dependería del orden en que EF
            // devuelva los tipos. Sin herencia, declarada == propia.
            var rowVersion = entityType.FindDeclaredProperty(nameof(BaseEntity.RowVersion));
            if (rowVersion is null || rowVersion.ClrType != typeof(byte[]))
                continue;

            if (!isPostgres)
            {
                rowVersion.IsConcurrencyToken = true;
                rowVersion.ValueGenerated = ValueGenerated.OnAddOrUpdate;
                rowVersion.SetColumnType("rowversion");
                rowVersion.IsNullable = false;
                continue;
            }

            // PostgreSQL: fuera la propiedad byte[] (no existe rowversion),
            // entra xmin como shadow concurrency token. AddIgnored es
            // imprescindible: sin el, la convencion de descubrimiento de EF
            // re-agrega la propiedad CLR al finalizar el modelo.
            entityType.RemoveProperty(rowVersion);
            entityType.AddIgnored(nameof(BaseEntity.RowVersion));

            if (entityType.FindProperty("xmin") is null)
            {
                var xmin = entityType.AddProperty("xmin", typeof(uint));
                xmin.SetColumnName("xmin");
                xmin.SetColumnType("xid");
                xmin.ValueGenerated = ValueGenerated.OnAddOrUpdate;
                xmin.IsConcurrencyToken = true;
            }
        }
    }

    /// <summary>
    /// Los filtros de indice se escriben en las configurations con la sintaxis
    /// canonica T-SQL ("[Col] = 0"). Para PostgreSQL se traducen aqui:
    /// corchetes → comillas dobles, y comparaciones numericas sobre columnas
    /// booleanas → TRUE/FALSE. Las configurations siguen siendo neutras.
    /// </summary>
    public static void ApplyPortableIndexFilters(ModelBuilder modelBuilder, string? providerName)
    {
        if (providerName != PostgreSqlProviderName)
            return;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var boolColumns = entityType.GetProperties()
                .Where(p => p.ClrType == typeof(bool) || p.ClrType == typeof(bool?))
                .Select(p => p.GetColumnName())
                .Where(n => n is not null)
                .ToHashSet(StringComparer.Ordinal);

            // DECLARED: en TPH las derivadas reportan también los índices
            // heredados, y se traduciría el mismo filtro varias veces. Los
            // índices declarados EN una derivada sí se visitan, porque el bucle
            // de arriba recorre todos los tipos.
            foreach (var index in entityType.GetDeclaredIndexes())
            {
                var filter = index.GetFilter();
                if (string.IsNullOrEmpty(filter))
                    continue;

                var translated = System.Text.RegularExpressions.Regex.Replace(
                    filter, @"\[(\w+)\]", "\"$1\"");

                foreach (var col in boolColumns)
                {
                    translated = translated
                        .Replace($"\"{col}\" = 1", $"\"{col}\" = TRUE", StringComparison.Ordinal)
                        .Replace($"\"{col}\" = 0", $"\"{col}\" = FALSE", StringComparison.Ordinal);
                }

                index.SetFilter(translated);
            }
        }
    }

    /// <summary>
    /// Tipos de columna especificos de SQL Server que en PostgreSQL se dejan
    /// al default del proveedor (binary/varbinary → bytea).
    /// </summary>
    public static void ApplyPortableColumnTypes(ModelBuilder modelBuilder, string? providerName)
    {
        if (providerName != PostgreSqlProviderName)
            return;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var columnType = property.GetColumnType();
                if (columnType is null)
                    continue;

                if (columnType.StartsWith("binary", StringComparison.OrdinalIgnoreCase) ||
                    columnType.StartsWith("varbinary", StringComparison.OrdinalIgnoreCase))
                {
                    property.SetColumnType(null);
                }
            }
        }
    }

    /// <summary>
    /// Todo instante persistido es UTC (convencion del proyecto). El converter
    /// normaliza en escritura (Unspecified se asume UTC; Local se convierte) y
    /// marca Kind=Utc en lectura — mismo comportamiento en ambos motores y
    /// requisito de Npgsql para <c>timestamp with time zone</c>.
    /// </summary>
    public static void ApplyUtcDateTimeConvention(ModelBuilder modelBuilder)
    {
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v
               : v.Kind == DateTimeKind.Local ? v.ToUniversalTime()
               : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => !v.HasValue ? v
               : v.Value.Kind == DateTimeKind.Utc ? v
               : v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime()
               : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc),
            v => !v.HasValue ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));

        var offsetConverter = new ValueConverter<DateTimeOffset, DateTimeOffset>(
            v => v.ToUniversalTime(),
            v => v);

        var nullableOffsetConverter = new ValueConverter<DateTimeOffset?, DateTimeOffset?>(
            v => v.HasValue ? v.Value.ToUniversalTime() : v,
            v => v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.GetValueConverter() is not null)
                    continue;

                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(dateTimeConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableDateTimeConverter);
                else if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(offsetConverter);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(nullableOffsetConverter);
            }
        }
    }
}
