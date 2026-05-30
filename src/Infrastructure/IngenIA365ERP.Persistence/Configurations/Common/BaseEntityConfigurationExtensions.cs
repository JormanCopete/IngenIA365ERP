using IngenIA365ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Common;

/// <summary>
/// Conventions transversales que se aplican a todas las entidades del modelo:
///  * <c>RowVersion</c> mapeado a <c>rowversion</c>/<c>timestamp</c> para concurrencia optimista (T011).
///  * Filtro global de soft-delete (T022) — solo materializa filas con <c>IsDeleted = false</c>.
///
/// Llamada desde <c>ApplicationDbContext.OnModelCreating</c> después de
/// <c>ApplyConfigurationsFromAssembly</c>, de modo que cualquier configuración
/// específica de entidad se haya registrado antes y esta solo ajuste lo común.
/// </summary>
public static class BaseEntityConfigurationExtensions
{
    public static void ApplyBaseEntityConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            if (clr is null) continue;

            var isBase = typeof(BaseEntity).IsAssignableFrom(clr);
            var isBaseLong = typeof(BaseEntityLong).IsAssignableFrom(clr);
            if (!isBase && !isBaseLong) continue;

            // T011 — Concurrencia optimista
            var rowVersion = entityType.FindProperty(nameof(BaseEntity.RowVersion));
            if (rowVersion is not null)
            {
                rowVersion.IsConcurrencyToken = true;
                rowVersion.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate;
                rowVersion.SetColumnType("rowversion");
                rowVersion.IsNullable = false;
            }

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
