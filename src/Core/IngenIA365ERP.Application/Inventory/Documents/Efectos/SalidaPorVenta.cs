using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Application.Inventory.Sales.Reservas;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// Las ventas que sacan mercancía (feature 012, I3, T610; FR-036, FR-044; mensajes.md §6.1, §6.2): <c>SalesInvoice</c>,
/// <c>PosEquivalentDocument</c> y <c>NonElectronicSalesReceipt</c>, una estrategia por clase sobre esta familia (T17).
/// <list type="bullet">
/// <item>antes de la aprobación: productos activos y no bloqueados; las reglas de venta de <see cref="ReglasDeConfirmacionDeVenta"/>
/// (veredicto fiscal, descuentos aprobados, vendedor, persona inactiva, impuestos, pagos); prepara la salida;</item>
/// <item>el monto que se aprueba es el <c>Total</c> de la venta;</item>
/// <item>dentro del cerrojo: la salida al <b>promedio</b> por <see cref="RegistroDeKardex"/> de cada línea de producto inventariable (los
/// servicios no mueven existencia), que deja el <c>UnitCost</c> en la línea; después la foto de impuestos, los bonos y el toque de las
/// sesiones de los pagos;</item>
/// <item>emite <c>VentaFacturada</c> (sin costo) y <c>CostoDeVentaReconocido</c> (sólo si algo salió) en el mismo evento (T11);</item>
/// <item>su anulación —sólo el comprobante no electrónico— la resuelve <see cref="AnulacionDeVenta"/>.</item>
/// </list>
/// Scoped: recuerda por documento lo preparado y lo registrado dentro de la petición. (nuevo)
/// </summary>
public abstract class SalidaPorVenta(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion,
    IApplicationDbContext db,
    ReservasDeInventario? reservas = null,
    VinculosDelCiclo? vinculos = null) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, (PreparacionDelRegistro Preparacion, IReadOnlyList<MovimientoDeKardex> Movimientos)> _preparados = [];
    private readonly Dictionary<Guid, RegistroHecho> _registrados = [];
    private readonly Dictionary<Guid, (IReadOnlyList<ConsumoDeReserva> Consumos, IReadOnlyList<int> Pedidos)> _deLosPedidos = [];

    /// <summary>
    /// ¿La clase descarga existencia? Sí, salvo la factura desde remisiones (I6, T881): lo que factura ya salió por la remisión. (nuevo)
    /// </summary>
    protected virtual bool DescargaExistencia => true;

    /// <summary>Las reglas propias de la clase antes de las de venta (I6: las remisiones de la factura). Por defecto, ninguna. (nuevo)</summary>
    protected virtual Task<Result> ValidarOrigenesAsync(ContextoDeEfecto contexto, CancellationToken ct) => Task.FromResult(Result.Success());

    /// <summary>Los documentos de origen que bloquea la confirmación (I6: los pedidos o las remisiones). (nuevo)</summary>
    protected virtual IReadOnlyList<int> OrigenesQueBloquea(ContextoDeEfecto contexto) =>
        _deLosPedidos.TryGetValue(contexto.Documento.PublicId, out var p) ? p.Pedidos : [];

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return await anulacion.ValidarAsync(contexto, ct);

        var productos = await ReglasDeLineasDeVenta.ProductosAsync(contexto.Documento, maestros, ct);
        if (productos.IsFailure) return Result.Failure(productos.Error);
        var origenes = await ValidarOrigenesAsync(contexto, ct);
        if (origenes.IsFailure) return origenes;
        var reglasDeVenta = await reglas.ValidarVentaAsync(contexto, ct);
        if (reglasDeVenta.IsFailure) return reglasDeVenta;

        // I6 (T882): la factura o el comprobante desde un pedido (FromOrder) no pasa de lo pendiente del pedido y consume su reserva.
        var delPedido = await DelPedidoAsync(contexto.Documento, ct);
        if (delPedido.IsFailure) return delPedido;

        // I4 (T739): la factura que reemplaza al documento equivalente no escribe kardex: la salida ya la hizo el original (ReplacementOf).
        var movimientos = !DescargaExistencia || reglas.EsFacturaEnLugarDelDocumentoEquivalente(contexto.Documento) ? [] : Movimientos(contexto.Documento, productos.Value);
        if (movimientos.Count == 0) return Result.Success();
        var preparado = await registro.PrepararAsync(contexto.Documento, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparados[contexto.Documento.PublicId] = (preparado.Value, movimientos);
        return Result.Success();
    }

    /// <summary>Los consumos de reserva de los pedidos de origen (<c>FromOrder</c>), sin pasar de lo pendiente de cada línea.</summary>
    private async Task<Result> DelPedidoAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (vinculos is null || !DescargaExistencia) return Result.Success();
        var pares = await vinculos.ParesAsync(documento, DocumentLinkKind.FromOrder, ct);
        if (pares.Count == 0) return Result.Success();
        var pedidos = await vinculos.OrigenesAsync(documento, DocumentLinkKind.FromOrder, ct);
        var lineas = pedidos.SelectMany(p => p.Lines).Where(l => pares.Any(x => x.SourceLineId == l.Id)).ToList();
        var pendiente = await vinculos.PendientePorDespacharAsync(lineas, documento.Id, ct);
        var excedidas = pares.GroupBy(p => p.SourceLineId)
            .Where(g => g.Sum(p => p.Destino.QuantityBase) > pendiente.GetValueOrDefault(g.Key))
            .Select(g => new ErroresDelCicloComercial.PendienteDePedido(lineas.First(l => l.Id == g.Key).PublicId, pendiente.GetValueOrDefault(g.Key),
                g.Sum(p => p.Destino.QuantityBase)))
            .ToList();
        if (excedidas.Count > 0) return Result.Failure(ErroresDelCicloComercial.OrderExceedsPending(excedidas));
        _deLosPedidos[documento.PublicId] = (pares.Select(p => new ConsumoDeReserva(p.SourceLineId, p.Destino.QuantityBase)).ToList(), pedidos.Select(p => p.Id).ToList());
        return Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    /// <summary>I3 (T655): la venta con pago de crédito pide la aprobación del crédito provisional.</summary>
    public override Task<Result<Domain.Entities.Approvals.ApprovalRequest?>> AprobacionPropiaAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        reglas.AprobacionDeCreditoAsync(contexto, ct);

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto)
    {
        if (contexto.EsAnulacion) return anulacion.Cerrojo(contexto, base.Cerrojo(contexto));
        var pedido = _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Preparacion.Cerrojo with { Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Preparacion.Cerrojo.Bodegas).Distinct().ToList() }
            : base.Cerrojo(contexto);
        var origenes = OrigenesQueBloquea(contexto);
        return origenes.Count == 0 ? pedido : pedido with { DocumentosDeOrigen = pedido.DocumentosDeOrigen.Concat(origenes).Distinct().ToList() };
    }

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        // I6 (T882): la reserva del pedido se consume antes de la salida, en la misma transacción: lo que sale ya no está reservado.
        if (reservas is not null && _deLosPedidos.TryGetValue(contexto.Documento.PublicId, out var delPedido))
            await reservas.ConsumirAsync(contexto.Documento, delPedido.Consumos, ct);
        if (_preparados.TryGetValue(contexto.Documento.PublicId, out var p))
        {
            var registrado = await registro.RegistrarAsync(contexto.Documento, p.Movimientos, ct);
            if (registrado.IsFailure) return Result.Failure(registrado.Error);
            _registrados[contexto.Documento.PublicId] = registrado.Value;
            contexto.Documento.CostTotal = registrado.Value.Lineas.Sum(k => -k.TotalCost);
        }
        return await reglas.AlConfirmarVentaAsync(contexto, ct);
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var filas = _registrados.TryGetValue(documento.PublicId, out var hecho)
            ? hecho.Lineas.ToList()
            : await KardexAsync(documento, ct);
        return await ContenidosAsync(documento, filas, ct);
    }

    /// <summary>T520: la venta con los costos provisionales (el promedio leído sin bloqueo) para la validación previa contable.</summary>
    public override async Task<IReadOnlyList<object>> MensajesProvisionalesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return await MensajesDeAnulacionAsync(contexto, ct);
        var documento = contexto.Documento;
        IReadOnlyList<KardexEntry> filas = [];
        if (documento.WarehouseId is int bodega && _preparados.TryGetValue(documento.PublicId, out var p))
        {
            var lineas = p.Movimientos.Select(m => m.Linea.Id).ToHashSet();
            filas = (await registro.FilasProvisionalesAsync(documento, bodega, KardexEntryKind.Exit, null, ct)).Where(f => lineas.Contains(f.DocumentLineId)).ToList();
        }
        return await ContenidosAsync(documento, filas, ct);
    }

    public override Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct) => anulacion.RevertirAsync(contexto, ct);

    public override Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct) => anulacion.MensajesAsync(contexto, ct);

    private async Task<IReadOnlyList<object>> ContenidosAsync(InventoryDocument documento, IReadOnlyList<KardexEntry> filas, CancellationToken ct)
    {
        var impuestos = await reglas.ImpuestosDeAsync(documento, ct);
        var pagos = await reglas.PagosDeAsync(documento, PaymentDirection.Received, ct);
        var contenidos = new List<object> { await emision.VentaFacturadaAsync(documento, impuestos, pagos, ct) };
        if (filas.Count > 0) contenidos.Add(await emision.CostoDeVentaAsync(documento, filas, ct));
        // I3 (T656): una VentaACreditoRegistrada por pago de crédito, hacia Cartera (cada una su propio evento).
        // I4 (T724): el reemplazo del caso b ajusta el crédito del rechazado (Replacement) en vez de registrar uno nuevo.
        contenidos.AddRange(await emision.CreditoDeLaVentaAsync(documento, pagos, ct));
        return contenidos;
    }

    /// <summary>Una salida por línea viva de producto inventariable, al promedio vigente, en la bodega de la venta.</summary>
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

