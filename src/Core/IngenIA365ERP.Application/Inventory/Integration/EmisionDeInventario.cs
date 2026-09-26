using System.Text.Json.Nodes;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
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
public sealed class EmisionDeInventario(IApplicationDbContext db)
{
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

        return new AjusteInventarioAprobadoV1
        {
            Operation = OperacionDeAjuste(documento.Class, tipo.IsTaxableWithdrawal),
            CauseCode = causa,
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

    // ---------------------------------------------------------------------------------------- anulación --

    /// <summary>
    /// Los contenidos de la anulación (§6.9, §6.10): <c>DocumentoAnulado</c> con el contenido de cada mensaje a Contabilidad del
    /// evento <c>Confirmation</c> del original, con todos los importes y cantidades con signo contrario, y un
    /// <c>AjusteDeCostoReconocido</c> por documento afectado. Si el original no emitió nada a Contabilidad, no hay nada que
    /// anular allá: vacío.
    /// </summary>
    public async Task<IReadOnlyList<object>> AnulacionAsync(
        InventoryDocument anulacion, InventoryDocument original, IReadOnlyList<DiferenciaDeCostoDeAnulacion> diferencias, CancellationToken ct)
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
    /// El contenido de un mensaje con todos sus importes y cantidades con el signo contrario (§6.9). Las tarifas, los números
    /// de línea, los enums y los pagos (que llevan el sentido en <c>direction</c>, no en el signo) no cambian.
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
                        if (nombre == "payments") continue;
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
