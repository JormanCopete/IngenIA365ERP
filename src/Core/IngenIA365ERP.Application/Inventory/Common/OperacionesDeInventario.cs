using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// Lo que comparten las <b>operaciones</b> de Inventario que emiten mensajes sin documento (feature 012, T288–T290; contracts/
/// mensajes.md §4, §6.13, §7.2): la reclasificación de grupo contable, el cierre y la reapertura de un período. (nuevo)
/// </summary>
public static class OperacionesDeInventario
{
    /// <summary>
    /// La sucursal del sobre de una operación sin sucursal propia (mensajes.md §4, «la sucursal principal; sus líneas traen la
    /// suya»). Inventario no lee la configuración contable (FR-014, <c>InventarioNoConoceContabilidadNiCartera</c>): la principal
    /// es la primera sucursal registrada de <c>COR_Branches</c>. Cada línea del mensaje lleva la sucursal de su bodega.
    /// </summary>
    public static async Task<Guid> SucursalPrincipalAsync(IApplicationDbContext db, CancellationToken ct) =>
        await db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("La cooperativa no tiene sucursales: una operación de inventario necesita al menos una.");

    /// <summary>
    /// El modo de una operación de negocio sin tipo de documento (FR-069): el <b>valor general</b> de
    /// <c>Contabilidad.ModoDePaso</c> a la fecha; por lotes, con el disparador, la hora y la granularidad generales y el tipo
    /// de mensaje como raíz del horario (<see cref="ClavesDeLote.Horario"/>).
    /// </summary>
    public static async Task<ModoDeEntrega> ModoGeneralAsync(ILectorDeParametros parametros, string raiz, DateOnly fecha, CancellationToken ct)
    {
        var modo = await TextoAsync(ParametrosDeInventario.ContabilidadModoDePaso, "EnLinea");
        switch (modo)
        {
            case "PorLotes":
                var disparador = await TextoAsync(ParametrosDeInventario.ContabilidadDisparadorDeLote, "HoraDiaria");
                var granularidad = await TextoAsync(ParametrosDeInventario.ContabilidadGranularidad, "PorDocumento");
                var hora = await TextoAsync(ParametrosDeInventario.ContabilidadHoraDeLote, string.Empty);
                TimeOnly? horaDeLote = disparador == "HoraDiaria" && TimeOnly.TryParse(hora, System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : null;
                return new ModoDeEntrega.Sellado(DeliveryMode.Batch, ClavesDeLote.Horario(raiz, disparador, horaDeLote, granularidad));
            case "NoPasa":
                return new ModoDeEntrega.Sellado(DeliveryMode.NotPosted);
            default:
                return new ModoDeEntrega.Sellado(DeliveryMode.Online);
        }

        async Task<string> TextoAsync(string clave, string defecto)
        {
            var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, fecha, ct: ct);
            return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto : defecto;
        }
    }
}