/// <summary><c>SalesInvoice</c>: la factura electrónica de venta; confirma sólo si la guardia fiscal responde <c>Electronic</c> (I4). (nuevo)</summary>
public sealed class EfectoFacturaDeVenta(
    RegistroDeKardex registro, EmisionDeInventario emision, IMaestrosDelDocumento maestros, ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion, IApplicationDbContext db, ReservasDeInventario? reservas = null, VinculosDelCiclo? vinculos = null)
    : SalidaPorVenta(registro, emision, maestros, reglas, anulacion, db, reservas, vinculos)
{
    public override DocumentClass Clase => DocumentClass.SalesInvoice;
}

/// <summary><c>PosEquivalentDocument</c>: el documento equivalente electrónico del POS; confirma con I4. (nuevo)</summary>
public sealed class EfectoDocumentoEquivalentePos(
    RegistroDeKardex registro, EmisionDeInventario emision, IMaestrosDelDocumento maestros, ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion, IApplicationDbContext db, ReservasDeInventario? reservas = null, VinculosDelCiclo? vinculos = null)
    : SalidaPorVenta(registro, emision, maestros, reglas, anulacion, db, reservas, vinculos)
{
    public override DocumentClass Clase => DocumentClass.PosEquivalentDocument;
}

/// <summary><c>NonElectronicSalesReceipt</c>: el comprobante de venta de una cooperativa no obligada; se anula con documento contrario. (nuevo)</summary>
public sealed class EfectoComprobanteDeVenta(
    RegistroDeKardex registro, EmisionDeInventario emision, IMaestrosDelDocumento maestros, ReglasDeConfirmacionDeVenta reglas,
    AnulacionDeVenta anulacion, IApplicationDbContext db, ReservasDeInventario? reservas = null, VinculosDelCiclo? vinculos = null)
    : SalidaPorVenta(registro, emision, maestros, reglas, anulacion, db, reservas, vinculos)
{
    public override DocumentClass Clase => DocumentClass.NonElectronicSalesReceipt;
}

/// <summary>Las reglas de producto de una línea de venta o nota: existe, no está inactivo ni bloqueado. Devuelve los inventariables. (nuevo)</summary>
public static class ReglasDeLineasDeVenta
{
    public static async Task<Result<IReadOnlySet<int>>> ProductosAsync(InventoryDocument documento, IMaestrosDelDocumento maestros, CancellationToken ct)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productos = (await maestros.ProductosPorIdAsync(vivas.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        foreach (var linea in vivas)
        {
            if (!productos.TryGetValue(linea.ProductId, out var producto)) continue;
            if (producto.Status == ProductStatus.Blocked) return Result.Failure<IReadOnlySet<int>>(InventoryErrors.ProductBlocked(linea.LineNumber, producto.Code));
            if (producto.Status == ProductStatus.Inactive && documento.Class is not (DocumentClass.CreditNote or DocumentClass.PosAdjustmentNote or DocumentClass.NonElectronicSalesNote))
                return Result.Failure<IReadOnlySet<int>>(InventoryErrors.ProductInactive(linea.LineNumber, producto.Code));
        }
        return Result.Success<IReadOnlySet<int>>(productos.Values.Where(p => p.Inventariable).Select(p => p.Id).ToHashSet());
    }
}
