using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>
/// Las diferencias de precio de una factura o nota del proveedor contra sus recepciones (feature 012, T341, T342; FR-050; E6):
/// en I1 no se retienen, se reconocen. (nuevo)
/// <list type="bullet">
/// <item><b>factura</b>: por cada línea enlazada a una línea de recepción, lo que la factura dice que cuesta (neto más lo que va
/// al costo) menos lo que costó en la recepción, en proporción a la cantidad facturada. Precio igual, diferencia cero;</item>
/// <item><b>nota</b> con <c>affectsCost</c>: su neto más lo que va al costo, negativo si es crédito, sobre la entrada de la
/// recepción de la línea de factura que corrige.</item>
/// </list>
/// Todo se calcula desde lo guardado (líneas y fotos de impuestos): la anulación lo recalcula igual y lo reversa con el signo
/// contrario. Las filas se escriben bajo <paramref name="lineasQueRegistran"/> (las del documento, o las de su anulación).
/// </summary>
public sealed class DiferenciasDePrecioDeCompra(IApplicationDbContext db, VinculosDeCompra vinculos)
{
    /// <summary>Las de una factura; <paramref name="alCosto"/> es lo que sumó al costo cada línea de la factura (por número).</summary>
    public async Task<IReadOnlyList<DiferenciaDePrecioPedida>> DeFacturaAsync(
        InventoryDocument factura, IReadOnlyDictionary<int, decimal> alCosto, IReadOnlyList<InventoryDocumentLine> lineasQueRegistran,
        decimal signo, CancellationToken ct)
    {
        var pares = await vinculos.DeAsync(factura, DocumentLinkKind.InvoiceOfReceipt, ct);
        if (pares.Count == 0) return [];
        var lineas = factura.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        var deRecepcion = pares.Select(p => p.SourceLineId).Distinct().ToList();
        var (origenes, alCostoDeRecepcion, entradas) = await RecepcionesAsync(deRecepcion, ct);
        var registran = lineasQueRegistran.ToDictionary(l => l.LineNumber);

        var pedidas = new List<DiferenciaDePrecioPedida>();
        foreach (var (origenId, destinoId, _, _) in pares)
        {
            if (!lineas.TryGetValue(destinoId, out var linea) || !origenes.TryGetValue(origenId, out var origen)) continue;
            if (!entradas.TryGetValue(origenId, out var entrada) || origen.QuantityBase <= 0m) continue;
            var costoDeRecepcion = origen.NetAmount + alCostoDeRecepcion.GetValueOrDefault(origenId);
            var esperado = Math.Round(costoDeRecepcion * linea.QuantityBase / origen.QuantityBase, 2, MidpointRounding.AwayFromZero);
            var facturado = linea.NetAmount + alCosto.GetValueOrDefault(linea.LineNumber);
            var diferencia = signo * (facturado - esperado);
            if (diferencia == 0m || !registran.TryGetValue(linea.LineNumber, out var registra)) continue;
            pedidas.Add(new DiferenciaDePrecioPedida(registra, entrada, linea.QuantityBase, diferencia));
        }
        return pedidas;
    }

    /// <summary>Las de una nota: sólo sus líneas con <c>affectsCost</c>; la crédito resta y la débito suma.</summary>
    public async Task<IReadOnlyList<DiferenciaDePrecioPedida>> DeNotaAsync(
        InventoryDocument nota, bool debito, IReadOnlyDictionary<int, decimal> alCosto, IReadOnlyList<InventoryDocumentLine> lineasQueRegistran,
        decimal signo, CancellationToken ct)
    {
        var conCosto = nota.Lines.Where(l => !l.IsDeleted && l.AffectsCost).ToDictionary(l => l.Id);
        if (conCosto.Count == 0) return [];
        var pares = (await vinculos.DeAsync(nota, DocumentLinkKind.NoteOf, ct)).Where(p => conCosto.ContainsKey(p.TargetLineId)).ToList();
        var lineasDeFactura = pares.Select(p => p.SourceLineId).Distinct().ToList();
        var deRecepcion = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => lineasDeFactura.Contains(x.TargetLineId))
            .Join(db.DocumentLinks.AsNoTracking(), x => x.DocumentLinkId, l => l.Id, (x, l) => new { x.SourceLineId, x.TargetLineId, l.Kind })
            .Where(y => y.Kind == DocumentLinkKind.InvoiceOfReceipt)
            .ToDictionaryAsync(y => y.TargetLineId, y => y.SourceLineId, ct);
        var (_, _, entradas) = await RecepcionesAsync(deRecepcion.Values.Distinct().ToList(), ct);
        var registran = lineasQueRegistran.ToDictionary(l => l.LineNumber);

        var pedidas = new List<DiferenciaDePrecioPedida>();
        foreach (var (lineaDeFactura, lineaDeNota, _, _) in pares)
        {
            var linea = conCosto[lineaDeNota];
            if (!deRecepcion.TryGetValue(lineaDeFactura, out var lineaDeRecepcion) || !entradas.TryGetValue(lineaDeRecepcion, out var entrada)) continue;
            var valor = linea.NetAmount + alCosto.GetValueOrDefault(linea.LineNumber);
            var diferencia = signo * (debito ? valor : -valor);
            if (diferencia == 0m || !registran.TryGetValue(linea.LineNumber, out var registra)) continue;
            pedidas.Add(new DiferenciaDePrecioPedida(registra, entrada, Math.Max(linea.QuantityBase, 1m), diferencia));
        }
        return pedidas;
    }

    /// <summary>Lo que sumó al costo cada línea de un documento ya confirmado, por número de línea (de su foto).</summary>
    public async Task<IReadOnlyDictionary<int, decimal>> AlCostoGuardadoAsync(InventoryDocument documento, CancellationToken ct)
    {
        var numeros = documento.Lines.ToDictionary(l => l.Id, l => l.LineNumber);
        var foto = await db.DocumentTaxLines.AsNoTracking()
            .Where(t => t.DocumentId == documento.Id && t.Treatment == TaxTreatment.AddedToCost && t.DocumentLineId != null)
            .Select(t => new { t.DocumentLineId, t.Amount }).ToListAsync(ct);
        return foto.Where(t => numeros.ContainsKey(t.DocumentLineId!.Value))
            .GroupBy(t => numeros[t.DocumentLineId!.Value]).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    }

    private async Task<(Dictionary<int, InventoryDocumentLine> Lineas, Dictionary<int, decimal> AlCosto, Dictionary<int, Domain.Entities.Inventory.Transactions.KardexEntry> Entradas)>
        RecepcionesAsync(IReadOnlyCollection<int> lineasDeRecepcion, CancellationToken ct)
    {
        var lineas = await db.InventoryDocumentLines.AsNoTracking().Where(l => lineasDeRecepcion.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var alCosto = (await db.DocumentTaxLines.AsNoTracking()
                .Where(t => t.DocumentLineId != null && lineasDeRecepcion.Contains(t.DocumentLineId.Value) && t.Treatment == TaxTreatment.AddedToCost)
                .Select(t => new { t.DocumentLineId, t.Amount }).ToListAsync(ct))
            .GroupBy(t => t.DocumentLineId!.Value).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        var entradas = (await db.KardexEntries.AsNoTracking()
                .Where(k => lineasDeRecepcion.Contains(k.DocumentLineId) && k.Kind == KardexEntryKind.Entry)
                .OrderBy(k => k.Id).ToListAsync(ct))
            .GroupBy(k => k.DocumentLineId).ToDictionary(g => g.Key, g => g.First());
        return (lineas, alCosto, entradas);
    }
}
