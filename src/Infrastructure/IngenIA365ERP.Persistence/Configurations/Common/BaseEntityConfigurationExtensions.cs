using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Configurations.Common;

/// <summary>
/// Conventions transversales que se aplican a todas las entidades del modelo:
///  * Concurrencia optimista portable por proveedor (feature 004, D-06):
///    SQL Server → <c>ROWVERSION</c>; PostgreSQL → <c>xmin</c>. Delegada en
///    <see cref="ProviderModelConventions.ApplyPortableRowVersion"/>.
///  * Instantes UTC normalizados en ambos motores.
///  * Filtro global de soft-delete (T022) — solo materializa filas con <c>IsDeleted = false</c>.
///
/// Llamada desde <c>OnModelCreating</c> después de aplicar las configuraciones
/// específicas, de modo que esta solo ajuste lo común.
/// </summary>
public static class BaseEntityConfigurationExtensions
{
    public static void ApplyBaseEntityConventions(this ModelBuilder modelBuilder, string? providerName)
    {
        // Feature 004 — diferencias por motor centralizadas (cubre TODAS las
        // entidades con byte[] RowVersion, incluidas las Admin).
        ProviderModelConventions.ApplyPortableRowVersion(modelBuilder, providerName);
        ProviderModelConventions.ApplyPortableIndexFilters(modelBuilder, providerName);
        ProviderModelConventions.ApplyPortableColumnTypes(modelBuilder, providerName);
        ProviderModelConventions.ApplyUtcDateTimeConvention(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (clr is null) continue;

            var isBase = typeof(BaseEntity).IsAssignableFrom(clr);
            var isBaseLong = typeof(BaseEntityLong).IsAssignableFrom(clr);
            if (!isBase && !isBaseLong) continue;

            // T022 — Filtro global de soft-delete.
            // Solo se aplica a entidades sin filtro explícito previo y que no son
            // de catálogos lookup inmutables (esos quedarán marcados con [Lookup]
            // en T099 — hasta entonces, todo lo derivado de BaseEntity está sujeto).
            if (entityType.GetQueryFilter() is null)
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(clr, "e");
                var prop = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var notDeleted = System.Linq.Expressions.Expression.Not(prop);
                var lambda = System.Linq.Expressions.Expression.Lambda(notDeleted, parameter);
                entityType.SetQueryFilter(lambda);
            }
        }
    }
}
