using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Purchasing;
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
/// <item>anularla (el <b>registro</b>) reversa sus diferencias de precio, libera su número y no toca los eventos;</item>
/// <item>I5 (T794, T796): si alguna recepción enlazada viene de una orden, <see cref="CruceATresVias"/> cruza cada línea contra lo
/// ordenado y lo recibido en el paso 2; en un intento nuevo da de baja las filas del anterior, y en el paso de aprobaciones
/// (<see cref="AprobacionPropiaAsync"/>, también en la reentrada) escribe <c>INV_PurchaseMatchLines</c> y pide una aprobación por línea
/// retenida: la factura queda <c>PendingApproval</c> sin número hasta que <see cref="DecisionDeCruce"/> apruebe la última. Con orden,
/// facturar más de lo recibido no responde 422: queda retenido por cantidad. Sin <c>cruce</c> (el documento soporte,
/// que no se cruza) sigue el cruce de dos vías de I1.</item>
/// </list>
/// </summary>
public class EfectoFacturaDeProveedor(
    RegistroDeKardex registro,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    CalculoTributarioDeCompra calculo,
    VinculosDeCompra vinculos,
    DiferenciasDePrecioDeCompra diferencias,
    IApplicationDbContext db,
    CruceATresVias? cruce = null) : EfectoDeClaseBase
{
    private sealed record Preparado(
        CalculoDeCompra Calculo,
        IReadOnlyList<InventoryDocument> Recepciones,
        IReadOnlyList<DiferenciaDePrecioPedida> Diferencias,
        PedidoDeCerrojo Cerrojo);

    private readonly Dictionary<Guid, Preparado> _preparados = [];
    private readonly Dictionary<Guid, IReadOnlyList<DiferenciaDePrecioRegistrada>> _registradas = [];
    private readonly Dictionary<Guid, IReadOnlyList<LineaCruzada>> _cruces = [];

    public override DocumentClass Clase => DocumentClass.SupplierInvoice;

    /// <summary>¿Nacen los eventos RADIAN 030/032? En la factura del proveedor sí; el documento soporte los emite la cooperativa. (I4, T740)</summary>
    /// <summary>La base, para las clases que se montan sobre ésta (I4, T740).</summary>
    protected IApplicationDbContext BaseDeDatos => db;

    protected virtual bool RegistraEventosRadian => true;

    /// <summary>El <c>supplierDocument.kind</c> de <c>FacturaProveedorRegistrada</c>. (I4, T740)</summary>
    protected virtual string TipoDelMensaje => "Invoice";

    /// <summary>
    /// Las reglas del documento del proveedor antes de la aprobación: en la factura, su número y CUFE (únicos entre los no liberados). El
    /// documento soporte las reemplaza: lo numera la cooperativa y el vendedor no está obligado a facturar. (I4, T740)
    /// </summary>
    protected virtual async Task<Error?> ReglasDelDocumentoAsync(InventoryDocument documento, CancellationToken ct)
    {
        var detalle = await vinculos.DetalleAsync(documento, ct);
        if (detalle is null) return InventoryErrors.FieldRequired("supplier");
        if (detalle.IsElectronic && detalle.Cufe is null) return ErroresDeCompras.CufeRequired();
        return await ColisionDeFacturaDeProveedor.BuscarAsync(db, detalle, ct);
    }

    /// <summary>El documento del proveedor que viaja en el mensaje; el documento soporte lo arma con su propio número. (I4, T740)</summary>
    protected virtual Task<SupplierInvoiceDetail?> DetalleDelMensajeAsync(InventoryDocument documento, CancellationToken ct) =>
        vinculos.DetalleAsync(documento, ct);

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

        var delDocumento = await ReglasDelDocumentoAsync(documento, ct);
        if (delDocumento is not null) return Result.Failure(delDocumento);

        var recepciones = await ReglasDeFacturaAsync(documento, ct);
        if (recepciones.IsFailure) return Result.Failure(recepciones.Error);

        // I5 (T794): el cruce a tres vías, sin escribir; un intento nuevo da de baja las filas del anterior.
        if (cruce is not null)
        {
            var cruzadas = await cruce.CruzarAsync(documento, ct);
            if (cruzadas.IsFailure) return Result.Failure(cruzadas.Error);
            if (documento.Status == DocumentStatus.Draft) await cruce.DarDeBajaAnterioresAsync(documento, ct);
            _cruces[documento.PublicId] = cruzadas.Value;
        }

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

    /// <summary>
    /// I5 (T794): las filas del cruce y una aprobación por línea retenida (<c>PurchaseMatchException</c>). Nula sin orden o sin nada
    /// retenido.
    /// </summary>
    public override Task<Result<Domain.Entities.Approvals.ApprovalRequest?>> AprobacionPropiaAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        cruce is not null && !contexto.EsAnulacion && _cruces.TryGetValue(contexto.Documento.PublicId, out var cruzadas)
            ? cruce.SolicitarAsync(contexto.Documento, contexto.Tipo, cruzadas, ct)
            : Task.FromResult(Result.Success<Domain.Entities.Approvals.ApprovalRequest?>(null));

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
        if (!RegistraEventosRadian) return Result.Success();
        var detalle = (await vinculos.DetalleAsync(documento, ct))!;
        foreach (var (codigo, estado) in TransicionesDeEventoRadian.Iniciales(detalle.IsCredit))
            db.SupplierInvoiceEvents.Add(new SupplierInvoiceEvent { DocumentId = documento.Id, EventCode = codigo, Status = estado });
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (!_preparados.TryGetValue(documento.PublicId, out var p)) return [];
        var detalle = await DetalleDelMensajeAsync(documento, ct);
        if (detalle is null) return [];

        var lineas = await LineasDelMensajeAsync(documento, p.Calculo, ct);
        var contenidos = new List<object>
        {
            await emision.FacturaProveedorRegistradaAsync(documento, detalle, TipoDelMensaje, p.Recepciones, lineas, p.Calculo.Renglones, 1m, ct),
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
        // I5 (T796): contra una recepción que viene de una orden, facturar de más no es un 422: lo retiene el cruce por cantidad.
        var conOrden = cruce is null ? new Dictionary<int, int>() : await cruce.OrdenDeLasRecepcionesAsync(lineasDeRecepcion.Keys.ToList(), ct);
        foreach (var grupo in vivas.Where(l => porLinea.ContainsKey(l.Id)).GroupBy(l => porLinea[l.Id].SourceLineId).Where(g => !conOrden.ContainsKey(g.Key)))
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
