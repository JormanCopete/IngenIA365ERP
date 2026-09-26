using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>SupplierInvoice</c> (feature 012, T341; FR-050, US9-1, US9-2; contracts/api.md §14.4; mensajes.md §6.4,
/// §6.10). (nuevo)
/// <list type="bullet">
/// <item>contra sus recepciones (<c>InvoiceOfReceipt</c>, por documento y por línea, que el borrador escribió): del mismo
/// proveedor, confirmadas, sin pasar de lo recibido − facturado − devuelto (<c>Inventory.Purchase.InvoiceExceedsReceived</c>),
/// sin reunir recepciones con modos de paso distintos (<c>.MixedPostingDestinations</c>); las líneas sin recepción, servicios;</item>
/// <item>el documento del proveedor único entre los no liberados (<c>Inventory.SupplierInvoice.Duplicate</c>,
/// <c>.CufeDuplicate</c>);</item>
/// <item>impuestos y retenciones por <see cref="CalculoTributarioDeCompra"/> y su foto completa al confirmar (descontable y al
/// costo por separado, retenciones);</item>
/// <item>una diferencia de precio con la recepción no se retiene (E6): <see cref="RegistroDeKardex.RegistrarDiferenciasDePrecioAsync"/>
/// la reparte entre existencia y vendido y sale un <c>AjusteDeCostoReconocido</c> por recepción afectada;</item>
/// <item>a crédito nacen el 030 y el 032 en <c>Pending</c>; de contado, <c>NotApplicable</c>;</item>
/// <item>emite <c>FacturaProveedorRegistrada</c> v1 (sin costo) y copia el modo de su recepción (derivado,
/// <see cref="OrigenesDelModoAsync"/>);</item>
/// <item>anularla (el <b>registro</b>) reversa sus diferencias de precio, libera su número y no toca los eventos.</item>
/// </list>
/// </summary>
public sealed class EfectoFacturaDeProveedor(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    CalculoTributarioDeCompra calculo,
    VinculosDeCompra vinculos,
    DiferenciasDePrecioDeCompra diferencias,
    IApplicationDbContext db) : EfectoDeClaseBase
{
    private sealed record Preparado(
        CalculoDeCompra Calculo,
        IReadOnlyList<InventoryDocument> Recepciones,
        IReadOnlyList<DiferenciaDePrecioPedida> Diferencias,
        PedidoDeCerrojo Cerrojo);

    private readonly Dictionary<Guid, Preparado> _preparados = [];
    private readonly Dictionary<Guid, IReadOnlyList<DiferenciaDePrecioRegistrada>> _registradas = [];

    public override DocumentClass Clase => DocumentClass.SupplierInvoice;

    public override async Task<IReadOnlyList<InventoryDocument>> OrigenesDelModoAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        (_preparados.TryGetValue(contexto.Documento.PublicId, out var p) ? p.Recepciones : await vinculos.OrigenesAsync(contexto.Documento, DocumentLinkKind.InvoiceOfReceipt, ct))
            .Where(r => r.Status == DocumentStatus.Confirmed).ToList();

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (contexto.EsAnulacion)
        {
            var original = contexto.Original!;
            var reversa = await diferencias.DeFacturaAsync(original, await diferencias.AlCostoGuardadoAsync(original, ct),
                documento.Lines.Where(l => !l.IsDeleted).ToList(), -1m, ct);
            var cerrojo = await registro.CerrojoDeDiferenciasAsync(documento, reversa, ct);
            if (cerrojo.IsFailure) return Result.Failure(cerrojo.Error);
            _preparados[documento.PublicId] = new Preparado(new CalculoDeCompra([], TotalesDeCompra.Cero, [], false), [], reversa, cerrojo.Value);
            return Result.Success();
        }

        var comunes = await ReglasDeCompra.ComunesAsync(contexto, maestros, ct);
        if (comunes is not null) return Result.Failure(comunes);

        var detalle = await vinculos.DetalleAsync(documento, ct);
        if (detalle is null) return Result.Failure(InventoryErrors.FieldRequired("supplier"));
        if (detalle.IsElectronic && detalle.Cufe is null) return Result.Failure(ErroresDeCompras.CufeRequired());
        var duplicado = await ColisionDeFacturaDeProveedor.BuscarAsync(db, detalle, ct);
        if (duplicado is not null) return Result.Failure(duplicado);

        var recepciones = await ReglasDeFacturaAsync(documento, ct);
        if (recepciones.IsFailure) return Result.Failure(recepciones.Error);

        var calculado = await calculo.CalcularAsync(documento, contexto.Tipo, ct);
        if (calculado.IsFailure) return Result.Failure(calculado.Error);
        CalculoTributarioDeCompra.AplicarTotales(documento, calculado.Value.Totales);

        var alCosto = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.LineNumber, l => calculado.Value.AlCostoDeLinea(l.LineNumber));
        var pedidas = await diferencias.DeFacturaAsync(documento, alCosto, documento.Lines.Where(l => !l.IsDeleted).ToList(), 1m, ct);
        var cerrojoDeCosto = await registro.CerrojoDeDiferenciasAsync(documento, pedidas, ct);
        if (cerrojoDeCosto.IsFailure) return Result.Failure(cerrojoDeCosto.Error);

        _preparados[documento.PublicId] = new Preparado(calculado.Value, recepciones.Value, pedidas, cerrojoDeCosto.Value with
        {
            DocumentosDeOrigen = recepciones.Value.Select(r => r.Id).ToList(),
            Bodegas = cerrojoDeCosto.Value.Bodegas.Concat(recepciones.Value.Select(r => r.WarehouseId).OfType<int>()).Distinct().ToList(),
        });
        return Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Cerrojo with { Bodegas = base.Cerrojo(contexto).Bodegas.Concat(p.Cerrojo.Bodegas).Distinct().ToList() }
            : base.Cerrojo(contexto);

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var p = _preparados[documento.PublicId];

        var registradas = await registro.RegistrarDiferenciasDePrecioAsync(documento, p.Diferencias, ct);
        if (registradas.IsFailure) return Result.Failure(registradas.Error);
        _registradas[documento.PublicId] = registradas.Value;

        // La foto de impuestos y retenciones (descontable y al costo por separado, research R20).
        var lineas = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.LineNumber);
        foreach (var r in p.Calculo.Renglones)
            db.DocumentTaxLines.Add(CalculoTributarioDeCompra.Foto(documento.Id, r.Linea is int n && lineas.TryGetValue(n, out var l) ? l.Id : null, r));

        // Los eventos RADIAN: a crédito, pendientes; de contado, no aplican.
        var detalle = (await vinculos.DetalleAsync(documento, ct))!;
        foreach (var (codigo, estado) in TransicionesDeEventoRadian.Iniciales(detalle.IsCredit))
            db.SupplierInvoiceEvents.Add(new SupplierInvoiceEvent { DocumentId = documento.Id, EventCode = codigo, Status = estado });
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (!_preparados.TryGetValue(documento.PublicId, out var p)) return [];
        var detalle = await vinculos.DetalleAsync(documento, ct);
        if (detalle is null) return [];

        var lineas = await LineasDelMensajeAsync(documento, p.Calculo, ct);
        var contenidos = new List<object>
        {
            await emision.FacturaProveedorRegistradaAsync(documento, detalle, "Invoice", p.Recepciones, lineas, p.Calculo.Renglones, 1m, ct),
        };
        if (_registradas.TryGetValue(documento.PublicId, out var hechas))
            contenidos.AddRange(await AjustesPorRecepcionAsync(emision, db, documento.OperationDate, hechas, ct));
        return contenidos;
    }

    public override async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var p = _preparados[contexto.Documento.PublicId];
        var registradas = await registro.RegistrarDiferenciasDePrecioAsync(contexto.Documento, p.Diferencias, ct);
        if (registradas.IsFailure) return Result.Failure(registradas.Error);
        _registradas[contexto.Documento.PublicId] = registradas.Value;

        // El registro anulado libera su número (el proveedor puede volver a registrarse); sus eventos RADIAN no se tocan.
        var detalle = await db.SupplierInvoiceDetails.FirstOrDefaultAsync(d => d.DocumentId == contexto.Original!.Id, ct);
        if (detalle is not null) detalle.IsReleased = true;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var contenidos = new List<object>(await emision.AnulacionAsync(contexto.Documento, contexto.Original!, [], ct));
        if (_registradas.TryGetValue(contexto.Documento.PublicId, out var hechas))
            contenidos.AddRange(await AjustesPorRecepcionAsync(emision, db, contexto.Documento.OperationDate, hechas, ct));
        return contenidos;
    }

    // ------------------------------------------------------------------------------------------------ apoyo --

    /// <summary>Las reglas contra las recepciones (§14.4). Devuelve las recepciones de la factura.</summary>
    private async Task<Result<IReadOnlyList<InventoryDocument>>> ReglasDeFacturaAsync(InventoryDocument documento, CancellationToken ct)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var pares = await vinculos.DeAsync(documento, DocumentLinkKind.InvoiceOfReceipt, ct);
        var porLinea = pares.GroupBy(p => p.TargetLineId).ToDictionary(g => g.Key, g => g.First());
        var productos = (await maestros.ProductosPorIdAsync(vivas.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(x => x.Id);

        foreach (var linea in vivas.Where(l => !porLinea.ContainsKey(l.Id)))
        {
            if (productos.TryGetValue(linea.ProductId, out var producto) && producto.Inventariable)
                return Result.Failure<IReadOnlyList<InventoryDocument>>(ErroresDeCompras.GoodsWithoutReceipt(linea.LineNumber, producto.Code));
        }

        var recepciones = await vinculos.OrigenesAsync(documento, DocumentLinkKind.InvoiceOfReceipt, ct);
        var idsDeRecepcion = pares.Select(p => p.SourceLineId).Distinct().ToList();
        var lineasDeRecepcion = await db.InventoryDocumentLines.AsNoTracking()
            .Where(l => idsDeRecepcion.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        foreach (var linea in vivas.Where(l => porLinea.ContainsKey(l.Id)))
        {
            var recepcion = recepciones.First(r => r.Id == porLinea[linea.Id].SourceDocumentId);
            var numero = VistaDeDocumentos.NumeroVisible(recepcion.Prefix, recepcion.Number);
            if (recepcion.CounterpartyPersonId != documento.CounterpartyPersonId)
                return Result.Failure<IReadOnlyList<InventoryDocument>>(ErroresDeCompras.ReceiptFromOtherSupplier(linea.LineNumber, recepcion.PublicId, numero));
            if (recepcion.Status != DocumentStatus.Confirmed)
                return Result.Failure<IReadOnlyList<InventoryDocument>>(ErroresDeCompras.ReceiptNotConfirmed(linea.LineNumber, recepcion.PublicId, numero));
        }

        if (recepciones.Select(r => r.PostingMode).Distinct().Count() > 1)
        {
            return Result.Failure<IReadOnlyList<InventoryDocument>>(ErroresDeCompras.MixedPostingDestinations(recepciones
                .Select(r => new ErroresDeCompras.RecepcionConModo(r.PublicId, VistaDeDocumentos.NumeroVisible(r.Prefix, r.Number), r.PostingMode?.ToString()))
                .ToList()));
        }

        var consumo = await vinculos.ConsumoAsync(lineasDeRecepcion.Keys.ToList(), documento.Id, ct);
        foreach (var grupo in vivas.Where(l => porLinea.ContainsKey(l.Id)).GroupBy(l => porLinea[l.Id].SourceLineId))
        {
            var origen = lineasDeRecepcion[grupo.Key];
            var hecho = consumo.GetValueOrDefault(grupo.Key) ?? new ConsumoDeRecepcion(0m, 0m);
            var disponible = origen.QuantityBase - hecho.Facturado - hecho.Devuelto;
            if (grupo.Sum(l => l.QuantityBase) > disponible)
            {
                return Result.Failure<IReadOnlyList<InventoryDocument>>(ErroresDeCompras.InvoiceExceedsReceived(grupo.First().LineNumber,
                    origen.QuantityBase, hecho.Facturado, hecho.Devuelto, Math.Max(0m, disponible)));
            }
        }
        return Result.Success(recepciones);
    }

    /// <summary>Las líneas del mensaje: importe, lo que fue al costo y la bodega de la recepción (o ninguna, en un servicio).</summary>
    private async Task<IReadOnlyList<EmisionDeInventario.LineaDeFacturaDeProveedor>> LineasDelMensajeAsync(
        InventoryDocument documento, CalculoDeCompra calculado, CancellationToken ct)
    {
        var pares = await vinculos.DeAsync(documento, DocumentLinkKind.InvoiceOfReceipt, ct);
        var recepcionDeLinea = pares.GroupBy(p => p.TargetLineId).ToDictionary(g => g.Key, g => g.First().SourceDocumentId);
        var recepciones = recepcionDeLinea.Values.Distinct().ToList();
        var bodegaDeRecepcion = await db.InventoryDocuments.AsNoTracking()
            .Where(d => recepciones.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.WarehouseId, ct);
        return documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).Select(l =>
        {
            var conRecepcion = recepcionDeLinea.TryGetValue(l.Id, out var recepcion);
            return new EmisionDeInventario.LineaDeFacturaDeProveedor(l.LineNumber, l.ProductId,
                conRecepcion ? bodegaDeRecepcion.GetValueOrDefault(recepcion) : null, !conRecepcion,
                l.GrossAmount, l.DiscountAmount, l.NetAmount, calculado.AlCostoDeLinea(l.LineNumber));
        }).ToList();
    }

    /// <summary>Un <c>AjusteDeCostoReconocido</c> <c>PriceDifference</c> por recepción afectada (§6.10).</summary>
    internal static async Task<IReadOnlyList<object>> AjustesPorRecepcionAsync(
        EmisionDeInventario emision, IApplicationDbContext db, DateOnly fecha, IReadOnlyList<DiferenciaDePrecioRegistrada> hechas, CancellationToken ct)
    {
        var contenidos = new List<object>();
        foreach (var porRecepcion in hechas.GroupBy(h => h.Pedida.Entrada.DocumentId))
        {
            var recepcion = await db.InventoryDocuments.AsNoTracking().FirstAsync(d => d.Id == porRecepcion.Key, ct);
            contenidos.Add(await emision.AjusteDeDiferenciaDePrecioAsync(recepcion, fecha, porRecepcion.ToList(), ct));
        }
        return contenidos;
    }
}
