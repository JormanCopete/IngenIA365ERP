using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Los permisos del cotejo con cifras de referencia de la implantación (2026-11-28): uno por área, porque cada cotejo muestra los saldos de
/// su módulo (y el de Cartera, Captaciones y Asociados, datos personales). Los siembra la API al arrancar, como los demás catálogos; CompanyAdmin
/// los recibe por <c>*</c> y ningún otro rol integrado los trae: se asignan a quien hace la implantación. (nuevo)
/// </summary>
public static class ImplantacionPermissionCatalogSeeder
{
    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("Implantacion.Cotejo", "Contabilidad", "Cotejar el balance por cuenta y los saldos por tercero contra un archivo de cifras de referencia"),
        ("Implantacion.Cotejo", "Cartera",      "Cotejar la cartera crédito por crédito contra un archivo de cifras de referencia (con datos del deudor)"),
        ("Implantacion.Cotejo", "Captaciones",  "Cotejar aportes y ahorros cuenta por cuenta contra un archivo de cifras de referencia (con datos del titular)"),
        ("Implantacion.Cotejo", "Asociados",    "Cotejar los asociados y su estado contra un archivo de cifras de referencia"),
    ];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var existentes = (await db.Permissions.IgnoreQueryFilters()
                .Where(p => p.Resource.StartsWith("Implantacion."))
                .Select(p => new { p.Resource, p.Action })
                .ToListAsync())
            .Select(p => $"{p.Resource}.{p.Action}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nuevos = Catalog
            .Where(p => !existentes.Contains($"{p.Resource}.{p.Action}"))
            .Select(p => new Permission { Resource = p.Resource, Action = p.Action, Description = p.Description, CreatedBy = "Seed", UpdatedBy = "Seed" })
            .ToList();
        if (nuevos.Count == 0)
        {
            logger.LogDebug("Permisos de implantación ya presentes ({Count}).", Catalog.Length);
            return;
        }
        db.Permissions.AddRange(nuevos);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de implantación sembrados: {Inserted} nuevos de {Total}.", nuevos.Count, Catalog.Length);
    }
}
