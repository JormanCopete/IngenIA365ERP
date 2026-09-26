using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Replenishment;

/// <summary>
/// El aviso de reposición al confirmar (feature 012, US17, T953; FR-035, FR-022; contracts/api.md §4.4, §9.3). Lo llama
/// <see cref="ConfirmacionDeDocumento"/> después de <c>RegistroDeKardex</c> y del guardado, dentro de la misma transacción, con el
/// documento recién confirmado: toma sólo sus <b>salidas</b> del kardex (líneas con cantidad negativa: venta, ajuste negativo,
/// consumo, baja, despacho de traslado, devolución, anulación de una entrada…) y, por cada (producto, bodega) con política de
/// reorden, evalúa la posición con <see cref="EvaluacionDeReposicion"/>:
/// <list type="bullet">
/// <item>si la posición quedó igual o menor que el punto, agrega el aviso <see cref="CodigoDelAviso"/> a <c>warnings[]</c> con
/// <c>{ productCode, warehouseCode, position, reorderPoint }</c> y levanta <c>Inventario.Reorden</c>;</item>
/// <item>si además —o sólo— el disponible quedó bajo el mínimo, levanta <c>Inventario.Quiebre</c>.</item>
/// </list>
/// Las alertas salen por <see cref="IAlertas"/> (el comando detecta la condición dentro de su propia unidad de trabajo), con la
/// misma condición que la revisión nocturna: una pendiente suma la ocurrencia. <b>Nunca bloquea</b>: una alerta que no se levanta
/// (tipo apagado) no detiene la confirmación. Una entrada no avisa nada. (nuevo)
/// </summary>
public sealed class AvisoDeReposicionAlConfirmar(IApplicationDbContext db, EvaluacionDeReposicion evaluacion, IAlertas alertas)
{
    public const string CodigoDelAviso = "Inventory.Stock.BelowReorderPoint";

    /// <summary>Los avisos para la respuesta; levanta las alertas que correspondan.</summary>
    public async Task<IReadOnlyList<AvisoDto>> AvisarAsync(InventoryDocument documento, CancellationToken ct)
    {
        var salidas = await db.KardexEntries.AsNoTracking()
            .Where(k => k.DocumentId == documento.Id && k.QuantityBase < 0m)
            .Select(k => new { k.ProductId, k.WarehouseId })
            .Distinct()
            .ToListAsync(ct);
        if (salidas.Count == 0) return [];

        var filas = await evaluacion.EvaluarAsync(salidas.Select(s => (s.ProductId, s.WarehouseId)).ToList(), ct);
        var avisos = new List<AvisoDto>();
        foreach (var fila in filas.Where(f => f.Resultado.Alerta))
        {
            if (fila.Resultado.RequiereReorden)
                avisos.Add(Aviso(fila));
            foreach (var alerta in AlertasDeReposicion.De(fila))
                await alertas.LevantarAsync(alerta, ct);
        }
        return avisos;
    }

    /// <summary>El aviso de §9.3: <c>data { productCode, warehouseCode, position, reorderPoint }</c>.</summary>
    public static AvisoDto Aviso(FilaDeReposicion f) => new(
        CodigoDelAviso,
        $"{f.ProductCode} quedó en {f.WarehouseCode} con posición {f.Resultado.Posicion:0.####}, igual o menor que su punto de reorden {f.PuntoDeReorden:0.####}.",
        new { productCode = f.ProductCode, warehouseCode = f.WarehouseCode, position = f.Resultado.Posicion, reorderPoint = f.PuntoDeReorden });
}
