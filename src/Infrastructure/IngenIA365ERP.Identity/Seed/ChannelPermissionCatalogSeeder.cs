using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 021 (contracts/erp-administracion.md): los permisos del canal digital del asociado. Los siembra la API al arrancar
/// (<see cref="PhaseZeroSecuritySeeder"/>), nunca el DbMigrator. <c>CompanyAdmin</c> los recibe por <c>*</c>; Operator, ReadOnly y
/// Auditor, sus <c>*.View</c>; Operator además habilita personas.
/// </summary>
public static class ChannelPermissionCatalogSeeder
{
    private const string Prefijo = "Channel.";

    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("Channel.Settings",     "View",    "Ver la configuración del canal digital del asociado"),
        ("Channel.Settings",     "Manage",  "Encender, apagar y configurar el canal digital, sus textos legales y sus conceptos"),
        ("Channel.Destinations", "Manage",  "Administrar los destinos de pago del canal y enviar las credenciales de la pasarela"),
        ("Channel.Enablements",  "View",    "Ver las personas habilitadas para el canal y sus novedades de contacto"),
        ("Channel.Enablements",  "Manage",  "Habilitar e inhabilitar personas para el canal, una a una o por carga masiva"),
        ("Channel.Support",      "View",    "Atender a los asociados: ver su cuenta del canal y sus intentos de pago"),
        ("Channel.Support",      "Manage",  "Desbloquear cuentas del canal y cerrar sus sesiones"),
        ("Channel.Payments",     "Reverse", "Reversar un pago hecho por el canal (con motivo)"),
        ("Channel.Metrics",      "View",    "Ver los indicadores del canal digital"),
    ];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var claves = Catalog.Select(p => $"{p.Resource}.{p.Action}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existentes = (await db.Permissions.IgnoreQueryFilters().Where(p => p.Resource.StartsWith(Prefijo)).ToListAsync())
            .Where(p => claves.Contains($"{p.Resource}.{p.Action}"))
            .ToList();

        var yaEstan = existentes.Select(p => $"{p.Resource}.{p.Action}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var descripciones = Catalog.ToDictionary(p => $"{p.Resource}.{p.Action}", p => p.Description, StringComparer.OrdinalIgnoreCase);
        var actualizadas = 0;
        foreach (var p in existentes)
        {
            if (descripciones.TryGetValue($"{p.Resource}.{p.Action}", out var descripcion) && p.Description != descripcion)
            {
                p.Description = descripcion;
                p.UpdatedBy = "Seed";
                actualizadas++;
            }
        }

        var nuevos = Catalog
            .Where(p => !yaEstan.Contains($"{p.Resource}.{p.Action}"))
            .Select(p => new Permission
            {
                Resource = p.Resource,
                Action = p.Action,
                Description = p.Description,
                CreatedBy = "Seed",
                UpdatedBy = "Seed",
            })
            .ToList();

        if (nuevos.Count == 0 && actualizadas == 0)
        {
            logger.LogDebug("Permisos del canal digital ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(nuevos);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos del canal digital: {Inserted} nuevos de {Total}, {Updated} descripciones puestas al día.",
            nuevos.Count, Catalog.Length, actualizadas);
    }
}
