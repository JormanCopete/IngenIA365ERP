using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Analytics;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// La clase ABC de los productos de una bodega para el conteo cíclico (feature 012, I6, T930; FR-040): la clasifica
/// <see cref="ClasificacionAbc"/> —el motor único del módulo— con los umbrales de <c>Informes.UmbralesAbc</c> a la fecha. La base es la
/// decisión por defecto de T931 (a revisar con el dueño): el <b>valor al costo de las salidas</b> de la bodega en los <b>doce meses</b>
/// anteriores a la fecha de la foto (sin los ajustes de costo); los productos con existencia y sin salidas valen cero y son C. Devuelve los
/// productos de la clase pedida, que el conteo congela en su <c>CountScopeJson</c> al abrir. (nuevo)
/// </summary>
public sealed class ClasificacionAbcDelConteo(IApplicationDbContext db, ILectorDeParametros parametros)
{
    /// <summary>Los meses hacia atrás que mide la base (T931, propuesta).</summary>
    public const int MesesDeLaBase = 12;

    /// <summary>La base en palabras, para el alcance guardado del conteo.</summary>
    public static string Base(DateOnly fecha) =>
        $"Valor al costo de las salidas de la bodega del {fecha.AddMonths(-MesesDeLaBase).AddDays(1):yyyy-MM-dd} al {fecha:yyyy-MM-dd}";

    public async Task<Result<IReadOnlyList<int>>> ProductosAsync(int bodegaId, ClaseAbc clase, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.InformesUmbralesAbc, fecha, ct: ct);
        if (leido.IsFailure) return Result.Failure<IReadOnlyList<int>>(leido.Error);
        var umbrales = UmbralesAbc.Interpretar(leido.Value.Texto);
        if (!umbrales.Admitido) return Result.Failure<IReadOnlyList<int>>(new Error(umbrales.Codigo!, umbrales.Mensaje!));

        var desde = fecha.AddMonths(-MesesDeLaBase);
        var salidas = await db.KardexEntries.AsNoTracking()
            .Where(k => k.WarehouseId == bodegaId && k.Kind == KardexEntryKind.Exit && k.OperationDate > desde && k.OperationDate <= fecha)
            .GroupBy(k => k.ProductId)
            .Select(g => new { ProductId = g.Key, Valor = -g.Sum(k => k.TotalCost) })
            .ToListAsync(ct);
        var conExistencia = await db.StockDetails.AsNoTracking().Where(s => s.WarehouseId == bodegaId && s.Quantity != 0m)
            .Select(s => s.ProductId).Distinct().ToListAsync(ct);

        var valores = salidas.Select(s => new ValorParaAbc<int>(s.ProductId, s.Valor))
            .Concat(conExistencia.Where(p => salidas.All(s => s.ProductId != p)).Select(p => new ValorParaAbc<int>(p, 0m)))
            .ToList();
        var filas = ClasificacionAbc.Clasificar(valores, umbrales.Umbrales!);
        return Result.Success<IReadOnlyList<int>>(filas.Where(f => f.Clase == clase).Select(f => f.Clave).Order().ToList());
    }
}
