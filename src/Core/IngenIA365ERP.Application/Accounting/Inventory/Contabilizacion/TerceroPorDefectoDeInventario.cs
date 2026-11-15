using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Personas;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;

/// <summary>
/// La persona que lleva una línea de Inventario cuando la cuenta exige tercero y la operación no trae ninguno: ajustes, consumo, bajas,
/// ensambles, traslados, ajustes de costo y reclasificaciones (implantación de COOFLOPAL, pedido del dueño del 2026-11-07). Sale del
/// número de documento de <see cref="Clave"/>, en Parámetros del sistema; vacío, sin persona o con dos personas de ese número no hay
/// tercero por defecto y el comprobante falla como siempre. Nunca reemplaza al proveedor, al cliente ni al cajero.
/// </summary>
public static class TerceroPorDefectoDeInventario
{
    public const string Clave = "Accounting.DefaultThirdPartyDocument";

    public static readonly IReadOnlySet<string> OperacionesSinTercero = new HashSet<string>(StringComparer.Ordinal)
    {
        OperacionesDeInventario.AjustePositivo, OperacionesDeInventario.AjusteNegativo, OperacionesDeInventario.ConsumoInterno,
        OperacionesDeInventario.Baja, OperacionesDeInventario.Ensamble, OperacionesDeInventario.DespachoTraslado,
        OperacionesDeInventario.RecepcionTraslado, OperacionesDeInventario.AjusteDeCosto, OperacionesDeInventario.Reclasificacion,
    };

    public static async Task<int?> PersonaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var numero = await db.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey == Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(numero)) return null;
        var personas = await ResolutorDePersonaPorDocumento.Filtrar(db.People.AsNoTracking(), null, numero)
            .Select(p => p.Id).Take(2).ToListAsync(ct);
        return personas.Count == 1 ? personas[0] : null;
    }
}
