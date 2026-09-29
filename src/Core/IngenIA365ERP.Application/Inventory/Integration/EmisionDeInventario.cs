using System.Text.Json.Nodes;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// Arma el <b>contenido</b> de los mensajes de Inventario desde el kardex ya escrito (feature 012, T254; FR-014, FR-036;
/// contracts/mensajes.md §5–§6; decisiones-transversales §2.6). No emite: devuelve los records <c>*V1</c> que la estrategia de
/// cada clase entrega al ciclo común, y éste los pasa a <c>EmisorDeMensajes</c>. Cantidades y costos por grupo contable y
/// bodega, <b>sin cuentas</b>: la cuenta la resuelve Contabilidad con la matriz. Cada historia suma aquí su parte (US2 los
/// ajustes y la anulación; US4 el saldo inicial; US9 compras; US10 traslados; US5 ventas). (nuevo)
///
/// <para>
/// Signo (§3): positivo es el efecto natural del mensaje. Una línea <c>Exit</c> de un ajuste negativo lleva cantidad y costo
/// positivos; el costo de una bodega es Σ <c>TotalCost</c> del kardex del documento en esa bodega (ajustes de costo del mismo
/// documento incluidos: un residuo de redondeo o una regularización), así el inventario de Contabilidad mueve lo mismo que el
/// kardex. El grupo contable es el del producto a la fecha de operación (<see cref="GrupoContableALaFecha"/>, US3, T287), no el
/// de hoy: una reclasificación posterior no cambia lo que dice el mensaje de un documento anterior.
/// </para>
/// </summary>
public sealed class EmisionDeInventario(
    IApplicationDbContext db,
    Sales.CreditosAprobadosEnCurso? creditosEnCurso = null,
    RechazoFiscalEnCurso? rechazoEnCurso = null,
    Sales.TrasladoDeVentaEnCurso? traslado = null)
{
    /// <summary>
    /// El caso fiscal de una anulación sin efecto fiscal por un rechazo de la DIAN (T724, T725), o nulo si es una anulación corriente.
    /// </summary>
    public ElectronicInvoicing.Canonical.CasoFiscalDeAnulacion? CasoFiscalDe(Guid anulacionPublicId) => rechazoEnCurso?.CasoDe(anulacionPublicId);

    /// <summary>Las propiedades de un contenido que son importes o cantidades: las que la anulación invierte (§6.9).</summary>
    public static readonly IReadOnlySet<string> ImportesYCantidades = new HashSet<string>(StringComparer.Ordinal)
    {
        "quantityBase", "cost", "grossAmount", "discountAmount", "netAmount", "taxableBase", "amount", "inventoryAmount",
        "soldAmount", "subtotal", "discountTotal", "taxTotal", "withholdingTotal", "total", "amountDue", "base", "taxableUnits",
    };

    /// <summary>La operación de la matriz de cada clase de ajuste (§6.5).</summary>
    public static string OperacionDeAjuste(DocumentClass clase, bool retiroGravado = false) => clase switch
    {
        DocumentClass.PositiveAdjustment => "AjustePositivo",
        DocumentClass.NegativeAdjustment => "AjusteNegativo",
        DocumentClass.InternalConsumption => retiroGravado ? "RetiroGravado" : "ConsumoInterno",
        DocumentClass.WriteOff => "Baja",
        DocumentClass.Assembly => "Ensamble",
        _ => throw new ArgumentOutOfRangeException(nameof(clase), clase, "No es una clase de ajuste."),
    };

    // ------------------------------------------------------------------------------------------ ajustes --

    /// <summary>
    /// <c>AjusteInventarioAprobado</c> de un ajuste positivo, negativo, consumo interno o baja (§6.5): una línea por grupo
    /// contable, bodega y sentido, con las líneas del documento que suman ahí; <c>causeCode</c> = código de la causa.
    /// </summary>
    public async Task<AjusteInventarioAprobadoV1> AjusteAprobadoAsync(InventoryDocument documento, InventoryDocumentType tipo, IEnumerable<KardexEntry> kardex, CancellationToken ct)
    {
        var filas = kardex.ToList();
        var causaId = documento.Lines.Where(l => !l.IsDeleted).Select(l => l.AdjustmentCauseId).FirstOrDefault(c => c is not null);
        var causa = causaId is int c
            ? await db.AdjustmentCauses.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct)
            : null;
        var sentido = ClasesDeDocumento.De(documento.Class).Effect == InventoryEffect.Entry ? KardexEntryKind.Entry : KardexEntryKind.Exit;

        // US11 (T396): el ajuste de un conteo nombra el conteo que lo originó (informativo, no es dependencia; mensajes.md).
        var conteo = documento.Id == 0
            ? null
            : await db.DocumentLinks.AsNoTracking().Where(l => l.TargetDocumentId == documento.Id && l.Kind == DocumentLinkKind.CountAdjustmentOf)
                .Join(db.InventoryDocuments.AsNoTracking(), l => l.SourceDocumentId, d => d.Id, (l, d) => new { d.PublicId, d.Class, d.Prefix, d.Number })
                .FirstOrDefaultAsync(ct);

        return new AjusteInventarioAprobadoV1
        {
            Operation = OperacionDeAjuste(documento.Class, tipo.IsTaxableWithdrawal),
            CauseCode = causa,
            SourceDocument = conteo is null ? null : new DocumentRefV1
            {
                PublicId = conteo.PublicId,
                DocumentClass = conteo.Class,
                Number = Documents.VistaDeDocumentos.NumeroVisible(conteo.Prefix, conteo.Number) ?? string.Empty,
            },
            Lines = await LineasDeCostoAsync(documento, filas, sentido, ct),
        };
    }

    /// <summary>Las líneas <c>CostLineV1</c> de un documento de una sola dirección, agrupadas por grupo contable y bodega.</summary>
    public async Task<IReadOnlyList<CostLineV1>> LineasDeCostoAsync(InventoryDocument documento, IReadOnlyList<KardexEntry> filas, KardexEntryKind sentido, CancellationToken ct)
    {
        if (filas.Count == 0) return [];
        var (grupos, bodegas) = await DimensionesAsync(filas.Select(f => f.ProductId), filas.Select(f => f.WarehouseId), documento.OperationDate, ct);
        var numeroDeLinea = documento.Lines.ToDictionary(l => l.Id, l => l.LineNumber);
        var signo = sentido == KardexEntryKind.Exit ? -1m : 1m;

        return filas
            .GroupBy(f => (Grupo: grupos.GetValueOrDefault(f.ProductId) ?? string.Empty, f.WarehouseId))
            .OrderBy(g => g.Key.Grupo, StringComparer.Ordinal).ThenBy(g => bodegas[g.Key.WarehouseId].Code, StringComparer.Ordinal)
            .Select(g => new CostLineV1
            {
                AccountingGroupCode = g.Key.Grupo,
                WarehouseCode = bodegas[g.Key.WarehouseId].Code,
                WarehouseBehavior = bodegas[g.Key.WarehouseId].Behavior,
                Movement = sentido,
                QuantityBase = signo * g.Sum(f => f.QuantityBase),
                Cost = signo * g.Sum(f => f.TotalCost),
                DocumentLines = g.Select(f => numeroDeLinea.GetValueOrDefault(f.DocumentLineId)).Where(n => n > 0).Distinct().Order().ToList(),
            })
            .ToList();
    }

    // ------------------------------------------------------------------------------------- saldo inicial --

    /// <summary>
    /// <c>SaldoInicialCargado</c> v1 de un documento <c>OpeningBalance</c> (US4, T309; mensajes.md §7.1): informativo, con la fecha
    /// de corte y una línea <c>Entry</c> por grupo contable y bodega, al costo cargado. Sin comprobante: ese valor ya está en los
    /// libros.
    /// </summary>
    public async Task<SaldoInicialCargadoV1> SaldoInicialCargadoAsync(InventoryDocument documento, IEnumerable<KardexEntry> kardex, CancellationToken ct) => new()
    {
        CutoffDate = documento.OperationDate,
        Lines = await LineasDeCostoAsync(documento, kardex.ToList(), KardexEntryKind.Entry, ct),
    };

    // ------------------------------------------------------------------------------------- compras (US9) --

    /// <summary>
    /// <c>CompraRecibida</c> v1 de una recepción (US9, T340; mensajes.md §6.3): una línea <c>Entry</c> por grupo contable y bodega,
    /// al costo de entrada (neto de descuentos y de impuestos descontables, más los que van al costo); la remisión del proveedor.
    /// </summary>
    public async Task<CompraRecibidaV1> CompraRecibidaAsync(InventoryDocument documento, IEnumerable<KardexEntry> kardex, CancellationToken ct) => new()
    {
        SupplierDeliveryReference = documento.ExternalReference,
        Lines = await LineasDeCostoAsync(documento, kardex.ToList(), KardexEntryKind.Entry, ct),
    };

    /// <summary>
    /// <c>DevolucionRegistrada</c> v1 de una devolución a proveedor (US9, T343; mensajes.md §6.8): una línea <c>Exit</c> por grupo y
    /// bodega al costo con que entró (sólo las salidas: la diferencia contra el promedio viaja en <c>AjusteDeCostoReconocido</c>).
    /// </summary>
    public async Task<DevolucionRegistradaV1> DevolucionAProveedorAsync(InventoryDocument documento, IEnumerable<KardexEntry> kardex, CancellationToken ct) => new()
    {
        Operation = "DevolucionAProveedor",
        Lines = await LineasDeCostoAsync(documento, kardex.Where(k => k.Kind != KardexEntryKind.CostAdjustment).ToList(), KardexEntryKind.Exit, ct),
    };

    /// <summary>Una línea de la factura del proveedor para su mensaje: el importe y lo que sumó al costo, y dónde entró (US9). (nuevo)</summary>
    public sealed record LineaDeFacturaDeProveedor(int LineNumber, int ProductId, int? WarehouseId, bool EsServicio, decimal Gross, decimal Discount, decimal Net, decimal TaxAddedToCost);

    /// <summary>
    /// <c>FacturaProveedorRegistrada</c> v1 de una factura o nota del proveedor (US9, T341, T342; mensajes.md §6.4): el documento del
    /// proveedor, las recepciones de las que deriva, una línea por grupo, bodega y tipo (mercancía o servicio), los impuestos y
    /// retenciones por tarifa y los totales. <b>Sin costo</b>. Con <paramref name="signo"/> −1 (nota crédito) todos los importes
    /// van negativos.
    /// </summary>
    public async Task<FacturaProveedorRegistradaV1> FacturaProveedorRegistradaAsync(
        InventoryDocument documento, Domain.Entities.Inventory.Purchasing.SupplierInvoiceDetail detalle, string kind,
        IReadOnlyList<InventoryDocument> recepciones, IReadOnlyList<LineaDeFacturaDeProveedor> lineas,
        IReadOnlyList<Domain.Taxes.RenglonTributario> renglones, decimal signo, CancellationToken ct)
    {
        var (grupos, bodegas) = await DimensionesAsync(lineas.Select(l => l.ProductId), lineas.Select(l => l.WarehouseId).OfType<int>(), documento.OperationDate, ct);
        var definiciones = renglones.Select(r => r.TaxDefinitionId).Distinct().ToList();
        var codigos = await db.TaxDefinitions.AsNoTracking().Where(t => definiciones.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        var conceptosIds = renglones.Select(r => r.WithholdingConceptId).OfType<int>().Distinct().ToList();
        var conceptos = await db.WithholdingConcepts.AsNoTracking().Where(c => conceptosIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);

        return new FacturaProveedorRegistradaV1
        {
            SupplierDocument = new SupplierDocumentV1
            {
                Kind = kind,
                Prefix = string.IsNullOrEmpty(detalle.SupplierPrefix) ? null : detalle.SupplierPrefix,
                Number = detalle.SupplierNumber,
                UniqueCode = detalle.Cufe,
                IssueDate = detalle.IssueDate,
                DueDate = detalle.DueDate,
                PaymentForm = detalle.PaymentForm,
                IsElectronic = detalle.IsElectronic,
            },
            DerivedFrom = recepciones.Select(Referencia).ToList(),
            Lines = lineas
                .GroupBy(l => (Grupo: grupos.GetValueOrDefault(l.ProductId) ?? string.Empty, l.WarehouseId, l.EsServicio))
                .OrderBy(g => g.Key.Grupo, StringComparer.Ordinal).ThenBy(g => g.Key.WarehouseId is int b ? bodegas[b].Code : string.Empty, StringComparer.Ordinal)
                .ThenBy(g => g.Key.EsServicio)
                .Select(g => new SupplierInvoiceLineV1
                {
                    AccountingGroupCode = g.Key.Grupo,
                    WarehouseCode = g.Key.WarehouseId is int b ? bodegas[b].Code : null,
                    LineKind = g.Key.EsServicio ? "Service" : "Goods",
                    GrossAmount = signo * g.Sum(l => l.Gross),
                    DiscountAmount = signo * g.Sum(l => l.Discount),
                    NetAmount = signo * g.Sum(l => l.Net),
                    TaxAddedToCost = signo * g.Sum(l => l.TaxAddedToCost),
                    DocumentLines = g.Select(l => l.LineNumber).Order().ToList(),
                })
                .ToList(),
            Taxes = renglones
                .GroupBy(r => (r.TaxDefinitionId, r.TaxRateCode, r.Treatment, r.WithholdingConceptId, r.MunicipalityDaneCode, r.Kind, r.Rate, r.AmountPerUnit))
                .Select(g => new TaxLineV1
                {
                    TaxCode = codigos.GetValueOrDefault(g.Key.TaxDefinitionId) ?? string.Empty,
                    TaxKind = g.Key.Kind,
                    TaxRateCode = g.Key.TaxRateCode,
                    Rate = g.Key.Rate,
                    AmountPerUnit = g.Key.AmountPerUnit,
                    TaxableUnits = g.Key.AmountPerUnit is null ? null : signo * g.Sum(r => r.TaxableUnits ?? 0m),
                    Treatment = g.Key.Treatment,
                    TaxableBase = signo * g.Sum(r => r.Base),
                    Amount = signo * g.Sum(r => r.Amount),
                    WithholdingConceptCode = g.Key.WithholdingConceptId is int c ? conceptos.GetValueOrDefault(c) : null,
                    MunicipalityDaneCode = g.Key.MunicipalityDaneCode,
                    DocumentLines = g.Select(r => r.Linea).OfType<int>().Distinct().Order().ToList(),
                })
                .ToList(),
            Totals = new SupplierInvoiceTotalsV1
            {
                Subtotal = signo * documento.Subtotal,
                DiscountTotal = signo * documento.DiscountTotal,
                TaxTotal = signo * documento.TaxTotal,
                WithholdingTotal = signo * documento.WithholdingTotal,
                Total = signo * documento.Total,
                AmountPayable = signo * (documento.Total - documento.WithholdingTotal),
            },
        };
    }

    /// <summary>
    /// Un <c>AjusteDeCostoReconocido</c> <c>PriceDifference</c> sobre una recepción (US9, T341; §6.10): lo que quedó en existencia y
    /// lo que pasó a lo vendido, por grupo contable y bodega.
    /// </summary>
    public async Task<AjusteDeCostoReconocidoV1> AjusteDeDiferenciaDePrecioAsync(
        InventoryDocument recepcion, DateOnly fecha, IReadOnlyList<DiferenciaDePrecioRegistrada> diferencias, CancellationToken ct)
    {
        var (grupos, bodegas) = await DimensionesAsync(diferencias.Select(d => d.Pedida.Entrada.ProductId), diferencias.Select(d => d.Pedida.Entrada.WarehouseId), fecha, ct);
        return new AjusteDeCostoReconocidoV1
        {
            Reason = KardexReason.PriceDifference,
            EffectiveDate = fecha,
            AffectedDocument = Referencia(recepcion),
            Lines = diferencias
                .GroupBy(d => (Grupo: grupos.GetValueOrDefault(d.Pedida.Entrada.ProductId) ?? string.Empty, d.Pedida.Entrada.WarehouseId))
                .OrderBy(g => g.Key.Grupo, StringComparer.Ordinal).ThenBy(g => bodegas[g.Key.WarehouseId].Code, StringComparer.Ordinal)
                .Select(g => new CostDifferenceLineV1
                {
                    AccountingGroupCode = g.Key.Grupo,
                    WarehouseCode = bodegas[g.Key.WarehouseId].Code,
                    WarehouseBehavior = bodegas[g.Key.WarehouseId].Behavior,
                    InventoryAmount = g.Sum(d => d.EnExistencia),
                    SoldAmount = g.Sum(d => d.Vendida),
                })
                .ToList(),
        };
    }

    /// <summary>
    /// Un <c>AjusteDeCostoReconocido</c> <c>LandedCost</c> sobre una recepción (US13, T800; §6.10; FR-046): lo que de sus costos
    /// adicionales quedó en existencia y lo que pasó a costo de venta, por grupo contable y bodega. En la anulación, con los signos
    /// contrarios. Lo que no suma cero es la contrapartida: la cuenta de costos por distribuir (contabilidad.md §3.6). (nuevo)
    /// </summary>
    public async Task<AjusteDeCostoReconocidoV1> AjusteDeCostosAdicionalesAsync(
        InventoryDocument recepcion, DateOnly fecha, IReadOnlyList<CostoAdicionalRegistrado> costos, CancellationToken ct)
    {
        var (grupos, bodegas) = await DimensionesAsync(costos.Select(d => d.Pedido.Entrada.ProductId), costos.Select(d => d.Pedido.Entrada.WarehouseId), fecha, ct);
        return new AjusteDeCostoReconocidoV1
        {
            Reason = KardexReason.LandedCost,
            EffectiveDate = fecha,
            AffectedDocument = Referencia(recepcion),
            Lines = costos
                .GroupBy(d => (Grupo: grupos.GetValueOrDefault(d.Pedido.Entrada.ProductId) ?? string.Empty, d.Pedido.Entrada.WarehouseId))
                .OrderBy(g => g.Key.Grupo, StringComparer.Ordinal).ThenBy(g => bodegas[g.Key.WarehouseId].Code, StringComparer.Ordinal)
                .Select(g => new CostDifferenceLineV1
                {
                    AccountingGroupCode = g.Key.Grupo,
                    WarehouseCode = bodegas[g.Key.WarehouseId].Code,
                    WarehouseBehavior = bodegas[g.Key.WarehouseId].Behavior,
                    InventoryAmount = g.Sum(d => d.EnExistencia),
                    SoldAmount = g.Sum(d => d.Vendida),
                })
                .ToList(),
        };
    }

    // ----------------------------------------------------------------------------------- traslados (US10) --

    /// <summary>
    /// <c>TrasladoDespachado</c> v1 de un despacho (US10, T367; mensajes.md §6.6): de la bodega de origen (<c>Operational</c>) a la de
    /// tránsito de su sucursal (<c>Transit</c>), una línea por grupo contable, con la cantidad y el valor que entraron al tránsito.
    /// </summary>
    public async Task<TrasladoDespachadoV1> TrasladoDespachadoAsync(InventoryDocument documento, IReadOnlyList<KardexEntry> kardex, CancellationToken ct)
    {
        var transito = documento.TransitWarehouseId!.Value;
        var codigos = await db.Warehouses.AsNoTracking()
            .Where(w => w.Id == transito || w.Id == documento.DestinationWarehouseId)
            .ToDictionaryAsync(w => w.Id, w => w.Code, ct);
        return new TrasladoDespachadoV1
        {
            TransitWarehouseCode = codigos.GetValueOrDefault(transito) ?? string.Empty,
            DestinationWarehouseCode = documento.DestinationWarehouseId is int d ? codigos.GetValueOrDefault(d) ?? string.Empty : string.Empty,
            Lines = await LineasDeTrasladoAsync(documento, kardex, documento.WarehouseId!.Value, transito, ct),
        };
    }

    /// <summary>
    /// <c>TrasladoRecibido</c> v1 de una recepción de traslado (US10, T367; mensajes.md §6.7): del tránsito (<c>Transit</c>) a la bodega
    /// que recibe (<c>Operational</c>: el destino, o el origen si es la devolución de un faltante), sólo lo recibido, derivado de su
    /// despacho.
    /// </summary>
    public async Task<TrasladoRecibidoV1> TrasladoRecibidoAsync(InventoryDocument documento, InventoryDocument despacho, IReadOnlyList<KardexEntry> kardex, CancellationToken ct)
    {
        var transito = despacho.TransitWarehouseId!.Value;
        var hacia = kardex.Where(k => k.Kind == KardexEntryKind.Entry && k.WarehouseId != transito).Select(k => k.WarehouseId).FirstOrDefault();
        return new TrasladoRecibidoV1
        {
            DerivedFrom = [Referencia(despacho)],
            Lines = hacia == 0 ? [] : await LineasDeTrasladoAsync(documento, kardex, transito, hacia, ct),
        };
    }

    /// <summary>Las <c>TransferCostLineV1</c> de <paramref name="desde"/> a <paramref name="hacia"/>: lo que entró a <paramref name="hacia"/>, por grupo contable.</summary>
    private async Task<IReadOnlyList<TransferCostLineV1>> LineasDeTrasladoAsync(
        InventoryDocument documento, IReadOnlyList<KardexEntry> kardex, int desde, int hacia, CancellationToken ct)
    {
        var entradas = kardex.Where(k => k.WarehouseId == hacia && k.Kind != KardexEntryKind.Exit).ToList();
        if (entradas.Count == 0) return [];
        var (grupos, bodegas) = await DimensionesAsync(entradas.Select(f => f.ProductId), [desde, hacia], documento.OperationDate, ct);
        var sucursales = await db.Warehouses.AsNoTracking().Where(w => w.Id == desde || w.Id == hacia)
            .Join(db.Branches.AsNoTracking(), w => w.BranchId, b => b.Id, (w, b) => new { w.Id, b.PublicId })
            .ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);
        var numeroDeLinea = documento.Lines.ToDictionary(l => l.Id, l => l.LineNumber);

        return entradas
            .GroupBy(f => grupos.GetValueOrDefault(f.ProductId) ?? string.Empty)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TransferCostLineV1
            {
                AccountingGroupCode = g.Key,
                FromWarehouseCode = bodegas[desde].Code,
                FromWarehouseBehavior = bodegas[desde].Behavior,
                FromBranchPublicId = sucursales.GetValueOrDefault(desde),
                ToWarehouseCode = bodegas[hacia].Code,
                ToWarehouseBehavior = bodegas[hacia].Behavior,
                ToBranchPublicId = sucursales.GetValueOrDefault(hacia),
                QuantityBase = g.Where(f => f.Kind == KardexEntryKind.Entry).Sum(f => f.QuantityBase),
                Cost = g.Sum(f => f.TotalCost),
                DocumentLines = g.Select(f => numeroDeLinea.GetValueOrDefault(f.DocumentLineId)).Where(n => n > 0).Distinct().Order().ToList(),
            })
            .ToList();
    }

    // -------------------------------------------------------------------------------------- ventas (US5) --

    /// <summary>
    /// <c>VentaFacturada</c> v1 de una venta (I3, T614; mensajes.md §6.1): ingresos por grupo contable y bodega (nula en servicios), la
    /// foto de impuestos y retenciones (<paramref name="impuestos"/>: <c>Generated</c> y <c>WithholdingSuffered</c>), un
    /// <see cref="PaymentLineV1"/> por pago <c>Received</c> y los totales. Sin costo. Invariantes (§6.1): Σ bruto = subtotal, Σ descuentos,
    /// Σ impuestos generados = taxTotal, Σ retenciones = withholdingTotal, Σ pagos = amountDue.
    /// </summary>
    public async Task<VentaFacturadaV1> VentaFacturadaAsync(InventoryDocument documento, IReadOnlyList<DocumentTaxLine> impuestos,
        IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        var caja = await DeCajaAsync(documento, ct);
        var canal = documento.SalesChannelId is int c ? await db.SalesChannels.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;
        return new VentaFacturadaV1
        {
            SalesChannelCode = canal,
            PointOfSaleCode = caja.PointOfSaleCode,
            CashRegisterCode = caja.CashRegisterCode,
            CashSessionPublicId = caja.CashSessionPublicId,
            Lines = await LineasDeVentaAsync(documento, ct),
            Taxes = await RenglonesDeImpuestoAsync(documento, impuestos, ct),
            Payments = await LineasDePagoAsync(documento, pagos.Where(p => p.Direction == PaymentDirection.Received).ToList(), ct),
            Totals = Totales(documento),
        };
    }

    /// <summary><c>CostoDeVentaReconocido</c> v1 (§6.2): cantidades y costo de lo que salió, <c>movement = Exit</c>, sin base ni impuestos.</summary>
    public async Task<CostoDeVentaReconocidoV1> CostoDeVentaAsync(InventoryDocument documento, IReadOnlyList<KardexEntry> kardex, CancellationToken ct)
    {
        var caja = await DeCajaAsync(documento, ct);
        return new CostoDeVentaReconocidoV1
        {
            PointOfSaleCode = caja.PointOfSaleCode,
            CashSessionPublicId = caja.CashSessionPublicId,
            Lines = await LineasDeCostoAsync(documento, kardex.Where(k => k.Kind != KardexEntryKind.Entry).ToList(), KardexEntryKind.Exit, ct),
        };
    }

    /// <summary>
    /// <c>NotaCreditoEmitida</c> v1 (§6.11): lo que se acredita por grupo y bodega, los impuestos con la foto del original en proporción,
    /// los reintegros (<c>Refunded</c>) y los totales, con la marca de anulación total y de devolución.
    /// </summary>
    public async Task<NotaCreditoEmitidaV1> NotaCreditoAsync(InventoryDocument nota, IReadOnlyList<DocumentTaxLine> impuestos,
        IReadOnlyList<DocumentPayment> reintegros, CancellationToken ct)
    {
        var caja = await DeCajaAsync(nota, ct);
        return new NotaCreditoEmitidaV1
        {
            IsTotalVoid = nota.IsFullReversal,
            WithReturn = nota.ReturnsGoods,
            PointOfSaleCode = caja.PointOfSaleCode,
            CashSessionPublicId = caja.CashSessionPublicId,
            Lines = await LineasDeVentaAsync(nota, ct),
            Taxes = await RenglonesDeImpuestoAsync(nota, impuestos, ct),
            Payments = await LineasDePagoAsync(nota, reintegros.Where(p => p.Direction == PaymentDirection.Refunded).ToList(), ct),
            Totals = Totales(nota),
        };
    }

    /// <summary>
    /// <c>NotaDebitoEmitida</c> v1 (I6, T884; mensajes.md §6.12): el mismo contenido que <c>VentaFacturada</c> —lo que se carga por grupo y
    /// bodega, la foto de impuestos, cómo se cobra (<c>Received</c>) y los totales— sin costo; <c>related</c> es la venta que corrige (lo pone
    /// el sobre del derivado). (nuevo)
    /// </summary>
    public async Task<NotaDebitoEmitidaV1> NotaDebitoAsync(InventoryDocument nota, IReadOnlyList<DocumentTaxLine> impuestos,
        IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        var caja = await DeCajaAsync(nota, ct);
        var canal = nota.SalesChannelId is int c ? await db.SalesChannels.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;
        return new NotaDebitoEmitidaV1
        {
            SalesChannelCode = canal,
            PointOfSaleCode = caja.PointOfSaleCode,
            CashRegisterCode = caja.CashRegisterCode,
            CashSessionPublicId = caja.CashSessionPublicId,
            Lines = await LineasDeVentaAsync(nota, ct),
            Taxes = await RenglonesDeImpuestoAsync(nota, impuestos, ct),
            Payments = await LineasDePagoAsync(nota, pagos.Where(p => p.Direction == PaymentDirection.Received).ToList(), ct),
            Totals = Totales(nota),
        };
    }

    /// <summary>
    /// Lo que una nota débito cobrada con crédito le dice a Cartera (I6, T885; mensajes.md §8.2; contracts/api.md §23.4): por cada pago de
    /// crédito de la nota, un <c>AjusteDeVentaACredito</c> con <c>AdjustmentClass = DebitNote</c>, el monto <b>positivo</b>, las condiciones
    /// del valor nuevo (<c>terms</c>) y el <c>OriginalMessageId</c> de la <c>VentaACreditoRegistrada</c> de la venta que corrige (el mismo medio
    /// primero, si no el primero de la venta), con su mismo sello. Si la venta no fue a crédito —o su crédito no se registró— no hay qué
    /// ajustar: el pago de crédito de la nota se registra como venta a crédito propia (decisiones-transversales T53a). (nuevo)
    /// </summary>
    public async Task<IReadOnlyList<object>> CreditoDeLaNotaDebitoAsync(InventoryDocument nota, InventoryDocument original, IReadOnlyList<DocumentPayment> pagos,
        CancellationToken ct)
    {
        var creditos = pagos.Where(p => p.Direction == PaymentDirection.Received && !p.IsDeleted && p.EsCredito).OrderBy(p => p.LineNumber).ToList();
        if (creditos.Count == 0) return [];
        var deLaVenta = await db.DocumentPayments.AsNoTracking()
            .Where(p => p.DocumentId == original.Id && !p.IsDeleted && p.Direction == PaymentDirection.Received
                && (p.MeansClass == PaymentMeansClass.AssociateCredit || p.MeansClass == PaymentMeansClass.CustomerCredit))
            .OrderBy(p => p.LineNumber).ToListAsync(ct);
        var registradas = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == original.PublicId && m.Type == VentaACreditoRegistradaV1.Type)
            .Select(m => new { m.PublicId, m.OriginEventKey }).ToListAsync(ct);

        var contenidos = new List<object>();
        var sinPar = new List<DocumentPayment>();
        foreach (var p in creditos)
        {
            var par = deLaVenta.FirstOrDefault(o => o.PaymentMeansId == p.PaymentMeansId) ?? deLaVenta.FirstOrDefault();
            var mensaje = par is null
                ? null
                : registradas.FirstOrDefault(m => m.OriginEventKey == IngenIA365ERP.Application.Common.Integration.ClavesDeEvento.ConfirmacionPor(par.PublicId));
            if (par is null || mensaje is null)
            {
                sinPar.Add(p);
                continue;
            }
            contenidos.Add(new AjusteDeVentaACreditoV1
            {
                AdjustmentClass = "DebitNote",
                Amount = p.Amount,
                OriginalMessageId = mensaje.PublicId,
                OriginalCreditPaymentPublicId = par.PublicId,
                OriginalDocument = Referencia(original),
                PaymentMeansCode = p.MeansCode,
                Terms = CondicionesDelCredito(p, nota.OperationDate),
                Reason = nota.Reason,
                AccountsReceivableRecordedBy = par.AccountsReceivableRecordedBy ?? Sales.CreditoEnLaVenta.Contabilidad,
            });
        }
        if (sinPar.Count > 0) contenidos.AddRange(await VentasACreditoAsync(nota, sinPar, ct));
        return contenidos;
    }

    /// <summary><c>DevolucionRegistrada</c> v1 de una devolución de cliente (§6.8): <c>Entry</c> al costo con que salió.</summary>
    public async Task<DevolucionRegistradaV1> DevolucionDeClienteAsync(InventoryDocument nota, IEnumerable<KardexEntry> kardex, CancellationToken ct) => new()
    {
        Operation = "DevolucionDeCliente",
        Lines = await LineasDeCostoAsync(nota, kardex.Where(k => k.Kind == KardexEntryKind.Entry).ToList(), KardexEntryKind.Entry, ct),
    };

    // ------------------------------------------------------------------------------------- Cartera (US6) --

    /// <summary>
    /// <c>VentaACreditoRegistrada</c> v1 (I3, T656; mensajes.md §8.1): una por pago de crédito <c>Received</c> de la venta, con la foto de la
    /// contraparte, el valor financiado, las condiciones del medio (T32), la línea sugerida, la marca «pendiente de validar», el origen del
    /// crédito, la aprobación (nunca el cajero) y el sello <c>accountsReceivableRecordedBy</c>. La clave de cada una es
    /// <c>Confirmation:{paymentPublicId:N}</c> y la pone <see cref="MensajesDelDocumento.Solicitudes"/>.
    /// </summary>
    public async Task<IReadOnlyList<VentaACreditoRegistradaV1>> VentasACreditoAsync(InventoryDocument documento, IReadOnlyList<DocumentPayment> pagos,
        CancellationToken ct)
    {
        var creditos = pagos.Where(p => p.Direction == PaymentDirection.Received && !p.IsDeleted && p.EsCredito).OrderBy(p => p.LineNumber).ToList();
        if (creditos.Count == 0) return [];
        var persona = documento.CounterpartyPersonId is int pid ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct) : null;
        var foto = persona is null ? null : FotoDeLaContraparte.De(documento, persona);
        var caja = await DeCajaAsync(documento, ct);
        var canal = documento.SalesChannelId is int c ? await db.SalesChannels.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;

        var lista = new List<VentaACreditoRegistradaV1>(creditos.Count);
        foreach (var p in creditos)
        {
            lista.Add(new VentaACreditoRegistradaV1
            {
                ThirdPartyKind = p.MeansClass == PaymentMeansClass.AssociateCredit ? "Associate" : "Customer",
                Person = new PartySnapshotV1 { TaxIdType = foto?.DianIdTypeCode ?? string.Empty, TaxId = foto?.TaxId ?? string.Empty, Name = foto?.LegalName ?? string.Empty },
                CreditPayment = new CreditPaymentV1
                {
                    PaymentPublicId = p.PublicId, LineNumber = p.LineNumber, PaymentMeansCode = p.MeansCode, PaymentMeansClass = p.MeansClass, Amount = p.Amount,
                },
                DocumentTotal = documento.Total,
                AmountDue = documento.AmountDue,
                Terms = CondicionesDelCredito(p, documento.OperationDate),
                CreditLineCode = null,
                SuggestedCreditLineCode = p.SuggestedCreditLineCode,
                PendingValidation = p.PendingValidation,
                Origin = p.CreditOrigin ?? CreditOrigin.ProvisionalCredit,
                Approval = await AprobacionDelCreditoAsync(p, ct),
                ConsultationEvidence = null,
                AccountsReceivableRecordedBy = p.AccountsReceivableRecordedBy ?? Sales.CreditoEnLaVenta.Contabilidad,
                PointOfSaleCode = caja.PointOfSaleCode,
                SalesChannelCode = canal,
            });
        }
        return lista;
    }

    /// <summary>
    /// <c>AjusteDeVentaACredito</c> v1 (I3, T656; mensajes.md §8.2): uno por pago de crédito del <paramref name="original"/> afectado. En una
    /// nota, lo reintegrado a ese pago (<c>RefundsPaymentId</c>, o el mismo medio si el reintegro no lo nombra) con signo negativo, clase
    /// <c>Return</c> si devuelve mercancía y <c>CreditNote</c> si no; una nota que reintegra sólo por medios de contado no emite nada. En una
    /// anulación (<paramref name="reintegros"/> nulo), todo el valor financiado en negativo, clase <c>Voiding</c>. Cada uno nombra la
    /// <c>VentaACreditoRegistrada</c> que ajusta y conserva su sello; sin ella (una venta anterior a la entrega) no hay qué ajustar.
    /// I4 (T724, T725): la anulación sin efecto fiscal de un rechazo de la DIAN ajusta con <paramref name="claseDeAnulacion"/>
    /// <c>VoidingByDianRejection</c>; el reemplazo del caso b ajusta con <c>Replacement</c> (<see cref="CreditoDeLaVentaAsync"/>).
    /// </summary>
    public async Task<IReadOnlyList<AjusteDeVentaACreditoV1>> AjustesDeVentaACreditoAsync(InventoryDocument documento, InventoryDocument original,
        IReadOnlyList<DocumentPayment>? reintegros, CancellationToken ct, string? claseDeAnulacion = null)
    {
        var creditos = await db.DocumentPayments.AsNoTracking()
            .Where(p => p.DocumentId == original.Id && !p.IsDeleted && p.Direction == PaymentDirection.Received
                && (p.MeansClass == PaymentMeansClass.AssociateCredit || p.MeansClass == PaymentMeansClass.CustomerCredit))
            .OrderBy(p => p.LineNumber).ToListAsync(ct);
        if (creditos.Count == 0) return [];
        var originales = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == original.PublicId && m.Type == VentaACreditoRegistradaV1.Type)
            .Select(m => new { m.PublicId, m.OriginEventKey }).ToListAsync(ct);

        var clase = reintegros is null ? claseDeAnulacion ?? "Voiding" : documento.ReturnsGoods ? "Return" : "CreditNote";
        var ajustes = new List<AjusteDeVentaACreditoV1>();
        foreach (var p in creditos)
        {
            var clave = IngenIA365ERP.Application.Common.Integration.ClavesDeEvento.ConfirmacionPor(p.PublicId);
            var mensaje = originales.FirstOrDefault(m => m.OriginEventKey == clave);
            if (mensaje is null) continue;
            var monto = reintegros is null
                ? p.Amount
                : reintegros.Where(r => !r.IsDeleted && (r.RefundsPaymentId == p.Id || r.RefundsPaymentId is null && r.PaymentMeansId == p.PaymentMeansId)).Sum(r => r.Amount);
            if (monto == 0m) continue;
            ajustes.Add(new AjusteDeVentaACreditoV1
            {
                AdjustmentClass = clase,
                Amount = -monto,
                OriginalMessageId = mensaje.PublicId,
                OriginalCreditPaymentPublicId = p.PublicId,
                OriginalDocument = Referencia(original),
                PaymentMeansCode = p.MeansCode,
                Reason = documento.Reason,
                AccountsReceivableRecordedBy = p.AccountsReceivableRecordedBy ?? Sales.CreditoEnLaVenta.Contabilidad,
            });
        }
        return ajustes;
    }

    /// <summary>
    /// Lo que una venta le dice a Cartera por su parte a crédito: una <c>VentaACreditoRegistrada</c> por pago de crédito (I3, T656) o, si la
    /// venta es el reemplazo del caso b de un rechazo de la DIAN (I4, T724; mensajes.md §8.2 y §9 punto 8), un <c>AjusteDeVentaACredito</c>
    /// <c>Replacement</c> por pago, con el valor nuevo en positivo y sus condiciones, sobre la venta a crédito del rechazado que la anulación
    /// sin efecto fiscal dejó en cero. Un pago de crédito del reemplazo sin par en el rechazado (el rechazado no tenía ese crédito o su venta a
    /// crédito no se registró) se registra como venta a crédito nueva.
    /// </summary>
    public async Task<IReadOnlyList<object>> CreditoDeLaVentaAsync(InventoryDocument documento, IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        if ((rechazoEnCurso?.ReemplazaA(documento.PublicId) ?? traslado?.DeLaFactura(documento.PublicId)) is not { } rechazadoPublicId)
            return [.. await VentasACreditoAsync(documento, pagos, ct)];

        var creditos = pagos.Where(p => p.Direction == PaymentDirection.Received && !p.IsDeleted && p.EsCredito).OrderBy(p => p.LineNumber).ToList();
        if (creditos.Count == 0) return [];

        var rechazado = await db.InventoryDocuments.AsNoTracking().FirstAsync(d => d.PublicId == rechazadoPublicId, ct);
        var delRechazado = await db.DocumentPayments.AsNoTracking()
            .Where(p => p.DocumentId == rechazado.Id && !p.IsDeleted && p.Direction == PaymentDirection.Received
                && (p.MeansClass == PaymentMeansClass.AssociateCredit || p.MeansClass == PaymentMeansClass.CustomerCredit))
            .OrderBy(p => p.LineNumber).ToListAsync(ct);
        var registradas = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == rechazado.PublicId && m.Type == VentaACreditoRegistradaV1.Type)
            .Select(m => new { m.PublicId, m.OriginEventKey }).ToListAsync(ct);

        var contenidos = new List<object>();
        var sinPar = new List<DocumentPayment>();
        var usados = new HashSet<int>();
        foreach (var p in creditos)
        {
            var par = delRechazado.FirstOrDefault(o => !usados.Contains(o.Id) && o.PaymentMeansId == p.PaymentMeansId)
                ?? delRechazado.FirstOrDefault(o => !usados.Contains(o.Id));
            var mensaje = par is null
                ? null
                : registradas.FirstOrDefault(m => m.OriginEventKey == IngenIA365ERP.Application.Common.Integration.ClavesDeEvento.ConfirmacionPor(par.PublicId));
            if (par is null || mensaje is null)
            {
                sinPar.Add(p);
                continue;
            }
            usados.Add(par.Id);
            contenidos.Add(new AjusteDeVentaACreditoV1
            {
                AdjustmentClass = "Replacement",
                Amount = p.Amount,
                OriginalMessageId = mensaje.PublicId,
                OriginalCreditPaymentPublicId = par.PublicId,
                OriginalDocument = Referencia(rechazado),
                PaymentMeansCode = p.MeansCode,
                Terms = CondicionesDelCredito(p, documento.OperationDate),
                Reason = documento.Reason,
                // El mismo sello del original: nunca cambia dentro de una cadena.
                AccountsReceivableRecordedBy = par.AccountsReceivableRecordedBy ?? Sales.CreditoEnLaVenta.Contabilidad,
            });
        }
        if (sinPar.Count > 0) contenidos.AddRange(await VentasACreditoAsync(documento, sinPar, ct));
        return contenidos;
    }

    /// <summary>Las condiciones del pago de crédito en la forma de §8.1 (días; la periodicidad por nombre).</summary>
    public static CreditTermsV1 CondicionesDelCredito(DocumentPayment p, DateOnly fecha)
    {
        var cuotas = p.InstallmentCount ?? 1;
        var ultimo = p.FinalDueDate ?? fecha.AddDays(p.CreditTermDays ?? 0);
        return new CreditTermsV1
        {
            TermUnit = "Days",
            Term = p.CreditTermDays ?? 0,
            Installments = cuotas,
            Periodicity = cuotas <= 1 ? "SinglePayment" : p.InstallmentPeriodDays switch
            {
                <= 7 => "Weekly",
                <= 15 => "Biweekly",
                _ => "Monthly",
            },
            FirstDueDate = p.FirstDueDate ?? ultimo,
            FinalDueDate = ultimo,
        };
    }

    /// <summary>
    /// La aprobación del crédito (§8.1): la que se está decidiendo en esta petición (el motor registra la decisión después de confirmar) o
    /// la última aprobación registrada de la solicitud del pago. Nula si no la hubo.
    /// </summary>
    private async Task<ApprovalRefV1?> AprobacionDelCreditoAsync(DocumentPayment pago, CancellationToken ct)
    {
        if (creditosEnCurso?.DecisionDe(pago.PublicId) is { } enCurso)
        {
            var usuario = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == enCurso.ApproverUserId)
                .Select(u => new { u.CentralUserId, u.Username }).FirstOrDefaultAsync(ct);
            return new ApprovalRefV1
            {
                ApprovalRequestPublicId = enCurso.RequestPublicId,
                ApprovedBy = new UserRefV1 { CentralUserId = usuario?.CentralUserId, Name = usuario?.Username ?? string.Empty },
                Level = enCurso.Level,
                Method = enCurso.Method,
                DecidedAt = new DateTimeOffset(DateTime.SpecifyKind(enCurso.DecidedAt, DateTimeKind.Utc)),
            };
        }
        if (pago.ApprovalRequestId is not int solicitudId) return null;
        var solicitud = await db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions).FirstOrDefaultAsync(r => r.Id == solicitudId, ct);
        var decision = solicitud?.Decisions.Where(d => d.Decision == Domain.Enums.Approvals.ApprovalDecisionKind.Approve).OrderByDescending(d => d.Level).FirstOrDefault();
        if (solicitud is null || decision is null) return null;
        var central = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == decision.DecidedByUserId).Select(u => u.CentralUserId).FirstOrDefaultAsync(ct);
        return new ApprovalRefV1
        {
            ApprovalRequestPublicId = solicitud.PublicId,
            ApprovedBy = new UserRefV1 { CentralUserId = central, Name = decision.DecidedByName },
            Level = decision.Level,
            Method = decision.Method,
            Reason = decision.Reason ?? string.Empty,
            DecidedAt = new DateTimeOffset(DateTime.SpecifyKind(decision.DecidedAt, DateTimeKind.Utc)),
        };
    }

    // --------------------------------------------------------------------------------------- caja (US5) --

    /// <summary>
    /// <c>MovimientoDeCajaRegistrado</c> v1 (§6.14): el medio que se mueve (o el que sale, en una reclasificación), la caja y la sesión,
    /// el destino (la <c>ReasonCode</c> del rol <c>CajaDestino</c>) y, con <c>Register</c>, la caja y la sesión que reciben.
    /// </summary>
    public async Task<MovimientoDeCajaRegistradoV1> MovimientoDeCajaAsync(InventoryDocument documento, CashMovementDetail detalle, CancellationToken ct)
    {
        var medios = await db.PaymentMeans.AsNoTracking().Where(m => m.Id == detalle.SourcePaymentMeansId || m.Id == detalle.TargetPaymentMeansId)
            .ToDictionaryAsync(m => m.Id, m => new { m.Code, m.Class }, ct);
        var sesiones = await db.CashSessions.AsNoTracking().Where(s => s.Id == detalle.CashSessionId || s.Id == detalle.DestinationCashSessionId)
            .Select(s => new { s.Id, s.PublicId, s.CashRegisterId, s.PointOfSaleId }).ToDictionaryAsync(s => s.Id, ct);
        var origen = sesiones[detalle.CashSessionId];
        var cajas = await db.CashRegisters.AsNoTracking().Where(r => r.Id == origen.CashRegisterId || r.Id == detalle.DestinationCashRegisterId)
            .Select(r => new { r.Id, r.Code, r.PointOfSaleId }).ToDictionaryAsync(r => r.Id, ct);
        var puntosIds = cajas.Values.Select(r => r.PointOfSaleId).Append(origen.PointOfSaleId).Distinct().ToList();
        var puntos = await db.PointsOfSale.AsNoTracking().Where(p => puntosIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var destino = detalle.DestinationCashRegisterId is int dc && cajas.TryGetValue(dc, out var cajaDestino) ? cajaDestino : null;
        return new MovimientoDeCajaRegistradoV1
        {
            MovementKind = detalle.Kind,
            PaymentMeansCode = medios[detalle.SourcePaymentMeansId].Code,
            PaymentMeansClass = medios[detalle.SourcePaymentMeansId].Class,
            DestinationPaymentMeansCode = detalle.TargetPaymentMeansId is int t && medios.TryGetValue(t, out var aDonde) ? aDonde.Code : null,
            PointOfSaleCode = puntos.GetValueOrDefault(origen.PointOfSaleId) ?? string.Empty,
            CashRegisterCode = cajas.TryGetValue(origen.CashRegisterId, out var caja) ? caja.Code : string.Empty,
            CashSessionPublicId = origen.PublicId,
            Destination = detalle.Destination,
            DestinationPointOfSaleCode = destino is null ? null : puntos.GetValueOrDefault(destino.PointOfSaleId),
            DestinationCashRegisterCode = destino?.Code,
            DestinationCashSessionPublicId = detalle.DestinationCashSessionId is int ds && sesiones.TryGetValue(ds, out var recibe) ? recibe.PublicId : null,
            Amount = detalle.Amount,
            Reason = documento.Reason ?? string.Empty,
        };
    }

    /// <summary>
    /// <c>DiferenciaDeArqueoAprobada</c> v1 (§6.15): una línea por medio con diferencia —también la aceptada dentro de la tolerancia—, con
    /// lo esperado, lo contado, la tolerancia copiada y el tratamiento; el cajero con su persona (obligatoria si hay
    /// <c>ShortageToCashier</c>) y la aprobación si la hubo. <c>Difference = Counted − Expected</c>.
    /// </summary>
    public async Task<DiferenciaDeArqueoAprobadaV1> DiferenciaDeArqueoAsync(InventoryDocument documento, IReadOnlyList<CashDocumentLine> lineas,
        ApprovalRefV1? aprobacion, Guid? cajeroCentralUserId, CancellationToken ct)
    {
        var sesionId = documento.CashSessionId ?? throw new InvalidOperationException("La diferencia de arqueo lleva su sesión de caja.");
        var sesion = await db.CashSessions.AsNoTracking().FirstAsync(s => s.Id == sesionId, ct);
        var caja = await db.CashRegisters.AsNoTracking().Where(r => r.Id == sesion.CashRegisterId).Select(r => r.Code).FirstAsync(ct);
        var punto = await db.PointsOfSale.AsNoTracking().Where(p => p.Id == sesion.PointOfSaleId).Select(p => p.Code).FirstAsync(ct);
        var conteoIds = lineas.Select(l => l.CashCountLineId).Distinct().ToList();
        var conteos = await db.CashCountLines.AsNoTracking().Where(l => conteoIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var medioIds = lineas.Select(l => l.PaymentMeansId).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().Where(m => medioIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => new { m.Code, m.Class }, ct);
        var personaId = lineas.Select(l => l.CashierPersonId).FirstOrDefault(p => p is not null) ?? sesion.CashierPersonId;
        Guid? persona = personaId is int pid ? await db.People.AsNoTracking().Where(p => p.Id == pid).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct) : null;
        if (persona is null && lineas.Any(l => l.Treatment == CashDifferenceTreatment.ShortageToCashier))
            throw new InvalidOperationException("Un faltante a cargo del cajero exige la persona del cajero (T50).");

        return new DiferenciaDeArqueoAprobadaV1
        {
            PointOfSaleCode = punto,
            CashRegisterCode = caja,
            CashSessionPublicId = sesion.PublicId,
            Cashier = new CashierV1 { CentralUserId = cajeroCentralUserId, PersonPublicId = persona, Name = sesion.CashierName },
            Approval = aprobacion,
            Lines = lineas.OrderBy(l => l.LineNumber).Select(l =>
            {
                var conteo = conteos.GetValueOrDefault(l.CashCountLineId);
                return new CashCountDifferenceLineV1
                {
                    PaymentMeansCode = medios[l.PaymentMeansId].Code,
                    PaymentMeansClass = medios[l.PaymentMeansId].Class,
                    CountMethod = conteo?.CountMethod ?? CashCountMethod.PhysicalCount,
                    Expected = conteo?.ExpectedAmount ?? 0m,
                    Counted = conteo?.CountedAmount ?? 0m,
                    Difference = conteo?.DifferenceAmount ?? l.Sign * l.Amount,
                    ToleranceAmount = conteo?.ToleranceAmount ?? 0m,
                    WithinTolerance = l.WithinTolerance,
                    Treatment = l.Treatment,
                    Reason = l.Reason,
                };
            }).ToList(),
        };
    }

    // ------------------------------------------------------------------------------------- apoyo de ventas --

    /// <summary>El punto, la caja y la sesión del documento (la <c>BatchScopeKey</c> de <c>CierreDeTurno</c>), si los tiene.</summary>
    private async Task<(string? PointOfSaleCode, string? CashRegisterCode, Guid? CashSessionPublicId)> DeCajaAsync(InventoryDocument documento, CancellationToken ct)
    {
        var punto = documento.PointOfSaleId is int p ? await db.PointsOfSale.AsNoTracking().Where(x => x.Id == p).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;
        var caja = documento.CashRegisterId is int c ? await db.CashRegisters.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;
        Guid? sesion = documento.CashSessionId is int s ? await db.CashSessions.AsNoTracking().Where(x => x.Id == s).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null;
        return (punto, caja, sesion);
    }

    /// <summary>Las <see cref="SalesAmountLineV1"/>: por grupo contable a la fecha y bodega (nula en servicios), sin impuestos.</summary>
    private async Task<IReadOnlyList<SalesAmountLineV1>> LineasDeVentaAsync(InventoryDocument documento, CancellationToken ct)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        if (vivas.Count == 0) return [];
        var productoIds = vivas.Select(l => l.ProductId).Distinct().ToList();
        var servicios = (await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id) && p.Kind == ProductKind.Service).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        var (grupos, bodegas) = await DimensionesAsync(productoIds, documento.WarehouseId is int w ? [w] : [], documento.OperationDate, ct);
        string? bodega = documento.WarehouseId is int b && bodegas.TryGetValue(b, out var d) ? d.Code : null;
        return vivas
            .GroupBy(l => (Grupo: grupos.GetValueOrDefault(l.ProductId) ?? string.Empty, Bodega: servicios.Contains(l.ProductId) ? null : bodega))
            .OrderBy(g => g.Key.Grupo, StringComparer.Ordinal).ThenBy(g => g.Key.Bodega ?? string.Empty, StringComparer.Ordinal)
            .Select(g => new SalesAmountLineV1
            {
                AccountingGroupCode = g.Key.Grupo,
                WarehouseCode = g.Key.Bodega,
                GrossAmount = g.Sum(l => l.GrossAmount),
                DiscountAmount = g.Sum(l => l.DiscountAmount),
                NetAmount = g.Sum(l => l.NetAmount),
                DocumentLines = g.Select(l => l.LineNumber).Order().ToList(),
            })
            .ToList();
    }

    /// <summary>Los <see cref="TaxLineV1"/> de la foto: uno por impuesto, tarifa, tratamiento, concepto y municipio.</summary>
    private async Task<IReadOnlyList<TaxLineV1>> RenglonesDeImpuestoAsync(InventoryDocument documento, IReadOnlyList<DocumentTaxLine> impuestos, CancellationToken ct)
    {
        if (impuestos.Count == 0) return [];
        var definiciones = impuestos.Select(r => r.TaxDefinitionId).Distinct().ToList();
        var codigos = await db.TaxDefinitions.AsNoTracking().Where(t => definiciones.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        var conceptosIds = impuestos.Select(r => r.WithholdingConceptId).OfType<int>().Distinct().ToList();
        var conceptos = await db.WithholdingConcepts.AsNoTracking().Where(c => conceptosIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var numeroDeLinea = documento.Lines.Where(l => l.Id != 0).ToDictionary(l => l.Id, l => l.LineNumber);
        return impuestos
            .GroupBy(r => (r.TaxDefinitionId, r.TaxRateCode, r.Treatment, r.WithholdingConceptId, r.MunicipalityDaneCode, r.Kind, r.Rate, r.AmountPerUnit))
            .Select(g => new TaxLineV1
            {
                TaxCode = codigos.GetValueOrDefault(g.Key.TaxDefinitionId) ?? string.Empty,
                TaxKind = g.Key.Kind,
                TaxRateCode = g.Key.TaxRateCode,
                Rate = g.Key.Rate,
                AmountPerUnit = g.Key.AmountPerUnit,
                TaxableUnits = g.Key.AmountPerUnit is null ? null : g.Sum(r => r.TaxableUnits ?? 0m),
                Treatment = g.Key.Treatment,
                TaxableBase = g.Sum(r => r.Base),
                Amount = g.Sum(r => r.Amount),
                WithholdingConceptCode = g.Key.WithholdingConceptId is int c ? conceptos.GetValueOrDefault(c) : null,
                MunicipalityDaneCode = g.Key.MunicipalityDaneCode,
                DocumentLines = g.Select(r => r.DocumentLineId is int li && numeroDeLinea.TryGetValue(li, out var n) ? n : 0).Where(n => n > 0).Distinct().Order().ToList(),
            })
            .ToList();
    }

    /// <summary>
    /// Un <see cref="PaymentLineV1"/> por pago, con el tercero natural según la clase del medio (§5): el adquirente en tarjetas, el banco en
    /// consignaciones y transferencias, el cliente en créditos; nulo en efectivo y bonos. Nunca el número de la tarjeta.
    /// </summary>
    private async Task<IReadOnlyList<PaymentLineV1>> LineasDePagoAsync(InventoryDocument documento, IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        if (pagos.Count == 0) return [];
        var adquirentes = pagos.Select(p => p.CardAcquirerPersonId).OfType<int>().Distinct().ToList();
        var bancos = pagos.Select(p => p.BankId).OfType<int>().Distinct().ToList();
        var personasDeBanco = await db.Banks.AsNoTracking().Where(b => bancos.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PersonId, ct);
        var personas = adquirentes.Concat(personasDeBanco.Values.OfType<int>()).Concat(documento.CounterpartyPersonId is int c ? [c] : []).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var terminalIds = pagos.Select(p => p.CardTerminalId).OfType<int>().Distinct().ToList();
        var terminales = await db.CardTerminals.AsNoTracking().Where(t => terminalIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code, ct);

        return pagos.OrderBy(p => p.LineNumber).Select(p =>
        {
            int? tercero = p.MeansClass switch
            {
                PaymentMeansClass.CreditCard or PaymentMeansClass.DebitCard => p.CardAcquirerPersonId,
                PaymentMeansClass.BankDeposit or PaymentMeansClass.Transfer => p.BankId is int b ? personasDeBanco.GetValueOrDefault(b) : null,
                PaymentMeansClass.AssociateCredit or PaymentMeansClass.CustomerCredit => documento.CounterpartyPersonId,
                _ => null,
            };
            return new PaymentLineV1
            {
                PaymentPublicId = p.PublicId,
                LineNumber = p.LineNumber,
                PaymentMeansCode = p.MeansCode,
                PaymentMeansClass = p.MeansClass,
                Direction = p.Direction,
                Amount = p.Amount,
                Reference = p.NormalizedReference ?? p.Reference,
                ThirdPartyPersonPublicId = tercero is int t && publicos.TryGetValue(t, out var g) ? g : null,
                CardNetworkCode = p.CardNetworkCode,
                CardAcquirerCode = p.CardAcquirerCode,
                CardTerminalCode = p.CardTerminalId is int ti ? terminales.GetValueOrDefault(ti) : null,
                BatchNumber = p.TerminalBatchNumber,
                PendingValidation = p.PendingValidation,
            };
        }).ToList();
    }

    private static SalesTotalsV1 Totales(InventoryDocument documento) => new()
    {
        Subtotal = documento.Subtotal,
        DiscountTotal = documento.DiscountTotal,
        TaxTotal = documento.TaxTotal,
        WithholdingTotal = documento.WithholdingTotal,
        Total = documento.Total,
        AmountDue = documento.AmountDue,
    };

    // ---------------------------------------------------------------------------------------- anulación --

    /// <summary>
    /// Los contenidos de la anulación (§6.9, §6.10): <c>DocumentoAnulado</c> con el contenido de cada mensaje a Contabilidad del
    /// evento <c>Confirmation</c> del original, con todos los importes y cantidades con signo contrario, y un
    /// <c>AjusteDeCostoReconocido</c> por documento afectado. Si el original no emitió nada a Contabilidad, no hay nada que
    /// anular allá: vacío.
    /// </summary>
    public async Task<IReadOnlyList<object>> AnulacionAsync(
        InventoryDocument anulacion, InventoryDocument original, IReadOnlyList<DiferenciaDeCostoDeAnulacion> diferencias, CancellationToken ct,
        string? fiscalCase = null)
    {
        var mensajes = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == original.PublicId && m.OriginEventKey == IngenIA365ERP.Application.Common.Integration.ClavesDeEvento.Confirmacion)
            .Where(m => db.IntegrationMessageDeliveries.Any(d => d.MessageId == m.Id && d.Destination == IntegrationDestinations.Accounting))
            .OrderBy(m => m.Id)
            .ToListAsync(ct);
        if (mensajes.Count == 0) return [];

        var tipoOriginal = await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.Id == original.DocumentTypeId).Select(t => t.Code).FirstAsync(ct);
        var referencia = Referencia(original);
        var contenidos = new List<object>
        {
            new DocumentoAnuladoV1
            {
                Reason = anulacion.Reason ?? string.Empty,
                FiscalCase = fiscalCase,
                VoidedDocument = referencia,
                VoidedDocumentTypeCode = tipoOriginal,
                VoidedContents = mensajes.Select(m => new VoidedContentV1
                {
                    MessageId = m.PublicId,
                    Type = m.Type,
                    Version = m.Version,
                    OperationDate = m.OperationDate,
                    Content = Invertido(m.PayloadJson),
                }).ToList(),
            },
        };

        foreach (var diferencia in diferencias)
        {
            var afectado = diferencia.AffectedDocumentId == original.Id
                ? original
                : await db.InventoryDocuments.AsNoTracking().FirstAsync(d => d.Id == diferencia.AffectedDocumentId, ct);
            contenidos.Add(await AjusteDeCostoAsync(afectado, diferencia.Reason, anulacion.OperationDate, diferencia.Lineas, ct));
        }
        return contenidos;
    }

    /// <summary>
    /// Un <c>AjusteDeCostoReconocido</c> sobre el documento afectado (§6.10): la diferencia por grupo contable y bodega. Las
    /// diferencias de una anulación quedan en la existencia del ámbito (<c>inventoryAmount</c>): no corrigen lo ya vendido.
    /// </summary>
    public async Task<AjusteDeCostoReconocidoV1> AjusteDeCostoAsync(
        InventoryDocument afectado, KardexReason motivo, DateOnly fecha, IReadOnlyList<KardexEntry> lineas, CancellationToken ct)
    {
        var (grupos, bodegas) = await DimensionesAsync(lineas.Select(f => f.ProductId), lineas.Select(f => f.WarehouseId), fecha, ct);
        return new AjusteDeCostoReconocidoV1
        {
            Reason = motivo,
            EffectiveDate = fecha,
            AffectedDocument = Referencia(afectado),
            Lines = lineas
                .GroupBy(f => (Grupo: grupos.GetValueOrDefault(f.ProductId) ?? string.Empty, f.WarehouseId))
                .OrderBy(g => g.Key.Grupo, StringComparer.Ordinal).ThenBy(g => bodegas[g.Key.WarehouseId].Code, StringComparer.Ordinal)
                .Select(g => new CostDifferenceLineV1
                {
                    AccountingGroupCode = g.Key.Grupo,
                    WarehouseCode = bodegas[g.Key.WarehouseId].Code,
                    WarehouseBehavior = bodegas[g.Key.WarehouseId].Behavior,
                    InventoryAmount = g.Sum(f => f.TotalCost),
                    SoldAmount = 0m,
                })
                .ToList(),
        };
    }

    // ------------------------------------------------------------------------------- retroactivo (US3) --

    /// <summary>
    /// Un <c>AjusteDeCostoReconocido</c> por documento afectado por el retroactivo mínimo (US3, T285; §6.10; FR-045): la diferencia
    /// <c>Retroactive</c> por grupo contable y bodega, partida en <c>inventoryAmount</c> (lo que sigue en existencia) y
    /// <c>soldAmount</c> (lo vendido o consumido), fechado en las líneas del kardex. Sólo los afectados que emitieron a
    /// Contabilidad: cada parte sigue el destino del mensaje de ése (<c>Confirmation:{afectado:N}</c>), y sin mensaje no hay
    /// destino que seguir. Lo agrega a sus mensajes la estrategia de la clase que lo causa (saldo inicial, ajuste de conteo).
    /// </summary>
    public async Task<IReadOnlyList<AjusteDeCostoReconocidoV1>> AjustesRetroactivosAsync(RegistroHecho hecho, CancellationToken ct)
    {
        if (hecho.AjustesRetroactivos.Count == 0) return [];
        var afectados = hecho.AjustesRetroactivos.Select(a => a.AffectedDocumentId).Distinct().ToList();
        var documentos = await db.InventoryDocuments.AsNoTracking().Where(d => afectados.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var publicos = documentos.Values.Select(d => d.PublicId).ToList();
        var conMensaje = (await db.IntegrationMessages.AsNoTracking()
                .Where(m => publicos.Contains(m.OriginPublicId))
                .Where(m => db.IntegrationMessageDeliveries.Any(e => e.MessageId == m.Id && e.Destination == IntegrationDestinations.Accounting))
                .Select(m => m.OriginPublicId).Distinct().ToListAsync(ct))
            .ToHashSet();

        var contenidos = new List<AjusteDeCostoReconocidoV1>();
        foreach (var ajuste in hecho.AjustesRetroactivos.OrderBy(a => a.Lineas.Min(l => l.Fila.OperationDate)).ThenBy(a => a.AffectedDocumentId))
        {
            if (!documentos.TryGetValue(ajuste.AffectedDocumentId, out var afectado) || !conMensaje.Contains(afectado.PublicId)) continue;
            var fecha = ajuste.Lineas.Min(l => l.Fila.OperationDate);
            var filas = ajuste.Lineas.Select(l => l.Fila).ToList();
            var (grupos, bodegas) = await DimensionesAsync(filas.Select(f => f.ProductId), filas.Select(f => f.WarehouseId), fecha, ct);
            contenidos.Add(new AjusteDeCostoReconocidoV1
            {
                Reason = KardexReason.Retroactive,
                EffectiveDate = fecha,
                AffectedDocument = Referencia(afectado),
                Lines = ajuste.Lineas
                    .GroupBy(l => (Grupo: grupos.GetValueOrDefault(l.Fila.ProductId) ?? string.Empty, l.Fila.WarehouseId))
                    .OrderBy(g => g.Key.Grupo, StringComparer.Ordinal).ThenBy(g => bodegas[g.Key.WarehouseId].Code, StringComparer.Ordinal)
                    .Select(g => new CostDifferenceLineV1
                    {
                        AccountingGroupCode = g.Key.Grupo,
                        WarehouseCode = bodegas[g.Key.WarehouseId].Code,
                        WarehouseBehavior = bodegas[g.Key.WarehouseId].Behavior,
                        InventoryAmount = g.Where(l => l.Porcion == Domain.Inventory.Costing.PorcionDelAjuste.EnExistencia).Sum(l => l.Fila.TotalCost),
                        SoldAmount = g.Where(l => l.Porcion == Domain.Inventory.Costing.PorcionDelAjuste.Vendida).Sum(l => l.Fila.TotalCost),
                    })
                    .ToList(),
            });
        }
        return contenidos;
    }

    /// <summary>
    /// El contenido de un mensaje con todos sus importes y cantidades con el signo contrario (§6.9), <b>también el de los pagos</b>
    /// (mensajes.md §3: su sentido va en <c>direction</c>, que no cambia; su <c>amount</c> sí se invierte). Las tarifas, los números de
    /// línea y los enums no cambian. Hasta el 2026-09-27 los pagos se saltaban y el espejo de una venta descuadraba (e2e T570).
    /// </summary>
    public static JsonNode? Invertido(string payloadJson)
    {
        var nodo = JsonNode.Parse(payloadJson);
        Invertir(nodo);
        return nodo;

        static void Invertir(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject objeto:
                    foreach (var (nombre, valor) in objeto.ToList())
                    {
                        if (valor is JsonValue v && ImportesYCantidades.Contains(nombre) && v.TryGetValue<decimal>(out var numero))
                            objeto[nombre] = JsonValue.Create(-numero);
                        else
                            Invertir(valor);
                    }
                    break;
                case JsonArray arreglo:
                    foreach (var elemento in arreglo) Invertir(elemento);
                    break;
            }
        }
    }

    /// <summary>La referencia a un documento para los mensajes: PublicId, clase y número visible.</summary>
    public static DocumentRefV1 Referencia(InventoryDocument documento) => new()
    {
        PublicId = documento.PublicId,
        DocumentClass = documento.Class,
        Number = VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number) ?? string.Empty,
    };

    private async Task<(Dictionary<int, string?> Grupos, Dictionary<int, (string Code, WarehouseBehavior Behavior)> Bodegas)> DimensionesAsync(
        IEnumerable<int> productos, IEnumerable<int> bodegas, DateOnly fecha, CancellationToken ct)
    {
        var productoIds = productos.Distinct().ToList();
        var bodegaIds = bodegas.Distinct().ToList();
        var grupos = new Dictionary<int, string?>(await GrupoContableALaFecha.CodigosAsync(db, productoIds, fecha, ct));
        var deBodegas = await db.Warehouses.AsNoTracking().Where(w => bodegaIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Code, w.Behavior })
            .ToDictionaryAsync(w => w.Id, w => (w.Code, w.Behavior), ct);
        return (grupos, deBodegas);
    }
}
