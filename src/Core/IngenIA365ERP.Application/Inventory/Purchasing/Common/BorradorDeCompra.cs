using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>
/// La compra directa (T344) arma la factura contra una recepción que puede quedar en aprobación en la misma operación: sólo
/// entonces el borrador de la factura admite una recepción que todavía no está confirmada. Scoped. (nuevo)
/// </summary>
public sealed class ContextoDeCompraDirecta
{
    public bool PermiteRecepcionPendiente { get; set; }
}

/// <summary>
/// El borrador de compras (feature 012, T339; contracts/api.md §9.3, §14.1–§14.6): lo que el grupo <c>Purchases</c> agrega al
/// guardado del ciclo común (<see cref="IBorradorDeGrupo"/>). (nuevo)
/// <list type="bullet">
/// <item><b>Campos por clase</b>: el documento del proveedor sólo en factura y nota; la factura de origen y la clase de nota sólo
/// en la nota; la línea de recepción en factura y devolución; la línea de factura, el valor y <c>affectsCost</c> sólo en la
/// nota; el costo digitado en ninguna. Lo demás: <c>Validation.Invalid</c>.</item>
/// <item><b>Líneas desde su origen</b>: producto y unidad de la línea de recepción o de factura; en la nota, la cantidad de la
/// factura si no viene y el valor en vez del precio.</item>
/// <item><b>Proveedor</b> obligatorio (en la factura y la nota, porque el número es único por proveedor); <b>municipio</b>
/// propuesto desde la sucursal de la bodega que recibe, y uno inexistente es <c>Inventory.Purchase.MunicipalityUnknown</c>.</item>
/// <item><b>Vínculos</b> con sus orígenes (<c>InvoiceOfReceipt</c>, <c>NoteOf</c>, <c>ReturnOf</c>), recepción del mismo
/// proveedor y confirmada, factura sin mezclar modos de paso; la bodega y la sucursal de una factura, las de sus recepciones.</item>
/// <item><b>Documento del proveedor</b> (<c>INV_SupplierInvoiceDetails</c>) con su unicidad, CUFE y fechas.</item>
/// <item><b>Impuestos y totales</b> por <see cref="CalculoTributarioDeCompra"/> en cada guardado, como vista previa.</item>
/// </list>
/// Lo que depende de otros documentos y puede cambiar antes de confirmar (lo facturado, lo devuelto, lo que queda de la
/// factura, la UVT vigente) vuelve como aviso con el código que daría la confirmación.
/// </summary>
public sealed class BorradorDeCompra(
    IApplicationDbContext db,
    IDateTimeService reloj,
    CalculoTributarioDeCompra calculo,
    VinculosDeCompra vinculos,
    ContextoDeCompraDirecta compraDirecta) : IBorradorDeGrupo
{
    public DocumentClassGroup Grupo => DocumentClassGroup.Purchases;

    // -------------------------------------------------------------------------------------------- preparar --

    public async Task<Result<SaveInventoryDraftRequest>> PrepararAsync(
        InventoryDocumentType tipo, SaveInventoryDraftRequest pedido, InventoryDocument? existente, CancellationToken ct)
    {
        var clase = tipo.Class;
        var esFactura = clase == DocumentClass.SupplierInvoice;
        var esNota = clase == DocumentClass.SupplierNote;
        var esDevolucion = clase == DocumentClass.SupplierReturn;

        if (pedido.Supplier is not null && !esFactura && !esNota) return Falla(ErroresDeCompras.CampoNoAdmitido("supplier", clase));
        if ((pedido.SupplierInvoicePublicId is not null || pedido.NoteKind is not null) && !esNota)
            return Falla(ErroresDeCompras.CampoNoAdmitido(pedido.NoteKind is null ? "supplierInvoicePublicId" : "noteKind", clase));
        if (pedido.OperationMunicipalityDaneCode is not null && esDevolucion)
            return Falla(ErroresDeCompras.CampoNoAdmitido("operationMunicipalityDaneCode", clase));

        var lineas = new List<SaveInventoryDraftLine>(pedido.Lines.Count);
        for (var i = 0; i < pedido.Lines.Count; i++)
        {
            var l = pedido.Lines[i];
            var numero = i + 1;
            if (l.UnitCost is not null) return Falla(ErroresDeCompras.CampoNoAdmitido("lines.unitCost", clase));
            if (l.ReceiptLinePublicId is not null && !esFactura && !esDevolucion) return Falla(ErroresDeCompras.CampoNoAdmitido("lines.receiptLinePublicId", clase));
            if ((l.InvoiceLinePublicId is not null || l.Amount is not null || l.AffectsCost is not null) && !esNota)
                return Falla(ErroresDeCompras.CampoNoAdmitido(l.InvoiceLinePublicId is not null ? "lines.invoiceLinePublicId" : l.Amount is not null ? "lines.amount" : "lines.affectsCost", clase));

            if (l.Origen is not { } origen)
            {
                if (esDevolucion) return Falla(ErroresDeCompras.ReturnReceiptLineRequired(numero));
                if (esNota) return Falla(ErroresDeCompras.NoteLineRequired(numero));
                lineas.Add(l);
                continue;
            }

            var fuente = await db.InventoryDocumentLines.AsNoTracking().Include(x => x.Document)
                .FirstOrDefaultAsync(x => x.PublicId == origen && !x.IsDeleted, ct);
            if (fuente?.Document is null) return Falla(InventoryErrors.DocumentNotFound());
            var claseEsperada = esNota ? DocumentClass.SupplierInvoice : DocumentClass.PurchaseReceipt;
            if (fuente.Document.Class != claseEsperada)
                return Falla(esNota ? ErroresDeCompras.NoteLineRequired(numero) : esDevolucion ? ErroresDeCompras.ReturnReceiptLineRequired(numero)
                    : ErroresDeCompras.GoodsWithoutReceipt(numero, string.Empty));

            var producto = await db.Products.AsNoTracking().Where(p => p.Id == fuente.ProductId).Select(p => p.PublicId).FirstAsync(ct);
            if (l.ProductPublicId != Guid.Empty && l.ProductPublicId != producto)
                return Falla(new ErrorConDatos("Validation.Invalid", $"Línea {numero}: el producto no es el de la línea de origen.", new { lineNumber = numero }));
            var unidad = l.UnitPublicId != Guid.Empty
                ? l.UnitPublicId
                : await db.UnitsOfMeasure.AsNoTracking().Where(u => u.Id == fuente.UnitId).Select(u => u.PublicId).FirstAsync(ct);

            var cantidad = l.Quantity;
            var precio = l.UnitPrice;
            if (esNota)
            {
                if (cantidad == 0m) cantidad = fuente.Quantity;
                if (l.Amount is { } valor && precio is null) precio = Math.Round(valor / cantidad, 6, MidpointRounding.AwayFromZero);
                precio ??= fuente.UnitPrice;
            }
            else if (esDevolucion)
            {
                precio ??= fuente.UnitPrice;
            }
            else
            {
                precio ??= fuente.UnitPrice;
            }
            lineas.Add(l with { ProductPublicId = producto, UnitPublicId = unidad, Quantity = cantidad, UnitPrice = precio });
        }
        return Result.Success(pedido with { Lines = lineas });
    }

    // --------------------------------------------------------------------------------------------- aplicar --

    public async Task<Result<ResultadoDelBorrador>> AplicarAsync(BorradorEnCurso borrador, CancellationToken ct)
    {
        var documento = borrador.Documento;
        var pedido = borrador.Pedido;
        PedidoActual = pedido;
        var clase = borrador.Tipo.Class;
        var avisos = new List<Error>();
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();

        // Proveedor: la factura y la nota no se guardan sin él (su número es único por proveedor); lo demás lo avisa.
        if (documento.CounterpartyPersonId is null)
        {
            if (ColisionDeFacturaDeProveedor.LlevaDocumentoDelProveedor(clase))
                return Falla<ResultadoDelBorrador>(InventoryErrors.FieldRequired(ReglasDelDocumento.CampoContraparte));
            avisos.Add(InventoryErrors.FieldRequired(ReglasDelDocumento.CampoContraparte));
        }

        IReadOnlyList<Domain.Taxes.RenglonTributario>? original = null;
        Dictionary<int, int>? lineaOriginal = null;

        switch (clase)
        {
            case DocumentClass.SupplierInvoice:
            {
                var r = await FacturaAsync(documento, vivas, avisos, ct);
                if (r.IsFailure) return Falla<ResultadoDelBorrador>(r.Error);
                break;
            }
            case DocumentClass.SupplierNote:
            {
                var r = await NotaAsync(documento, pedido, vivas, avisos, ct);
                if (r.IsFailure) return Falla<ResultadoDelBorrador>(r.Error);
                (original, lineaOriginal) = r.Value;
                break;
            }
            case DocumentClass.SupplierReturn:
            {
                var r = await DevolucionAsync(documento, vivas, avisos, ct);
                if (r.IsFailure) return Falla<ResultadoDelBorrador>(r.Error);
                break;
            }
        }

        // Municipio de la operación (ReteICA): el pedido, o el de la sucursal de la bodega que recibe.
        if (clase != DocumentClass.SupplierReturn)
        {
            var municipio = await MunicipioAsync(documento, pedido.OperationMunicipalityDaneCode, ct);
            if (municipio.IsFailure) return Falla<ResultadoDelBorrador>(municipio.Error);
        }

        // Documento del proveedor (factura y nota).
        if (ColisionDeFacturaDeProveedor.LlevaDocumentoDelProveedor(clase))
        {
            var detalle = await DetalleAsync(documento, pedido, clase, ct);
            if (detalle.IsFailure) return Falla<ResultadoDelBorrador>(detalle.Error);
        }

        // Impuestos y totales: la vista previa del servidor (la devolución no factura: su ajuste lo hace la nota del proveedor).
        IReadOnlyList<DocumentTaxLineDto> previstos = [];
        if (clase != DocumentClass.SupplierReturn && documento.CounterpartyPersonId is not null)
        {
            var calculado = await calculo.CalcularAsync(documento, borrador.Tipo, ct, original, lineaOriginal);
            if (calculado.IsFailure)
            {
                avisos.Add(calculado.Error);
            }
            else
            {
                CalculoTributarioDeCompra.AplicarTotales(documento, calculado.Value.Totales);
                previstos = calculado.Value.Renglones.Select(r => new DocumentTaxLineDto(r.Linea, r.Kind, r.TaxRateCode, r.Rate, r.AmountPerUnit,
                    r.Base, r.Amount, r.Treatment, r.MunicipalityDaneCode, JsonSerializer.Serialize(r.Explicacion))).ToList();
            }
        }

        return Result.Success(new ResultadoDelBorrador(avisos, previstos));
    }

    public async Task<Error?> TraducirColisionAsync(DbUpdateException ex, BorradorEnCurso borrador, CancellationToken ct)
    {
        if (!ColisionDeFacturaDeProveedor.Es(ex)) return null;
        var detalle = await vinculos.DetalleAsync(borrador.Documento, ct);
        return detalle is null ? null : await ColisionDeFacturaDeProveedor.TraducirAsync(db, ex, detalle, ct);
    }

    // ------------------------------------------------------------------------------------------ por clase --

    /// <summary>La factura contra sus recepciones: del mismo proveedor, confirmadas, un solo modo de paso; bodega y sucursal.</summary>
    private async Task<Result> FacturaAsync(InventoryDocument documento, List<InventoryDocumentLine> vivas, List<Error> avisos, CancellationToken ct)
    {
        var pares = new List<(InventoryDocumentLine Origen, InventoryDocumentLine Destino)>();
        var origenes = await OrigenesDeLasLineasAsync(documento, ct);
        var idsDeProducto = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => idsDeProducto.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Code, p.Kind }, ct);

        foreach (var linea in vivas)
        {
            if (!origenes.TryGetValue(linea.LineNumber, out var origen))
            {
                if (productos.TryGetValue(linea.ProductId, out var p) && p.Kind != ProductKind.Service)
                    return Result.Failure(ErroresDeCompras.GoodsWithoutReceipt(linea.LineNumber, p.Code));
                continue;
            }
            pares.Add((origen, linea));
        }

        var idsDeRecepcion = pares.Select(p => p.Origen.DocumentId).Distinct().ToList();
        var recepciones = await db.InventoryDocuments.AsNoTracking().Where(d => idsDeRecepcion.Contains(d.Id)).ToListAsync(ct);
        foreach (var (origen, linea) in pares)
        {
            var recepcion = recepciones.First(r => r.Id == origen.DocumentId);
            var numero = VistaDeDocumentos.NumeroVisible(recepcion.Prefix, recepcion.Number);
            if (recepcion.CounterpartyPersonId != documento.CounterpartyPersonId)
                return Result.Failure(ErroresDeCompras.ReceiptFromOtherSupplier(linea.LineNumber, recepcion.PublicId, numero));
            var admitida = recepcion.Status == DocumentStatus.Confirmed
                           || (compraDirecta.PermiteRecepcionPendiente && recepcion.Status is DocumentStatus.PendingApproval or DocumentStatus.Draft);
            if (!admitida) return Result.Failure(ErroresDeCompras.ReceiptNotConfirmed(linea.LineNumber, recepcion.PublicId, numero));
        }
        var modos = recepciones.Where(r => r.Status == DocumentStatus.Confirmed).Select(r => r.PostingMode).Distinct().ToList();
        if (modos.Count > 1)
        {
            return Result.Failure(ErroresDeCompras.MixedPostingDestinations(recepciones
                .Select(r => new ErroresDeCompras.RecepcionConModo(r.PublicId, VistaDeDocumentos.NumeroVisible(r.Prefix, r.Number), r.PostingMode?.ToString()))
                .ToList()));
        }

        await vinculos.ReemplazarAsync(documento, DocumentLinkKind.InvoiceOfReceipt, pares, ct);

        // La bodega y la sucursal: las de sus recepciones (una sola bodega si todas comparten; si no, nula).
        if (recepciones.Count > 0)
        {
            var bodegas = recepciones.Select(r => r.WarehouseId).Distinct().ToList();
            documento.WarehouseId = bodegas.Count == 1 ? bodegas[0] : null;
            documento.BranchId = recepciones[0].BranchId;
        }
        else if (documento.BranchId == 0)
        {
            documento.BranchId = await SucursalPrincipalAsync(ct);
        }

        // Lo facturado de más se avisa (depende de otros documentos: la confirmación lo vuelve a mirar).
        var consumo = await vinculos.ConsumoAsync(pares.Select(p => p.Origen.Id).ToList(), documento.Id, ct);
        foreach (var (origen, linea) in pares)
        {
            var hecho = consumo.GetValueOrDefault(origen.Id) ?? new ConsumoDeRecepcion(0m, 0m);
            var disponible = origen.QuantityBase - hecho.Facturado - hecho.Devuelto;
            var yaEnEste = pares.Where(p => p.Origen.Id == origen.Id).Sum(p => p.Destino.QuantityBase);
            if (yaEnEste > disponible)
                avisos.Add(ErroresDeCompras.InvoiceExceedsReceived(linea.LineNumber, origen.QuantityBase, hecho.Facturado, hecho.Devuelto, Math.Max(0m, disponible)));
        }
        return Result.Success();
    }

    /// <summary>La nota contra una factura confirmada del mismo proveedor; sus líneas contra las de la factura.</summary>
    private async Task<Result<(IReadOnlyList<Domain.Taxes.RenglonTributario>? Original, Dictionary<int, int>? LineaOriginal)>> NotaAsync(
        InventoryDocument documento, SaveInventoryDraftRequest pedido, List<InventoryDocumentLine> vivas, List<Error> avisos, CancellationToken ct)
    {
        if (pedido.SupplierInvoicePublicId is not { } facturaId)
            return Result.Failure<(IReadOnlyList<Domain.Taxes.RenglonTributario>?, Dictionary<int, int>?)>(InventoryErrors.FieldRequired("supplierInvoice"));
        if (pedido.NoteKind is not ("Credit" or "Debit"))
            return Result.Failure<(IReadOnlyList<Domain.Taxes.RenglonTributario>?, Dictionary<int, int>?)>(new Error("Validation.Invalid", "La clase de la nota es Credit o Debit."));

        var factura = await db.InventoryDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.PublicId == facturaId && d.Class == DocumentClass.SupplierInvoice, ct);
        if (factura is null) return Result.Failure<(IReadOnlyList<Domain.Taxes.RenglonTributario>?, Dictionary<int, int>?)>(InventoryErrors.DocumentNotFound());
        var numero = VistaDeDocumentos.NumeroVisible(factura.Prefix, factura.Number);
        if (factura.Status != DocumentStatus.Confirmed)
            return Result.Failure<(IReadOnlyList<Domain.Taxes.RenglonTributario>?, Dictionary<int, int>?)>(ErroresDeCompras.NoteInvoiceNotConfirmed(factura.PublicId, numero));
        if (factura.CounterpartyPersonId != documento.CounterpartyPersonId)
            return Result.Failure<(IReadOnlyList<Domain.Taxes.RenglonTributario>?, Dictionary<int, int>?)>(ErroresDeCompras.NoteInvoiceFromOtherSupplier(factura.PublicId, numero));

        var origenes = await OrigenesDeLasLineasAsync(documento, ct);
        var pares = new List<(InventoryDocumentLine Origen, InventoryDocumentLine Destino)>();
        var lineaOriginal = new Dictionary<int, int>();
        for (var i = 0; i < vivas.Count; i++)
        {
            var linea = vivas[i];
            if (!origenes.TryGetValue(linea.LineNumber, out var origen) || origen.DocumentId != factura.Id)
                return Result.Failure<(IReadOnlyList<Domain.Taxes.RenglonTributario>?, Dictionary<int, int>?)>(ErroresDeCompras.NoteLineRequired(linea.LineNumber));
            pares.Add((origen, linea));
            lineaOriginal[linea.LineNumber] = origen.LineNumber;

            // El valor de la línea, tal cual (el precio es su cociente).
            if (linea.LineNumber - 1 < pedido.Lines.Count && pedido.Lines[linea.LineNumber - 1].Amount is { } valor)
            {
                linea.GrossAmount = valor;
                if (linea.DiscountAmount > valor) linea.DiscountAmount = valor;
                linea.NetAmount = valor - linea.DiscountAmount;
            }
        }
        await vinculos.ReemplazarAsync(documento, DocumentLinkKind.NoteOf, pares, ct);
        documento.WarehouseId = factura.WarehouseId;
        documento.BranchId = factura.BranchId;
        documento.Subtotal = vivas.Sum(l => l.GrossAmount);
        documento.DiscountTotal = vivas.Sum(l => l.DiscountAmount);

        var foto = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == factura.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var numeros = factura.Lines.ToDictionary(l => l.Id, l => l.LineNumber);
        var renglones = CalculoTributarioDeCompra.DesdeLaFoto(foto, numeros);

        if (pedido.NoteKind == "Credit")
        {
            var restante = await vinculos.RestanteDeFacturaAsync(factura, documento.Id, ct);
            var neto = vivas.Sum(l => l.NetAmount);
            if (neto > restante) avisos.Add(ErroresDeCompras.NoteExceedsInvoice(restante));
        }
        return Result.Success<(IReadOnlyList<Domain.Taxes.RenglonTributario>?, Dictionary<int, int>?)>((renglones, lineaOriginal));
    }

    /// <summary>La devolución: cada línea contra una de recepción del mismo proveedor, confirmada; lo devuelto de más se avisa.</summary>
    private async Task<Result> DevolucionAsync(InventoryDocument documento, List<InventoryDocumentLine> vivas, List<Error> avisos, CancellationToken ct)
    {
        var origenes = await OrigenesDeLasLineasAsync(documento, ct);
        var pares = new List<(InventoryDocumentLine Origen, InventoryDocumentLine Destino)>();
        foreach (var linea in vivas)
        {
            if (!origenes.TryGetValue(linea.LineNumber, out var origen)) return Result.Failure(ErroresDeCompras.ReturnReceiptLineRequired(linea.LineNumber));
            pares.Add((origen, linea));
        }
        var idsDeRecepcion = pares.Select(p => p.Origen.DocumentId).Distinct().ToList();
        var recepciones = await db.InventoryDocuments.AsNoTracking().Where(d => idsDeRecepcion.Contains(d.Id)).ToListAsync(ct);
        foreach (var (origen, linea) in pares)
        {
            var recepcion = recepciones.First(r => r.Id == origen.DocumentId);
            var numero = VistaDeDocumentos.NumeroVisible(recepcion.Prefix, recepcion.Number);
            if (recepcion.CounterpartyPersonId != documento.CounterpartyPersonId)
                return Result.Failure(ErroresDeCompras.ReceiptFromOtherSupplier(linea.LineNumber, recepcion.PublicId, numero));
            if (recepcion.Status != DocumentStatus.Confirmed)
                return Result.Failure(ErroresDeCompras.ReceiptNotConfirmed(linea.LineNumber, recepcion.PublicId, numero));
        }
        await vinculos.ReemplazarAsync(documento, DocumentLinkKind.ReturnOf, pares, ct);
        if (documento.WarehouseId is null && recepciones.Count > 0)
        {
            documento.WarehouseId = recepciones[0].WarehouseId;
            documento.BranchId = recepciones[0].BranchId;
        }

        var consumo = await vinculos.ConsumoAsync(pares.Select(p => p.Origen.Id).ToList(), documento.Id, ct);
        foreach (var (origen, linea) in pares)
        {
            var devuelto = consumo.GetValueOrDefault(origen.Id)?.Devuelto ?? 0m;
            var enEste = pares.Where(p => p.Origen.Id == origen.Id).Sum(p => p.Destino.QuantityBase);
            if (devuelto + enEste > origen.QuantityBase)
                avisos.Add(ErroresDeCompras.ReturnExceedsReceived(linea.LineNumber, origen.QuantityBase, devuelto));
        }
        return Result.Success();
    }

    // --------------------------------------------------------------------------------------------- apoyo --

    /// <summary>La línea de origen de cada línea del borrador, por número de línea (del pedido ya preparado).</summary>
    private async Task<Dictionary<int, InventoryDocumentLine>> OrigenesDeLasLineasAsync(InventoryDocument documento, CancellationToken ct)
    {
        var pedidas = PedidoActual?.Lines ?? [];
        var publicas = pedidas.Select(l => l.Origen).OfType<Guid>().Distinct().ToList();
        var fuentes = await db.InventoryDocumentLines.AsNoTracking().Where(l => publicas.Contains(l.PublicId) && !l.IsDeleted)
            .ToDictionaryAsync(l => l.PublicId, ct);
        var resultado = new Dictionary<int, InventoryDocumentLine>();
        for (var i = 0; i < pedidas.Count; i++)
        {
            if (pedidas[i].Origen is { } origen && fuentes.TryGetValue(origen, out var fuente)) resultado[i + 1] = fuente;
        }
        return resultado;
    }

    /// <summary>El pedido que se está aplicando (lo fija <see cref="AplicarAsync"/> para los ayudantes).</summary>
    private SaveInventoryDraftRequest? PedidoActual { get; set; }

    private async Task<Result> MunicipioAsync(InventoryDocument documento, string? pedido, CancellationToken ct)
    {
        var codigo = string.IsNullOrWhiteSpace(pedido) ? null : pedido.Trim();
        if (codigo is not null)
        {
            if (!await db.Cities.AsNoTracking().AnyAsync(c => c.DaneCode == codigo, ct)) return Result.Failure(ErroresDeCompras.MunicipalityUnknown(codigo));
            documento.OperationMunicipalityDaneCode = codigo;
            return Result.Success();
        }
        documento.OperationMunicipalityDaneCode = documento.BranchId == 0
            ? null
            : await db.Branches.AsNoTracking().Where(b => b.Id == documento.BranchId).Select(b => b.MunicipalityDaneCode).FirstOrDefaultAsync(ct);
        return Result.Success();
    }

    private async Task<Result> DetalleAsync(InventoryDocument documento, SaveInventoryDraftRequest pedido, DocumentClass clase, CancellationToken ct)
    {
        if (pedido.Supplier is not { } proveedor || string.IsNullOrWhiteSpace(proveedor.Number))
            return Result.Failure(InventoryErrors.FieldRequired("supplier"));

        var hoy = reloj.HoyLocal;
        var cufe = SupplierInvoiceDetail.NormalizarCufe(proveedor.Cufe);
        if (cufe is not null && (cufe.Length != SupplierInvoiceDetail.LargoDelCufe || !cufe.All(Uri.IsHexDigit))) return Result.Failure(ErroresDeCompras.CufeInvalid());
        if (proveedor.IsElectronic && cufe is null) return Result.Failure(ErroresDeCompras.CufeRequired());
        if (proveedor.IssueDate > hoy) return Result.Failure(ErroresDeCompras.IssueDateInFuture(hoy));

        bool aCredito;
        if (clase == DocumentClass.SupplierNote)
        {
            var factura = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == pedido.SupplierInvoicePublicId).Select(d => d.Id).FirstOrDefaultAsync(ct);
            aCredito = await db.SupplierInvoiceDetails.AsNoTracking().Where(s => s.DocumentId == factura).Select(s => s.IsCredit).FirstOrDefaultAsync(ct);
        }
        else
        {
            if (proveedor.PaymentForm is not (SupplierInvoiceDetail.Contado or SupplierInvoiceDetail.Credito))
                return Result.Failure(new Error("Validation.Invalid", "La forma de pago es Cash (contado) o Credit (crédito)."));
            aCredito = proveedor.PaymentForm == SupplierInvoiceDetail.Credito;
            if ((aCredito && proveedor.DueDate is null) || (proveedor.DueDate is { } vence && vence < proveedor.IssueDate))
                return Result.Failure(ErroresDeCompras.DueDateInvalid(proveedor.IssueDate));
        }

        var detalle = await vinculos.DetalleAsync(documento, ct);
        if (detalle is null)
        {
            detalle = new SupplierInvoiceDetail { Document = documento };
            db.SupplierInvoiceDetails.Add(detalle);
        }
        detalle.DocumentId = documento.Id;
        detalle.DocumentClass = clase;
        detalle.SupplierPersonId = documento.CounterpartyPersonId!.Value;
        detalle.SupplierPrefix = SupplierInvoiceDetail.NormalizarNumero(proveedor.Prefix);
        detalle.SupplierNumber = SupplierInvoiceDetail.NormalizarNumero(proveedor.Number);
        detalle.Cufe = cufe;
        detalle.IssueDate = proveedor.IssueDate;
        detalle.DueDate = proveedor.DueDate;
        detalle.IsCredit = aCredito;
        detalle.IsElectronic = proveedor.IsElectronic;
        detalle.IsDebitNote = clase == DocumentClass.SupplierNote && pedido.NoteKind == "Debit";
        detalle.IsReleased = false;

        if (detalle.SupplierPrefix.Length > SupplierInvoiceDetail.LargoDelPrefijo || detalle.SupplierNumber.Length > SupplierInvoiceDetail.LargoDelNumero)
            return Result.Failure(new Error("Validation.Invalid", string.Format(CultureInfo.InvariantCulture,
                "El prefijo admite {0} caracteres y el número {1}.", SupplierInvoiceDetail.LargoDelPrefijo, SupplierInvoiceDetail.LargoDelNumero)));

        var duplicado = await ColisionDeFacturaDeProveedor.BuscarAsync(db, detalle, ct);
        return duplicado is null ? Result.Success() : Result.Failure(duplicado);
    }

    private async Task<int> SucursalPrincipalAsync(CancellationToken ct) =>
        await db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => b.Id).FirstOrDefaultAsync(ct);

    private static Result<T> Falla<T>(Error error) => Result.Failure<T>(error);

    private static Result<SaveInventoryDraftRequest> Falla(Error error) => Result.Failure<SaveInventoryDraftRequest>(error);
}
