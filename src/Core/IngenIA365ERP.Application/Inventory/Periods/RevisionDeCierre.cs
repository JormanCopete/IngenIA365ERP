using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Periods;

/// <summary>
/// Lo que se revisa antes de cerrar un mes (feature 012, T289; FR-047; contracts/api.md §13.4), en un solo sitio para que la
/// vista previa (<c>GetPeriodCloseCheckQuery</c>) y el cierre (<c>CloseInventoryPeriodCommand</c>) digan lo mismo. (nuevo)
/// <list type="bullet">
/// <item><b>Bloquea</b>: otro mes por cerrar antes (los meses cierran en orden, para toda la cooperativa), un mes que no
/// terminó en hora de Colombia y los conteos abiertos con foto en el mes (US11, T401: <c>PhysicalCount</c> en borrador con
/// <c>CountSnapshotAt</c>; ni cerrados ni descartados).</item>
/// <item><b>Avisa</b>: borradores y documentos en aprobación con fecha en el mes (los conteos no cuentan como borrador:
/// son del bloqueo), despachos de traslado sin recepción con fecha en o antes del fin del mes, los recibidos hasta el fin
/// del mes con faltantes o sobrantes sin resolver (<c>INV_TransferDiscrepancies</c> con <c>ResolvedAt</c> nulo, US10, T377) y
/// entregas de mensajes de Inventario pendientes, en lote o rechazadas con fecha en el mes.</item>
/// <item><b>Remisiones sin facturar</b> (I6, T890): las remisiones confirmadas con fecha en el mes que todavía tienen algo por facturar, con
/// su valor —lo pendiente al precio neto de la línea del pedido del que salió (<c>valueSource = Order</c>) o, sin pedido, al de la propia
/// remisión, que se precificó con la lista vigente (<c>PriceList</c>)—; se cierra sólo aceptándolas con permiso y motivo.</item>
/// </list>
/// </summary>
public sealed class RevisionDeCierre(IApplicationDbContext db, IDateTimeService reloj)
{
    /// <summary>El mes siguiente al último cerrado, o el del inicio si no hay ninguno.</summary>
    public static InventoryErrors.MesDeInventario SiguientePorCerrar(InventorySetup setup) =>
        InventoryErrors.MesDeInventario.De(setup.LastClosedDate is { } cerrado ? cerrado.AddDays(1) : setup.StartDate);

    /// <summary>La revisión del mes; <c>Inventory.Period.NotStarted</c> si el módulo no arrancó.</summary>
    public async Task<Result<PeriodCloseCheckDto>> RevisarAsync(int year, int month, CancellationToken ct)
    {
        var setup = await db.InventorySetups.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (setup is null) return Result.Failure<PeriodCloseCheckDto>(InventoryErrors.PeriodNotStarted());

        var inicio = new DateOnly(year, month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);

        var siguiente = SiguientePorCerrar(setup);
        var noEsElSiguiente = siguiente.Year != year || siguiente.Month != month ? siguiente : null;
        DateOnly? noTermino = fin >= reloj.HoyLocal ? fin : null;
        var bloqueos = new CloseBlockersDto(await ConteosAbiertosAsync(inicio, fin, ct), noEsElSiguiente, noTermino);

        var avisos = await AvisosAsync(inicio, fin, ct);
        var remisiones = await RemisionesSinFacturarAsync(inicio, fin, ct);
        return Result.Success(new PeriodCloseCheckDto(year, month, !bloqueos.Any, bloqueos, avisos, remisiones, remisiones.Sum(r => r.Value)));
    }

