using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>SupplierReturn</c> (feature 012, T343; FR-044, FR-051; contracts/api.md §14.6; mensajes.md §6.8, §6.10).
/// (nuevo)
/// <list type="bullet">
/// <item>toda línea va enlazada (<c>ReturnOf</c>) a una línea de recepción confirmada del mismo proveedor
/// (<c>Inventory.Return.ReceiptLineRequired</c>) y no pasa de lo recibido menos lo ya devuelto
/// (<c>Inventory.Return.ExceedsReceived</c>);</item>
/// <item>sale al <b>costo con que entró</b> (<see cref="ValoracionDelMovimiento.DevolucionDeEntrada"/> con el costo de la fila del
/// kardex de la recepción), aunque el promedio haya cambiado; la diferencia con el promedio vigente es
/// <c>KardexReason.VoidDifference</c> sobre esa entrada y viaja en un <c>AjusteDeCostoReconocido</c> por recepción;</item>
/// <item>sin disponible suficiente, <c>Inventory.Stock.Insufficient</c> (lo decide <see cref="RegistroDeKardex"/>);</item>
/// <item>emite <c>DevolucionRegistrada</c> v1 (<c>DevolucionAProveedor</c>, <c>Exit</c>) y copia el modo de su recepción
/// (derivado, data-model §5.3);</item>
/// <item><c>supportDocumentAdjustmentNoteRequired</c> queda siempre falso hasta I4.</item>
/// </list>
/// </summary>
public sealed class EfectoDevolucionAProveedor(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    VinculosDeCompra vinculos,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, (PreparacionDelRegistro Preparacion, IReadOnlyList<MovimientoDeKardex> Movimientos, IReadOnlyList<InventoryDocument> Recepciones)> _preparados = [];
    private readonly Dictionary<Guid, RegistroHecho> _registrados = [];
    private readonly Dictionary<Guid, ReversionHecha> _revertidos = [];

    public override DocumentClass Clase => DocumentClass.SupplierReturn;

    public override Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Error>>([]);

    public override async Task<IReadOnlyList<InventoryDocument>> OrigenesDelModoAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Recepciones.Where(r => r.Status == DocumentStatus.Confirmed).ToList()
            : (await vinculos.OrigenesAsync(contexto.Documento, DocumentLinkKind.ReturnOf, ct)).Where(r => r.Status == DocumentStatus.Confirmed).ToList();

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (contexto.EsAnulacion)
        {
            var reversa = await reversion.MovimientosAsync(documento, contexto.Original!, ct);
            if (reversa.Count == 0) return Result.Success();
            var prep = await registro.PrepararAsync(documento, reversa, ct);
            if (prep.IsFailure) return Result.Failure(prep.Error);
            _preparados[documento.PublicId] = (prep.Value, reversa, []);
            return Result.Success();
        }

        var comunes = await ReglasDeCompra.ComunesAsync(contexto, maestros, ct);
        if (comunes is not null) return Result.Failure(comunes);
        var productos = await ReglasDeCompra.ProductosDeMercanciaAsync(documento, maestros, ct);
        if (productos.Count > 0) return Result.Failure(productos[0]);
        if (documento.WarehouseId is not int bodega) return Result.Failure(InventoryErrors.FieldRequired(ReglasDelDocumento.CampoBodega));

        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var pares = await vinculos.DeAsync(documento, DocumentLinkKind.ReturnOf, ct);
        var porLinea = pares.GroupBy(p => p.TargetLineId).ToDictionary(g => g.Key, g => g.First());
        foreach (var linea in vivas)
            if (!porLinea.ContainsKey(linea.Id)) return Result.Failure(ErroresDeCompras.ReturnReceiptLineRequired(linea.LineNumber));

        var lineasDeRecepcion = pares.Select(p => p.SourceLineId).Distinct().ToList();
        var origenes = await db.InventoryDocumentLines.AsNoTracking().Where(l => lineasDeRecepcion.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var recepciones = await vinculos.OrigenesAsync(documento, DocumentLinkKind.ReturnOf, ct);
        foreach (var linea in vivas)
        {
            var origen = origenes[porLinea[linea.Id].SourceLineId];
            var recepcion = recepciones.First(r => r.Id == origen.DocumentId);
            var numero = VistaDeDocumentos.NumeroVisible(recepcion.Prefix, recepcion.Number);
            if (recepcion.Status != DocumentStatus.Confirmed) return Result.Failure(ErroresDeCompras.ReceiptNotConfirmed(linea.LineNumber, recepcion.PublicId, numero));
            if (recepcion.CounterpartyPersonId != documento.CounterpartyPersonId)
                return Result.Failure(ErroresDeCompras.ReceiptFromOtherSupplier(linea.LineNumber, recepcion.PublicId, numero));
        }

        var consumo = await vinculos.ConsumoAsync(lineasDeRecepcion, documento.Id, ct);
        foreach (var grupo in vivas.GroupBy(l => porLinea[l.Id].SourceLineId))
        {
            var origen = origenes[grupo.Key];
            var devuelto = consumo.GetValueOrDefault(grupo.Key)?.Devuelto ?? 0m;
            if (devuelto + grupo.Sum(l => l.QuantityBase) > origen.QuantityBase)
                return Result.Failure(ErroresDeCompras.ReturnExceedsReceived(grupo.First().LineNumber, origen.QuantityBase, devuelto));
        }

        // Cada línea sale al costo de la fila del kardex de su línea de recepción (la entrada que devuelve).
        var entradas = await db.KardexEntries.AsNoTracking()
            .Where(k => lineasDeRecepcion.Contains(k.DocumentLineId) && k.Kind == KardexEntryKind.Entry)
            .ToListAsync(ct);
        var movimientos = new List<MovimientoDeKardex>(vivas.Count);
        foreach (var linea in vivas.Where(l => l.QuantityBase > 0m))
        {
            var entrada = entradas.Where(k => k.DocumentLineId == porLinea[linea.Id].SourceLineId).OrderBy(k => k.Id).FirstOrDefault();
            if (entrada is null) return Result.Failure(ErroresDeCompras.ReturnReceiptLineRequired(linea.LineNumber));
            movimientos.Add(new MovimientoDeKardex(linea, bodega, -linea.QuantityBase, ValoracionDelMovimiento.DevolucionDeEntrada, entrada.UnitCost,
                entrada, LocationId: linea.LocationId));
        }

        var preparado = await registro.PrepararAsync(documento, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparados[documento.PublicId] = (preparado.Value, movimientos, recepciones);
        return Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p) ? p.Preparacion.ValorEstimado : base.MontoParaAprobar(contexto);

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Preparacion.Cerrojo with
            {
                Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Preparacion.Cerrojo.Bodegas).Distinct().ToList(),
                DocumentosDeOrigen = p.Recepciones.Select(r => r.Id).ToList(),
            }
            : base.Cerrojo(contexto);

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var (_, movimientos, _) = _preparados[contexto.Documento.PublicId];
        var registrado = await registro.RegistrarAsync(contexto.Documento, movimientos, ct);
        if (registrado.IsFailure) return Result.Failure(registrado.Error);
        _registrados[contexto.Documento.PublicId] = registrado.Value;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var filas = _registrados.TryGetValue(documento.PublicId, out var hecho)
            ? hecho.Lineas.ToList()
            : await ReglasDeCompra.KardexDelDocumentoAsync(db, documento, ct);
        if (filas.Count == 0) return [];

        var contenidos = new List<object> { await emision.DevolucionAProveedorAsync(documento, filas, ct) };
        // La diferencia contra el promedio (VoidDifference) corrige la entrada de la recepción: uno por recepción afectada.
        var diferencias = filas.Where(f => f.Kind == KardexEntryKind.CostAdjustment && f.Reason == KardexReason.VoidDifference).ToList();
        if (diferencias.Count > 0)
        {
            var entradas = diferencias.Select(d => d.AffectsEntryId).OfType<long>().Distinct().ToList();
            var documentoDeEntrada = await db.KardexEntries.AsNoTracking().Where(k => entradas.Contains(k.Id))
                .ToDictionaryAsync(k => k.Id, k => k.DocumentId, ct);
            foreach (var porRecepcion in diferencias.GroupBy(d => d.AffectsEntryId is long e && documentoDeEntrada.TryGetValue(e, out var doc) ? doc : 0))
            {
                if (porRecepcion.Key == 0) continue;
                var recepcion = await db.InventoryDocuments.AsNoTracking().FirstAsync(d => d.Id == porRecepcion.Key, ct);
                contenidos.Add(await emision.AjusteDeCostoAsync(recepcion, KardexReason.VoidDifference, documento.OperationDate, porRecepcion.ToList(), ct));
            }
        }
        return contenidos;
    }

    public override async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var revertido = await reversion.RevertirAsync(contexto.Documento, contexto.Original!, ct);
        if (revertido.IsFailure) return Result.Failure(revertido.Error);
        _revertidos[contexto.Documento.PublicId] = revertido.Value;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var diferencias = _revertidos.TryGetValue(contexto.Documento.PublicId, out var hecha) ? hecha.Diferencias : [];
        return await emision.AnulacionAsync(contexto.Documento, contexto.Original!, diferencias, ct);
    }

    /// <summary>Hasta I4 no hay documento soporte: la devolución nunca pide su nota de ajuste.</summary>
    public const bool SupportDocumentAdjustmentNoteRequired = false;
}
