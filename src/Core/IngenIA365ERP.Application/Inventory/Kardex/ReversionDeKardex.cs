using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Kardex;

/// <summary>
/// Una diferencia de costo que dejó la anulación sobre un documento afectado: las líneas <c>CostAdjustment</c> (hoy
/// <c>VoidDifference</c>) con la entrada que corrigen. Con ellas se arma un <c>AjusteDeCostoReconocido</c> por documento. (nuevo)
/// </summary>
public sealed record DiferenciaDeCostoDeAnulacion(int AffectedDocumentId, KardexReason Reason, IReadOnlyList<KardexEntry> Lineas);

/// <summary>Lo que dejó la reversión: el registro y las diferencias de costo por documento afectado. (nuevo)</summary>
public sealed record ReversionHecha(RegistroHecho Registro, IReadOnlyList<DiferenciaDeCostoDeAnulacion> Diferencias);

/// <summary>
/// La parte de kardex de toda anulación (feature 012, T252; FR-006, FR-036; data-model §3.1 «anulación»; contracts/api.md §9.5).
/// La usan las estrategias de clase desde <c>RevertirAsync</c>, que llama <c>VoidInventoryDocumentCommand</c> por el flujo
/// canónico. Por cada <see cref="KardexEntry"/> de entrada o salida del original, un movimiento que la revierte con
/// <c>ReversesEntryId</c> y <b>el mismo</b> <c>UnitCost</c>, fechado en la fecha del <c>Voiding</c>:
/// <list type="bullet">
/// <item>la reversión de una salida entra al costo con que salió (<see cref="ValoracionDelMovimiento.AlCostoDeOrigen"/>, sin
/// diferencia);</item>
/// <item>la reversión de una entrada sale al costo con que entró (<see cref="ValoracionDelMovimiento.DevolucionDeEntrada"/>): si
/// esa entrada ya entró al promedio y la diferencia contra el vigente no es cero, el motor agrega una línea
/// <c>CostAdjustment</c> <c>VoidDifference</c> con <c>AffectsEntryId</c>, y aquí se agrupa por documento afectado para el
/// mensaje <c>AjusteDeCostoReconocido</c> (<c>originEventKey = Confirmation:{affectedDocumentPublicId:N}</c>).</item>
/// </list>
/// Todo pasa por <see cref="RegistroDeKardex"/>, que sigue siendo el único escritor. Las líneas de ajuste de costo del original
/// (un residuo, una regularización) no se revierten: el valor lo reconcilia la <c>VoidDifference</c>. (nuevo)
/// </summary>
public sealed class ReversionDeKardex(IApplicationDbContext db, RegistroDeKardex registro)
{
    /// <summary>La sugerencia cuando anular una entrada ya consumida dejaría negativo (§9.5).</summary>
    public const string SugerenciaAjusteNegativo = nameof(DocumentClass.NegativeAdjustment);

    public const string SugerenciaDevolucionAProveedor = nameof(DocumentClass.SupplierReturn);

    /// <summary>Los movimientos que revierten el kardex de <paramref name="original"/> sobre las líneas de <paramref name="anulacion"/>.</summary>
    public async Task<IReadOnlyList<MovimientoDeKardex>> MovimientosAsync(InventoryDocument anulacion, InventoryDocument original, CancellationToken ct)
    {
        var filas = await db.KardexEntries
            .Where(k => k.DocumentId == original.Id && k.Kind != KardexEntryKind.CostAdjustment)
            .OrderBy(k => k.Id)
            .ToListAsync(ct);
        var numeroDeLinea = original.Lines.ToDictionary(l => l.Id, l => l.LineNumber);
        var lineas = anulacion.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.LineNumber);

        var movimientos = new List<MovimientoDeKardex>(filas.Count);
        foreach (var fila in filas)
        {
            if (!numeroDeLinea.TryGetValue(fila.DocumentLineId, out var numero) || !lineas.TryGetValue(numero, out var linea))
                throw new InvalidOperationException($"La anulación no tiene la línea {fila.DocumentLineId} del original.");
            var esEntrada = fila.Kind == KardexEntryKind.Entry;
            movimientos.Add(new MovimientoDeKardex(
                linea,
                fila.WarehouseId,
                -fila.QuantityBase,
                esEntrada ? ValoracionDelMovimiento.DevolucionDeEntrada : ValoracionDelMovimiento.AlCostoDeOrigen,
                fila.UnitCost,
                fila,
                EsAnulacion: true,
                LocationId: fila.LocationId));
        }
        return movimientos;
    }

    /// <summary>
    /// Revierte el kardex del original (dentro del cerrojo, sin guardar). Si la reversión de una entrada ya consumida no cabe,
    /// <c>Inventory.Stock.Insufficient</c> con <c>data.suggestion</c>.
    /// </summary>
    public async Task<Result<ReversionHecha>> RevertirAsync(InventoryDocument anulacion, InventoryDocument original, CancellationToken ct)
    {
        var movimientos = await MovimientosAsync(anulacion, original, ct);
        if (movimientos.Count == 0) return Result.Success(new ReversionHecha(new RegistroHecho([]), []));

        var sugerencia = original.Class is DocumentClass.PurchaseReceipt or DocumentClass.SupplierInvoice
            ? SugerenciaDevolucionAProveedor
            : SugerenciaAjusteNegativo;
        var registrado = await registro.RegistrarAsync(anulacion, movimientos, ct, sugerencia);
        if (registrado.IsFailure) return Result.Failure<ReversionHecha>(registrado.Error);

        // Cada diferencia corrige una entrada del original: el documento afectado es el anulado.
        var diferencias = registrado.Value.Lineas
            .Where(l => l.Kind == KardexEntryKind.CostAdjustment && l.Reason == KardexReason.VoidDifference)
            .ToList();
        IReadOnlyList<DiferenciaDeCostoDeAnulacion> porDocumento = diferencias.Count == 0
            ? []
            : [new DiferenciaDeCostoDeAnulacion(original.Id, KardexReason.VoidDifference, diferencias)];
        return Result.Success(new ReversionHecha(registrado.Value, porDocumento));
    }
}
