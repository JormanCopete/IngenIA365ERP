using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Los permisos de Transporte (feature 020, R19; FR-053). Los siembra la API al arrancar, como los de Comercial y Cartera, en toda
/// cooperativa: con el módulo apagado no llegan a la sesión (<c>RecorteDePermisosPorModulo</c>). CompanyAdmin los recibe por
/// <c>*</c>; Operator, sus lecturas y la operación del día (crear, editar, activar e inactivar, documentos, conductores,
/// suscripciones); ReadOnly y Auditor, sus lecturas. Retirar, cambiar propietario, importar, facturar, configurar servicios y
/// conectores y gestionar envíos son del administrador. Los de conectores (<c>Core.Connectors.*</c>) van en el catálogo de Core.
/// </summary>
public static class TransportPermissionCatalogSeeder
{
    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("Transport.Vehicles",      "View",         "Ver y buscar vehículos, su propietario, su historia y sus equipos"),
        ("Transport.Vehicles",      "Export",       "Exportar los informes de Transporte"),
        ("Transport.Vehicles",      "Create",       "Registrar vehículos, también con propietario nuevo en un solo paso"),
        ("Transport.Vehicles",      "Update",       "Editar la ficha, la vinculación y los equipos de un vehículo"),
        ("Transport.Vehicles",      "ChangeStatus", "Activar e inactivar (suspender) un vehículo"),
        ("Transport.Vehicles",      "ChangeOwner",  "Cambiar el propietario de un vehículo y ver las deudas del saliente"),
        ("Transport.Vehicles",      "Retire",       "Retirar (desvincular) un vehículo"),
        ("Transport.Vehicles",      "Import",       "Cargar y actualizar vehículos y cambios en lote con plantilla"),
        ("Transport.Documents",     "View",         "Ver los documentos de los vehículos y el tablero de vencimientos"),
        ("Transport.Documents",     "Manage",       "Registrar, renovar y corregir documentos de los vehículos, también en lote"),
        ("Transport.Drivers",       "View",         "Ver los conductores y sus asignaciones"),
        ("Transport.Drivers",       "Manage",       "Registrar conductores, asignarlos a vehículos y terminar asignaciones"),
        ("Transport.Catalogs",      "View",         "Ver los catálogos de Transporte"),
        ("Transport.Catalogs",      "Manage",       "Administrar los catálogos y los tipos de documento de Transporte"),
        ("Transport.Services",      "View",         "Ver los servicios por vehículo y las suscripciones"),
        ("Transport.Services",      "Manage",       "Suscribir vehículos a servicios y pactar su precio, también en lote"),
        ("Transport.ServiceSetup",  "Manage",       "Configurar los servicios: producto, prioridad, línea de Cartera, medios de pago, conector y políticas"),
        ("Transport.Billing",       "View",         "Ver la facturación de servicios, sus corridas y sus cargos"),
        ("Transport.Billing",       "Run",          "Facturar servicios del período, en lote o por vehículo"),
        ("Transport.Notifications", "View",         "Ver el tablero de envíos a los sistemas externos"),
        ("Transport.Notifications", "Manage",       "Reintentar y cerrar a mano los envíos; recibir sus avisos"),
    ];

    /// <summary>Lo que hace el operador además de sus lecturas (R19).</summary>
    internal static readonly string[] DelOperador =
    [
        "Transport.Vehicles.Create", "Transport.Vehicles.Update", "Transport.Vehicles.ChangeStatus",
        "Transport.Documents.Manage", "Transport.Drivers.Manage", "Transport.Services.Manage",
    ];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.Resource.StartsWith("Transport."))
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
            logger.LogDebug("Permisos de Transporte ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(toInsert);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de Transporte sembrados: {Inserted} nuevos de {Total}.", toInsert.Count, Catalog.Length);
    }
}
