using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 012 (T124; decisiones-transversales §2.10, T48; contracts/api.md §1.1 y §24) — permisos de facturación
/// electrónica para <c>SEC_Permissions</c>, hermano de <see cref="InventoryPermissionCatalogSeeder"/>: misma
/// convención, misma idempotencia. Se siembran en I1 aunque sus rutas lleguen con I4, para que la plantilla
/// <c>inventario.administrador</c> no cambie de forma. Los artefactos (XML, respuestas, PDF) no tienen permiso propio:
/// se leen con <c>Inventory.Sales.View</c> o <c>Inventory.Purchases.View</c> (FR-068).
/// </summary>
public static class ElectronicInvoicingPermissionCatalogSeeder
{
    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("ElectronicInvoicing.Settings",      "View",                     "Ver la configuración de facturación electrónica"),
        ("ElectronicInvoicing.Settings",      "Manage",                   "Configurar la emisión electrónica, sus canales y los parámetros EINV"),
        ("ElectronicInvoicing.Resolutions",   "View",                     "Ver las resoluciones de numeración de la DIAN"),
        ("ElectronicInvoicing.Resolutions",   "Manage",                   "Registrar resoluciones de numeración y vincularlas a un canal"),
        ("ElectronicInvoicing.Documents",     "View",                     "Ver los documentos electrónicos y su estado ante la DIAN"),
        ("ElectronicInvoicing.Documents",     "Transmit",                 "Transmitir y retransmitir documentos electrónicos"),
        ("ElectronicInvoicing.Documents",     "Correct",                  "Corregir un documento rechazado"),
        ("ElectronicInvoicing.Documents",     "TransmitByCurrentChannel", "Transmitir por el canal vigente un documento emitido por otro"),
        ("ElectronicInvoicing.Contingencies", "View",                     "Ver las contingencias"),
        ("ElectronicInvoicing.Contingencies", "Declare",                  "Declarar y cerrar una contingencia"),
    ];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.Resource.StartsWith("ElectronicInvoicing."))
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
            logger.LogDebug("Permisos de facturación electrónica ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(toInsert);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de facturación electrónica sembrados: {Inserted} nuevos de {Total}.", toInsert.Count, Catalog.Length);
    }
}
