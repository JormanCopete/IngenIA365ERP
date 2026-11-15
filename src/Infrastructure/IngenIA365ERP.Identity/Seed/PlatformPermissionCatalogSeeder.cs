using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 023 (D6; data-model §6): permisos de la plataforma de trabajos de fondo, hermano de
/// <see cref="ElectronicInvoicingPermissionCatalogSeeder"/>. Ver los mensajes muertos lo reciben <c>CompanyAdmin</c> y los roles con
/// <c>*.View</c>; reprocesarlos, sólo <c>CompanyAdmin</c> (por su comodín). Idempotente. (nuevo)
/// </summary>
public static class PlatformPermissionCatalogSeeder
{
    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("Platform.DeadLetters", "View",      "Ver los mensajes muertos (documentos electrónicos y entregas que agotaron sus intentos)"),
        ("Platform.DeadLetters", "Reprocess", "Reprocesar mensajes muertos con motivo auditado"),
    ];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.Resource.StartsWith("Platform."))
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
            logger.LogDebug("Permisos de plataforma ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(toInsert);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de plataforma sembrados: {Inserted} nuevos de {Total}.", toInsert.Count, Catalog.Length);
    }
}
