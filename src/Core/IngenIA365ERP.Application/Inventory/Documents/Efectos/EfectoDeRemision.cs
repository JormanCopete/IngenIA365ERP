using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Application.Inventory.Sales.Reservas;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// <c>Shipment</c>, la remisión (feature 012, I6, T880; FR-036, FR-044, FR-052, FR-075; data-model §14; mensajes.md §6.2):
/// <list type="bullet">
/// <item>antes de la aprobación: productos activos y no bloqueados; los pedidos de origen (<c>DispatchOf</c>) confirmados, y ninguna línea pasa
/// de lo pendiente de su pedido (<c>Inventory.Order.ExceedsPending</c>); prepara la salida;</item>
/// <item>bloquea las filas del kardex de la salida y los pedidos de origen (dos remisiones del mismo pedido no se pasan);</item>
/// <item>dentro del cerrojo: primero consume la reserva de los pedidos (<see cref="ReservasDeInventario.ConsumirAsync"/>) y después sale al
/// <b>promedio</b> por <see cref="RegistroDeKardex"/>, que deja en la línea el <c>UnitCost</c> con que salió (el que usa su anulación);</item>
/// <item>cadena <c>Sales</c>: sella el modo de paso vigente y emite <c>CostoDeVentaReconocido</c> (sin base ni impuestos: se reconoce al
/// despachar); la factura desde remisiones no vuelve a descargar;</item>
/// <item>su anulación —sólo si no se facturó; facturada, el ciclo común responde <c>Inventory.Document.HasDependents</c>— entra al costo con que
/// salió y emite <c>DocumentoAnulado</c> (<see cref="AnulacionDeVenta"/>). No vuelve a reservar: el pedido recupera lo pendiente por despachar.</item>
/// </list>
/// Lo pendiente de facturar se calcula con <c>INV_DocumentLineLinks</c> (<see cref="VinculosDelCiclo.PendientePorFacturarAsync"/>). Scoped. (nuevo)
/// </summary>
public sealed class EfectoDeRemision(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    AnulacionDeVenta anulacion,
    ReservasDeInventario reservas,
    VinculosDelCiclo vinculos,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, (PreparacionDelRegistro Preparacion, IReadOnlyList<MovimientoDeKardex> Movimientos)> _preparados = [];
    private readonly Dictionary<Guid, RegistroHecho> _registrados = [];
    private readonly Dictionary<Guid, (IReadOnlyList<ConsumoDeReserva> Consumos, IReadOnlyList<int> Pedidos)> _deLosPedidos = [];

    public override DocumentClass Clase => DocumentClass.Shipment;

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return await anulacion.ValidarAsync(contexto, ct);
        var documento = contexto.Documento;
        var productos = await ReglasDeLineasDeVenta.ProductosAsync(documento, maestros, ct);
        if (productos.IsFailure) return Result.Failure(productos.Error);

        var pares = await vinculos.ParesAsync(documento, DocumentLinkKind.DispatchOf, ct);
        if (pares.Count > 0)
        {
            var pedidos = await vinculos.OrigenesAsync(documento, DocumentLinkKind.DispatchOf, ct);
            if (pedidos.Any(p => p.Class != DocumentClass.SalesOrder || p.Status != DocumentStatus.Confirmed))
                return Result.Failure(ErroresDelCicloComercial.OriginInvalid(DocumentClass.Shipment, DocumentClass.SalesOrder));
            var lineas = pedidos.SelectMany(p => p.Lines).Where(l => pares.Any(x => x.SourceLineId == l.Id)).ToList();
            var pendiente = await vinculos.PendientePorDespacharAsync(lineas, documento.Id, ct);
            var excedidas = pares.GroupBy(p => p.SourceLineId)
                .Where(g => g.Sum(p => p.Destino.QuantityBase) > pendiente.GetValueOrDefault(g.Key))
                .Select(g => new ErroresDelCicloComercial.PendienteDePedido(lineas.First(l => l.Id == g.Key).PublicId, pendiente.GetValueOrDefault(g.Key),
                    g.Sum(p => p.Destino.QuantityBase)))
                .ToList();
            if (excedidas.Count > 0) return Result.Failure(ErroresDelCicloComercial.OrderExceedsPending(excedidas));
            _deLosPedidos[documento.PublicId] = (pares.Select(p => new ConsumoDeReserva(p.SourceLineId, p.Destino.QuantityBase)).ToList(),
                pedidos.Select(p => p.Id).ToList());
        }

        var movimientos = Movimientos(documento, productos.Value);
        if (movimientos.Count == 0) return Result.Success();
        var preparado = await registro.PrepararAsync(documento, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparados[documento.PublicId] = (preparado.Value, movimientos);
        return Result.Success();
    }

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto)
    {
        if (contexto.EsAnulacion) return anulacion.Cerrojo(contexto, base.Cerrojo(contexto));
        var pedido = _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Preparacion.Cerrojo with { Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Preparacion.Cerrojo.Bodegas).Distinct().ToList() }
            : base.Cerrojo(contexto);
        return _deLosPedidos.TryGetValue(contexto.Documento.PublicId, out var d)
            ? pedido with { DocumentosDeOrigen = pedido.DocumentosDeOrigen.Concat(d.Pedidos).Distinct().ToList() }
            : pedido;
    }

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (_deLosPedidos.TryGetValue(documento.PublicId, out var delPedido)) await reservas.ConsumirAsync(documento, delPedido.Consumos, ct);
        if (!_preparados.TryGetValue(documento.PublicId, out var p)) return Result.Success();
        var registrado = await registro.RegistrarAsync(documento, p.Movimientos, ct);
        if (registrado.IsFailure) return Result.Failure(registrado.Error);
        _registrados[documento.PublicId] = registrado.Value;
        documento.CostTotal = registrado.Value.Lineas.Sum(k => -k.TotalCost);
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var filas = _registrados.TryGetValue(documento.PublicId, out var hecho) ? hecho.Lineas.ToList() : await KardexAsync(documento, ct);
        return filas.Count == 0 ? [] : [await emision.CostoDeVentaAsync(documento, filas, ct)];
    }

    /// <summary>T520: el costo de venta con el promedio leído sin bloqueo, para la validación previa contable.</summary>
    public override async Task<IReadOnlyList<object>> MensajesProvisionalesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return await MensajesDeAnulacionAsync(contexto, ct);
        var documento = contexto.Documento;
        if (documento.WarehouseId is not int bodega || !_preparados.TryGetValue(documento.PublicId, out var p)) return [];
        var lineas = p.Movimientos.Select(m => m.Linea.Id).ToHashSet();
        var filas = (await registro.FilasProvisionalesAsync(documento, bodega, KardexEntryKind.Exit, null, ct)).Where(f => lineas.Contains(f.DocumentLineId)).ToList();
        return filas.Count == 0 ? [] : [await emision.CostoDeVentaAsync(documento, filas, ct)];
    }

    public override Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct) => anulacion.RevertirAsync(contexto, ct);

    public override Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct) => anulacion.MensajesAsync(contexto, ct);

    /// <summary>Una salida por línea viva de producto inventariable, al promedio vigente, en la bodega de la remisión.</summary>
    private static IReadOnlyList<MovimientoDeKardex> Movimientos(InventoryDocument documento, IReadOnlySet<int> inventariables)
    {
        if (documento.WarehouseId is not int bodega) return [];
        return documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m && inventariables.Contains(l.ProductId)).OrderBy(l => l.LineNumber)
            .Select(l => new MovimientoDeKardex(l, bodega, -l.QuantityBase, ValoracionDelMovimiento.AlCostoVigente, LocationId: l.LocationId))
            .ToList();
    }

    private async Task<List<KardexEntry>> KardexAsync(InventoryDocument documento, CancellationToken ct)
    {
        var enMemoria = db.KardexEntries.Local.Where(k => k.DocumentId == documento.Id).ToList();
        return enMemoria.Count > 0 ? enMemoria : await db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == documento.Id).ToListAsync(ct);
    }
}
