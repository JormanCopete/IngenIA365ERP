using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Transfers;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>TransferReceipt</c> (feature 012, US10, T367, T376; FR-039, US10-2, US10-3; contracts/api.md §11; mensajes.md
/// §6.7). La usan la recepción de <c>ReceiveTransferCommand</c> y las dos resoluciones de un faltante que son recepciones: la
/// recepción tardía al destino (<c>ReceiptOf</c>) y la devolución al origen (<c>ReturnOf</c>). (nuevo)
/// <list type="bullet">
/// <item>cada línea va enlazada a una línea de su despacho, que tiene que estar confirmado
/// (<c>Inventory.Transfer.NotInTransit</c>), con fecha no anterior a la del despacho (<c>.ReceiptBeforeDispatch</c>) y sin pasar
/// de lo que sigue en tránsito de esa línea (<c>.ReceiveExceedsDispatched</c>);</item>
/// <item>sale del tránsito y entra al destino (o al origen, si es una devolución) <b>al costo de la línea de despacho</b> (T18): el
/// valor total no cambia; un producto bloqueado se recibe igual (caso borde);</item>
/// <item>es <b>derivada</b>: no lee el modo de paso, copia el de su despacho y su mensaje <c>TrasladoRecibido</c> v1 sigue el
/// destino del de él (FR-075);</item>
/// <item>no se anula: lo recibido se corrige con un traslado contrario (<c>Inventory.Transfer.UseReverseTransfer</c>).</item>
/// </list>
/// Es <c>Scoped</c>: lo preparado se recuerda por documento dentro de la petición.
/// </summary>
public sealed class EfectoRecepcionDeTraslado(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    private readonly Dictionary<Guid, Preparada> _preparadas = [];
    private readonly Dictionary<Guid, RegistroHecho> _registradas = [];

    private sealed record Preparada(PreparacionDelRegistro Preparacion, IReadOnlyList<MovimientoDeKardex> Movimientos, InventoryDocument Despacho, decimal Monto);

    public override DocumentClass Clase => DocumentClass.TransferReceipt;

    public override async Task<IReadOnlyList<InventoryDocument>> OrigenesDelModoAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (_preparadas.TryGetValue(contexto.Documento.PublicId, out var p)) return [p.Despacho];
        var despacho = await DespachoAsync(contexto.Documento, ct);
        return despacho is null ? [] : [despacho.Value.Despacho];
    }

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return Result.Failure(ErroresDeTraslados.UseReverseTransfer());

        var documento = contexto.Documento;
        var enlace = await DespachoAsync(documento, ct);
        if (enlace is null) return Result.Failure(ErroresDeTraslados.NotInTransit(DocumentStatus.Draft));
        var (despacho, kind, pares) = enlace.Value;
        if (despacho.Class != DocumentClass.TransferDispatch || despacho.Status != DocumentStatus.Confirmed)
            return Result.Failure(ErroresDeTraslados.NotInTransit(despacho.Status));
        if (documento.OperationDate < despacho.OperationDate) return Result.Failure(ErroresDeTraslados.ReceiptBeforeDispatch(despacho.OperationDate));
        if (despacho.TransitWarehouseId is not int transito) return Result.Failure(ErroresDeTraslados.NotInTransit(despacho.Status));

        var hacia = kind == DocumentLinkKind.ReturnOf ? despacho.WarehouseId!.Value : despacho.DestinationWarehouseId!.Value;
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var porLinea = pares.ToDictionary(x => x.TargetLineId, x => x.SourceLineId);

        // Lo que ya salió del tránsito por cada línea del despacho (otras recepciones, bajas y devoluciones vigentes).
        var lineasDeDespacho = porLinea.Values.Distinct().ToList();
        var consumido = await ConsumidoAsync(lineasDeDespacho, documento.Id, ct);
        var entradasAlTransito = await db.KardexEntries.AsNoTracking()
            .Where(k => lineasDeDespacho.Contains(k.DocumentLineId) && k.DocumentId == despacho.Id && k.WarehouseId == transito && k.Kind == KardexEntryKind.Entry)
            .ToListAsync(ct);
        var ubicaciones = await ReglasDeTraslado.UbicacionesPorDefectoAsync(db, [hacia], ct);

        var movimientos = new List<MovimientoDeKardex>(vivas.Count * 2);
        var monto = 0m;
        foreach (var grupo in vivas.GroupBy(l => porLinea.GetValueOrDefault(l.Id)))
        {
            var lineaDeDespacho = despacho.Lines.FirstOrDefault(l => l.Id == grupo.Key);
            var entrada = entradasAlTransito.Where(k => k.DocumentLineId == grupo.Key).OrderBy(k => k.Id).FirstOrDefault();
            if (lineaDeDespacho is null || entrada is null) return Result.Failure(ErroresDeTraslados.NotInTransit(despacho.Status));

            var pendiente = lineaDeDespacho.QuantityBase - consumido.GetValueOrDefault(grupo.Key);
            if (grupo.Sum(l => l.QuantityBase) > pendiente)
                return Result.Failure(ErroresDeTraslados.ReceiveExceedsDispatched(grupo.First().LineNumber, pendiente));

            foreach (var linea in grupo.Where(l => l.QuantityBase > 0m))
            {
                linea.ToLocationId ??= ubicaciones.TryGetValue(hacia, out var u) ? u : null;
                var sale = new MovimientoDeKardex(linea, transito, -linea.QuantityBase, ValoracionDelMovimiento.AlCostoDeOrigen, entrada.UnitCost,
                    LocationId: entrada.LocationId);
                movimientos.Add(sale);
                movimientos.Add(new MovimientoDeKardex(linea, hacia, linea.QuantityBase, ValoracionDelMovimiento.AlCostoDeOrigen, entrada.UnitCost,
                    LocationId: linea.ToLocationId, AlCostoDe: sale));
                monto += Math.Round(linea.QuantityBase * entrada.UnitCost, 2, MidpointRounding.AwayFromZero);
            }
        }

        if (movimientos.Count == 0) return Result.Success();
        var preparado = await registro.PrepararAsync(documento, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparadas[documento.PublicId] = new Preparada(preparado.Value, movimientos, despacho, monto);
        return Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) =>
        _preparadas.TryGetValue(contexto.Documento.PublicId, out var p) ? p.Monto : base.MontoParaAprobar(contexto);

    /// <summary>Bloquea además el despacho (sin modificarlo): dos recepciones del mismo tránsito no se pasan de lo pendiente.</summary>
    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        _preparadas.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Preparacion.Cerrojo with
            {
                Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Preparacion.Cerrojo.Bodegas).Distinct().ToList(),
                DocumentosDeOrigen = [p.Despacho.Id],
            }
            : base.Cerrojo(contexto);

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (!_preparadas.TryGetValue(contexto.Documento.PublicId, out var p)) return Result.Success();
        var registrado = await registro.RegistrarAsync(contexto.Documento, p.Movimientos, ct);
        if (registrado.IsFailure) return Result.Failure(registrado.Error);
        _registradas[contexto.Documento.PublicId] = registrado.Value;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var filas = _registradas.TryGetValue(contexto.Documento.PublicId, out var hecho)
            ? hecho.Lineas.ToList()
            : await db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == contexto.Documento.Id).ToListAsync(ct);
        if (filas.Count == 0) return [];
        var despacho = _preparadas.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Despacho
            : (await DespachoAsync(contexto.Documento, ct))?.Despacho;
        if (despacho is null) return [];
        return [await emision.TrasladoRecibidoAsync(contexto.Documento, despacho, filas, ct)];
    }

    public override Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        Task.FromResult(Result.Failure(ErroresDeTraslados.UseReverseTransfer()));

    // ------------------------------------------------------------------------------------------ apoyo --

    /// <summary>
    /// El despacho del que deriva el documento, cómo se enlaza (<c>ReceiptOf</c> al destino, <c>ReturnOf</c> al origen) y los pares de
    /// líneas (en memoria primero: el borrador recién armado puede no tener Id).
    /// </summary>
    private async Task<(InventoryDocument Despacho, DocumentLinkKind Kind, IReadOnlyList<(int SourceLineId, int TargetLineId)> Pares)?> DespachoAsync(
        InventoryDocument documento, CancellationToken ct)
    {
        var vinculo = db.DocumentLinks.Local.FirstOrDefault(l => !l.IsDeleted
                          && (l.Kind == DocumentLinkKind.ReceiptOf || l.Kind == DocumentLinkKind.ReturnOf)
                          && (l.TargetDocument == documento || (documento.Id != 0 && l.TargetDocumentId == documento.Id)))
                      ?? (documento.Id == 0 ? null : await db.DocumentLinks.Include(l => l.LineLinks)
                          .FirstOrDefaultAsync(l => l.TargetDocumentId == documento.Id
                              && (l.Kind == DocumentLinkKind.ReceiptOf || l.Kind == DocumentLinkKind.ReturnOf), ct));
        if (vinculo is null) return null;
        var despacho = await db.InventoryDocuments.Include(d => d.Lines).FirstAsync(d => d.Id == vinculo.SourceDocumentId, ct);
        var pares = vinculo.LineLinks.Where(x => !x.IsDeleted)
            .Select(x => (x.SourceLineId, TargetLineId: x.TargetLine?.Id ?? x.TargetLineId)).ToList();
        return (despacho, vinculo.Kind, pares);
    }

    /// <summary>Lo que ya salió del tránsito de cada línea de despacho por otros documentos vigentes (sin contar <paramref name="excluir"/>).</summary>
    private async Task<IReadOnlyDictionary<int, decimal>> ConsumidoAsync(IReadOnlyCollection<int> lineasDeDespacho, int excluir, CancellationToken ct)
    {
        var filas = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => lineasDeDespacho.Contains(x.SourceLineId))
            .Join(db.DocumentLinks.AsNoTracking(), x => x.DocumentLinkId, l => l.Id, (x, l) => new { x.SourceLineId, x.QuantityBase, l.TargetDocumentId })
            .Where(y => y.TargetDocumentId != excluir)
            .Join(db.InventoryDocuments.AsNoTracking(), y => y.TargetDocumentId, d => d.Id, (y, d) => new { y.SourceLineId, y.QuantityBase, d.Status })
            .Where(y => Vigentes.Contains(y.Status))
            .ToListAsync(ct);
        return filas.GroupBy(f => f.SourceLineId).ToDictionary(g => g.Key, g => g.Sum(f => f.QuantityBase));
    }
}
