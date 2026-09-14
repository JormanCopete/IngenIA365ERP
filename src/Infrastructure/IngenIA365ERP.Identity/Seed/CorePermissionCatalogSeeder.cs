using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 008 — permisos de los maestros de Core (personas y asociados) para
/// <c>SEC_Permissions</c>, hermano de <see cref="DomainPermissionCatalogSeeder"/> y
/// <see cref="PayrollPermissionCatalogSeeder"/>: misma convención <c>Resource.Action</c>,
/// misma idempotencia (sólo inserta los que faltan).
///
/// <para>
/// Hasta el 2026-09-13 <c>/api/core/people</c> y <c>/api/core/associates</c> sólo exigían
/// sesión: los códigos <c>Core.People.*</c> existían únicamente en <c>IdentitySeedData</c>,
/// que nadie ejecuta, y con la acción <c>Read</c>, que no casa con el patrón <c>*.View</c> de
/// los roles built-in. La acción aquí es <c>View</c> a propósito: así Auditor, Operator y
/// ReadOnly reciben la lectura por su glob sin tocar sus patrones.
/// </para>
///
/// <para>
/// «Restaurar persona» no tiene código propio: usa <c>Core.People.Delete</c>, la misma
/// potestad que eliminar.
/// </para>
/// </summary>
public static class CorePermissionCatalogSeeder
{
    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("Core.People",     "View",   "Ver y buscar personas (maestro centralizado)"),
        ("Core.People",     "Create", "Crear personas, también desde Empleados y Asociados en un solo paso"),
        ("Core.People",     "Update", "Editar los datos personales de una persona"),
        ("Core.People",     "Delete", "Eliminar una persona (lógico) y restaurar una eliminada"),

        ("Core.Associates", "View",   "Ver asociados y su afiliación"),
        ("Core.Associates", "Create", "Registrar asociados (con persona existente o nueva)"),
        ("Core.Associates", "Update", "Editar la afiliación de un asociado"),
    ];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.Resource.StartsWith("Core."))
            .Select(p => new { p.Resource, p.Action })
            .ToListAsync();

        var existingKeys = existing
            .Select(p => $"{p.Resource}.{p.Action}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = Catalog
            .Where(p => !existingKeys.Contains($"{p.Resource}.{p.Action}"))
            .Select(p => new Permission
            {
                Resource = p.Resource,
                Action = p.Action,
                Description = p.Description,
                CreatedBy = "Seed",
                UpdatedBy = "Seed"
            })
            .ToList();

        if (toInsert.Count == 0)
        {
            logger.LogDebug("Permisos de Core ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(toInsert);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de Core sembrados: {Inserted} nuevos de {Total}.", toInsert.Count, Catalog.Length);
    }
}