    /// <summary>
    /// I6 (T890; FR-047, FR-052; contracts/api.md §13.4): las remisiones confirmadas con fecha en el mes y saldo pendiente de facturar
    /// (<see cref="Sales.CicloComercial.VinculosDelCiclo.PendientePorFacturarAsync"/>), con su valor al precio del pedido o de la lista.
    /// </summary>
    public async Task<IReadOnlyList<UnbilledShipmentDto>> RemisionesSinFacturarAsync(DateOnly inicio, DateOnly fin, CancellationToken ct)
    {
        var remisiones = await db.InventoryDocuments.AsNoTracking().Include(d => d.Lines)
            .Where(d => d.Class == DocumentClass.Shipment && d.Status == DocumentStatus.Confirmed && d.OperationDate >= inicio && d.OperationDate <= fin)
            .OrderBy(d => d.OperationDate).ThenBy(d => d.Id).ToListAsync(ct);
        if (remisiones.Count == 0) return [];
        var vinculos = new Sales.CicloComercial.VinculosDelCiclo(db, reloj);
        var lineas = remisiones.SelectMany(r => r.Lines).Where(l => !l.IsDeleted).ToList();
        var pendiente = await vinculos.PendientePorFacturarAsync(lineas, 0, ct);
        var ids = lineas.Select(l => l.Id).ToList();
        var delPedido = (await db.DocumentLineLinks.AsNoTracking()
                .Where(x => !x.IsDeleted && ids.Contains(x.TargetLineId) && !x.DocumentLink!.IsDeleted && x.DocumentLink.Kind == DocumentLinkKind.DispatchOf)
                .Select(x => new { x.TargetLineId, x.SourceLine!.NetAmount, x.SourceLine.QuantityBase })
                .ToListAsync(ct))
            .GroupBy(x => x.TargetLineId).ToDictionary(g => g.Key, g => g.First());
        var personas = remisiones.Select(r => r.CounterpartyPersonId).OfType<int>().Distinct().ToList();
        var nombres = (await db.People.AsNoTracking().Where(p => personas.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id, Pos.BorradorDelPos.Nombre);

        var resultado = new List<UnbilledShipmentDto>();
        foreach (var r in remisiones)
        {
            var vivas = r.Lines.Where(l => !l.IsDeleted && pendiente.GetValueOrDefault(l.Id) > 0m).ToList();
            if (vivas.Count == 0) continue;
            var valor = 0m;
            var dePedido = false;
            foreach (var l in vivas)
            {
                var queda = pendiente[l.Id];
                if (delPedido.TryGetValue(l.Id, out var o) && o.QuantityBase > 0m)
                {
                    dePedido = true;
                    valor += Math.Round(o.NetAmount / o.QuantityBase * queda, 2, MidpointRounding.AwayFromZero);
                }
                else if (l.QuantityBase > 0m)
                {
                    valor += Math.Round(l.NetAmount / l.QuantityBase * queda, 2, MidpointRounding.AwayFromZero);
                }
            }
            resultado.Add(new UnbilledShipmentDto(r.PublicId, VistaDeDocumentos.NumeroVisible(r.Prefix, r.Number),
                r.CounterpartyPersonId is int p && nombres.TryGetValue(p, out var nombre) ? nombre : string.Empty, valor, dePedido ? "Order" : "PriceList"));
        }
        return resultado;
    }

    /// <summary>
    /// Los conteos abiertos con foto en el mes (US11, T401; FR-047): <c>PhysicalCount</c> en borrador con foto (<c>CountSnapshotAt</c>) y la
    /// fecha de la foto dentro del mes. Uno cerrado o descartado ya no bloquea.
    /// </summary>
    private async Task<IReadOnlyList<InventoryErrors.ConteoAbierto>> ConteosAbiertosAsync(DateOnly inicio, DateOnly fin, CancellationToken ct) =>
        await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.Class == DocumentClass.PhysicalCount && d.Status == DocumentStatus.Draft && d.CountSnapshotAt != null
                && d.OperationDate >= inicio && d.OperationDate <= fin)
            .Join(db.Warehouses.AsNoTracking().IgnoreQueryFilters(), d => d.WarehouseId, w => (int?)w.Id,
                (d, w) => new { d.Id, d.PublicId, w.Code, d.OperationDate })
            .OrderBy(x => x.OperationDate).ThenBy(x => x.Id)
            // Un conteo abierto no tiene número todavía: se numera al cerrar.
            .Select(x => new InventoryErrors.ConteoAbierto(x.PublicId, null, x.Code, x.OperationDate))
            .ToListAsync(ct);

    private async Task<CloseWarningsDto> AvisosAsync(DateOnly inicio, DateOnly fin, CancellationToken ct)
    {
        var abiertos = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.OperationDate >= inicio && d.OperationDate <= fin && d.Class != DocumentClass.PhysicalCount
                && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.PendingApproval))
            .GroupBy(d => d.Status)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(ct);

        var recibidos = db.DocumentLinks.AsNoTracking()
            .Where(l => db.InventoryDocuments.Any(r => r.Class == DocumentClass.TransferReceipt && r.Status == DocumentStatus.Confirmed
                && (r.Id == l.SourceDocumentId || r.Id == l.TargetDocumentId)))
            .Select(l => new { l.SourceDocumentId, l.TargetDocumentId });
        var despachos = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.Class == DocumentClass.TransferDispatch && d.Status == DocumentStatus.Confirmed && d.OperationDate <= fin)
            .Where(d => !recibidos.Any(l => l.SourceDocumentId == d.Id || l.TargetDocumentId == d.Id))
            .OrderBy(d => d.OperationDate).ThenBy(d => d.Id)
            .Select(d => new { d.Id, d.PublicId, d.Prefix, d.Number, Pendiente = d.Lines.Where(l => !l.IsDeleted).Sum(l => l.QuantityBase) })
            .ToListAsync(ct);

        // US10 (T377): los traslados recibidos en o antes del fin del mes con faltantes o sobrantes sin resolver.
        var conDiferencias = (await db.TransferDiscrepancies.AsNoTracking()
                .Where(x => x.ResolvedAt == null)
                .Join(db.InventoryDocuments.AsNoTracking(), x => x.ReceiptDocumentId, r => r.Id, (x, r) => new { x, r.OperationDate })
                .Where(y => y.OperationDate <= fin)
                .Join(db.InventoryDocuments.AsNoTracking(), y => y.x.DispatchDocumentId, d => d.Id,
                    (y, d) => new { d.Id, d.PublicId, d.Prefix, d.Number, d.OperationDate, y.x.QuantityBase, y.x.ResolvedQuantityBase })
                .ToListAsync(ct))
            .GroupBy(y => new { y.Id, y.PublicId, y.Prefix, y.Number, y.OperationDate })
            .Where(g => despachos.All(d => d.Id != g.Key.Id))
            .OrderBy(g => g.Key.OperationDate).ThenBy(g => g.Key.Id)
            .Select(g => new { g.Key.Id, g.Key.PublicId, g.Key.Prefix, g.Key.Number, Pendiente = g.Sum(y => y.QuantityBase - y.ResolvedQuantityBase) })
            .ToList();
        despachos = [.. despachos, .. conDiferencias];

        var entregas = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(e => e.Status == DeliveryStatus.Pending || e.Status == DeliveryStatus.InBatch || e.Status == DeliveryStatus.Rejected)
            .Where(e => db.IntegrationMessages.Any(m => m.Id == e.MessageId && m.OriginModule == EmisorDeMensajes.ModuloDeOrigen
                && m.OperationDate >= inicio && m.OperationDate <= fin))
            .GroupBy(e => e.Status)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(ct);

        return new CloseWarningsDto(
            abiertos.Where(a => a.Estado == DocumentStatus.Draft).Sum(a => a.Cantidad),
            abiertos.Where(a => a.Estado == DocumentStatus.PendingApproval).Sum(a => a.Cantidad),
            despachos.Select(d => new UnresolvedTransferDto(d.PublicId, VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number), d.Pendiente)).ToList(),
            new PendingMessagesDto(
                entregas.Where(e => e.Estado == DeliveryStatus.Pending).Sum(e => e.Cantidad),
                entregas.Where(e => e.Estado == DeliveryStatus.InBatch).Sum(e => e.Cantidad),
                entregas.Where(e => e.Estado == DeliveryStatus.Rejected).Sum(e => e.Cantidad)));
    }
}
