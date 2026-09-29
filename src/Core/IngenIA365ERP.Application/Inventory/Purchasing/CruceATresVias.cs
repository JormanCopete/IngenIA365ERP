using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Una línea de la factura del proveedor cruzada contra su recepción y la línea de orden de esa recepción (nula sin orden): lo que
/// entró a <see cref="CruceDeCompra.Cruzar"/> y lo que devolvió. (nuevo)
/// </summary>
public sealed record LineaCruzada(
    InventoryDocumentLine LineaDeFactura,
    int ReceiptLineId,
    int ReceiptDocumentId,
    int? OrderLineId,
    LineaAlCruce Linea,
    ResultadoDelCruce Resultado);

/// <summary>
/// El cruce a tres vías de la factura del proveedor en su confirmación (feature 012, I5, T794, T796; FR-050; contracts/api.md §14.9;
/// data-model §9.6, §21; decisiones-transversales §3 T42a y T42d). Sólo opera cuando alguna recepción enlazada a la factura viene de
/// una orden (vínculo <c>FromOrder</c>); sin orden la factura sigue el cruce de dos vías de I1 (E6) y no escribe nada. (nuevo)
/// <list type="number">
/// <item><see cref="CruzarAsync"/> (paso 2 del flujo canónico, sin escribir): por línea, lo ordenado, lo recibido no facturado, lo
/// facturado y los tres precios por unidad base, con las tolerancias vigentes a la fecha de la factura, por el motor puro
/// <see cref="CruceDeCompra"/>;</item>
/// <item><see cref="DarDeBajaAnterioresAsync"/>: en un intento nuevo (la factura está en borrador), las filas del intento anterior
/// quedan de baja lógica y sus solicitudes pendientes se invalidan;</item>
/// <item><see cref="SolicitarAsync"/> (paso de aprobaciones, también en la reentrada): escribe <c>INV_PurchaseMatchLines</c> si este
/// intento todavía no las tiene y pide por <see cref="IMotorDeAprobaciones.SolicitarAsync"/> <b>una solicitud por línea retenida</b>
/// —sujeto <c>PurchaseMatchException</c>, <c>SourceType = PurchaseMatchLine</c>, la política del sujeto para el tipo de la factura o,
/// sin política, un nivel con <c>Inventory.Purchases.Approve</c> (regla fija del motor)—; devuelve la primera pendiente, que deja la
/// factura en <c>PendingApproval</c> sin número.</item>
/// </list>
/// Nunca guarda. La decisión de cada solicitud la toma <see cref="DecisionDeCruce"/>.
/// </summary>
public sealed class CruceATresVias(
    IApplicationDbContext db,
    IMotorDeAprobaciones motor,
    ILectorDeParametros parametros,
    VinculosDeCompra vinculos,
    IDateTimeService reloj)
{
    /// <summary>Decimales de un precio por unidad base (<c>PrecisionDeInventario.PrecioUnitario</c>, 18,6).</summary>
    public const int DecimalesDePrecio = 6;

    /// <summary>Por línea de recepción, la línea de orden que recibió (vínculos <c>FromOrder</c> vivos).</summary>
    public async Task<IReadOnlyDictionary<int, int>> OrdenDeLasRecepcionesAsync(IReadOnlyCollection<int> lineasDeRecepcion, CancellationToken ct)
    {
        if (lineasDeRecepcion.Count == 0) return new Dictionary<int, int>();
        var pares = await (
                from x in db.DocumentLineLinks.AsNoTracking()
                join l in db.DocumentLinks.AsNoTracking() on x.DocumentLinkId equals l.Id
                where lineasDeRecepcion.Contains(x.TargetLineId) && !x.IsDeleted && !l.IsDeleted && l.Kind == DocumentLinkKind.FromOrder
                select new { x.TargetLineId, x.SourceLineId })
            .ToListAsync(ct);
        return pares.GroupBy(p => p.TargetLineId).ToDictionary(g => g.Key, g => g.First().SourceLineId);
    }

    /// <summary>
    /// El cruce de cada línea de la factura enlazada a una recepción, sin escribir. Vacío si ninguna recepción enlazada viene de una
    /// orden. Falla sólo si las tolerancias vigentes no se pueden leer.
    /// </summary>
    public async Task<Result<IReadOnlyList<LineaCruzada>>> CruzarAsync(InventoryDocument factura, CancellationToken ct)
    {
        var pares = await vinculos.DeAsync(factura, DocumentLinkKind.InvoiceOfReceipt, ct);
        if (pares.Count == 0) return Result.Success<IReadOnlyList<LineaCruzada>>([]);
        var lineasDeRecepcion = pares.Select(p => p.SourceLineId).Distinct().ToList();
        var ordenDe = await OrdenDeLasRecepcionesAsync(lineasDeRecepcion, ct);
        if (ordenDe.Count == 0) return Result.Success<IReadOnlyList<LineaCruzada>>([]);

        var tolerancias = await ToleranciasVigentesDeCompra.LeerAsync(parametros, factura.OperationDate, ct);
        if (tolerancias.IsFailure) return Result.Failure<IReadOnlyList<LineaCruzada>>(tolerancias.Error);
        var montos = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.RedondeoMontos, factura.OperationDate, ct: ct);
        var redondeo = montos.IsSuccess ? Redondeo.MontosDesde(montos.Value.Texto) : RedondeoDeMontos.Centavo;

        var idsDeOrigen = lineasDeRecepcion.Concat(ordenDe.Values).Distinct().ToList();
        var origenes = await db.InventoryDocumentLines.AsNoTracking().Where(l => idsDeOrigen.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var consumo = await vinculos.ConsumoAsync(lineasDeRecepcion, factura.Id, ct);
        var lineas = factura.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        var usadoEnEsta = new Dictionary<int, decimal>();

        var cruzadas = new List<LineaCruzada>();
        foreach (var (lineaDeRecepcion, lineaDeFactura, _, recepcionId) in pares.OrderBy(p => lineas.TryGetValue(p.TargetLineId, out var l) ? l.LineNumber : int.MaxValue))
        {
            if (!lineas.TryGetValue(lineaDeFactura, out var linea) || !origenes.TryGetValue(lineaDeRecepcion, out var recibida)) continue;
            if (linea.QuantityBase <= 0m) continue;
            InventoryDocumentLine? ordenada = ordenDe.TryGetValue(lineaDeRecepcion, out var o) ? origenes.GetValueOrDefault(o) : null;

            var hecho = consumo.GetValueOrDefault(lineaDeRecepcion) ?? new ConsumoDeRecepcion(0m, 0m);
            var disponible = recibida.QuantityBase - hecho.Facturado - hecho.Devuelto - usadoEnEsta.GetValueOrDefault(lineaDeRecepcion);
            usadoEnEsta[lineaDeRecepcion] = usadoEnEsta.GetValueOrDefault(lineaDeRecepcion) + linea.QuantityBase;

            var alCruce = new LineaAlCruce(
                ordenada?.QuantityBase,
                Math.Max(0m, disponible),
                linea.QuantityBase,
                ordenada is null ? null : PrecioPorUnidadBase(ordenada),
                PrecioPorUnidadBase(recibida),
                PrecioPorUnidadBase(linea));
            cruzadas.Add(new LineaCruzada(linea, lineaDeRecepcion, recepcionId, ordenada?.Id, alCruce,
                CruceDeCompra.Cruzar(alCruce, tolerancias.Value, redondeo)));
        }
        return Result.Success<IReadOnlyList<LineaCruzada>>(cruzadas);
    }

    /// <summary>El precio neto de descuento por unidad base de una línea (orden, recepción o factura).</summary>
    public static decimal PrecioPorUnidadBase(InventoryDocumentLine linea) =>
        linea.QuantityBase == 0m ? 0m : Math.Round(linea.NetAmount / linea.QuantityBase, DecimalesDePrecio, MidpointRounding.AwayFromZero);

    /// <summary>Las filas vivas del cruce de la factura (seguidas; las dadas de baja en esta petición ya no cuentan).</summary>
    public async Task<List<PurchaseMatchLine>> VivasAsync(InventoryDocument factura, CancellationToken ct)
    {
        var guardadas = factura.Id == 0 ? [] : await db.PurchaseMatchLines.Where(m => m.InvoiceDocumentId == factura.Id && !m.IsDeleted).ToListAsync(ct);
        return guardadas.Concat(db.PurchaseMatchLines.Local.Where(m => m.Id == 0 && m.InvoiceDocumentId == factura.Id))
            .Where(m => !m.IsDeleted).Distinct().OrderBy(m => m.InvoiceLineId).ToList();
    }

    /// <summary>Un intento nuevo: las filas del anterior quedan de baja lógica y sus solicitudes pendientes, canceladas.</summary>
    public async Task DarDeBajaAnterioresAsync(InventoryDocument factura, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        foreach (var fila in await VivasAsync(factura, ct))
        {
            if (fila.EstaRetenida)
                await motor.InvalidarAsync(ApprovalSourceTypes.PurchaseMatchLine, fila.PublicId, ApprovalSubjects.PurchaseMatchException, ct);
            fila.IsDeleted = true;
            fila.DeletedAt = ahora;
        }
    }

    /// <summary>
    /// Escribe las filas del intento (si todavía no las tiene) y pide la aprobación de cada línea retenida que no la tenga pendiente.
    /// Devuelve la primera solicitud pendiente, o nula si nada queda retenido.
    /// </summary>
    public async Task<Result<ApprovalRequest?>> SolicitarAsync(
        InventoryDocument factura, InventoryDocumentType tipo, IReadOnlyList<LineaCruzada> cruzadas, CancellationToken ct)
    {
        if (cruzadas.Count == 0) return Result.Success<ApprovalRequest?>(null);
        var filas = await VivasAsync(factura, ct);
        if (filas.Count == 0)
        {
            foreach (var c in cruzadas)
            {
                var fila = PurchaseMatchLine.Registrar(factura.Id, c.LineaDeFactura.Id, c.OrderLineId, c.Linea, c.Resultado);
                db.PurchaseMatchLines.Add(fila);
                filas.Add(fila);
            }
        }

        var retenidas = filas.Where(f => f.EstaRetenida).ToList();
        if (retenidas.Count == 0) return Result.Success<ApprovalRequest?>(null);

        var idsDeSolicitud = retenidas.Select(f => f.ApprovalRequestPublicId).OfType<Guid>().ToList();
        var existentes = await db.ApprovalRequests.Where(r => idsDeSolicitud.Contains(r.PublicId)).ToListAsync(ct);
        existentes.AddRange(db.ApprovalRequests.Local.Where(r => r.Id == 0 && idsDeSolicitud.Contains(r.PublicId) && !existentes.Contains(r)));

        var porLinea = cruzadas.ToDictionary(c => c.LineaDeFactura.Id);
        var recepciones = cruzadas.Select(c => c.ReceiptDocumentId).Distinct().ToList();
        var bodegaDeRecepcion = await (from d in db.InventoryDocuments.AsNoTracking()
                                       join w in db.Warehouses.AsNoTracking() on d.WarehouseId equals w.Id
                                       where recepciones.Contains(d.Id)
                                       select new { d.Id, w.PublicId }).ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);
        var creador = factura.CreatedByUserId;

        ApprovalRequest? primera = null;
        foreach (var fila in retenidas)
        {
            if (fila.ApprovalRequestPublicId is { } pedida && existentes.FirstOrDefault(r => r.PublicId == pedida) is { Status: ApprovalRequestStatus.Pending } pendiente)
            {
                primera ??= pendiente;
                continue;
            }

            var cruzada = porLinea.GetValueOrDefault(fila.InvoiceLineId);
            var numero = cruzada?.LineaDeFactura.LineNumber ?? 0;
            var solicitada = await motor.SolicitarAsync(new SolicitudDeAprobacion(
                ApprovalSubjects.PurchaseMatchException,
                ApprovalSourceTypes.PurchaseMatchLine,
                fila.PublicId,
                Etiqueta(tipo, numero, fila.Reasons),
                tipo.PublicId,
                cruzada is null ? null : bodegaDeRecepcion.GetValueOrDefault(cruzada.ReceiptDocumentId),
                null,
                Monto(fila),
                factura.OperationDate,
                creador,
                [],
                Huella(factura, fila),
                null), ct);
            if (solicitada.IsFailure) return Result.Failure<ApprovalRequest?>(solicitada.Error);

            if (solicitada.Value is { } nueva)
            {
                fila.AsignarSolicitud(nueva.PublicId);
                primera ??= nueva;
                continue;
            }

            // La política vigente no pide aprobación para este monto: la excepción de precio pasa sola; la de cantidad nunca (T796).
            if (!fila.Aprobable)
                return Result.Failure<ApprovalRequest?>(ErroresDeCompras.QuantityNotApprovable(numero, fila.ReceivedNotInvoicedQuantity, fila.InvoicedQuantity));
            fila.Aprobar();
        }
        return Result.Success(primera);
    }

    /// <summary>
    /// El monto que evalúa la política de la excepción: la diferencia de precio en valor absoluto más lo facturado de más al precio
    /// facturado (decisiones-transversales §3 T42d).
    /// </summary>
    public static decimal Monto(PurchaseMatchLine fila) =>
        Math.Abs(fila.PriceDifferenceAmount)
        + Math.Round(fila.QuantityDifference * (fila.InvoicedUnitPrice ?? 0m), 2, MidpointRounding.AwayFromZero);

    /// <summary>La huella de lo que se aprueba de una línea del cruce: si la factura o el cruce cambian, la aprobación ya no vale.</summary>
    public static string Huella(InventoryDocument factura, PurchaseMatchLine fila) => HuellaDeOperacion.Calcular("PurchaseMatchLine", new
    {
        Factura = factura.PublicId,
        Fila = fila.PublicId,
        fila.InvoiceLineId,
        fila.OrderLineId,
        fila.OrderedQuantity,
        fila.ReceivedNotInvoicedQuantity,
        fila.InvoicedQuantity,
        fila.OrderedUnitPrice,
        fila.InvoicedUnitPrice,
        fila.Reasons,
    });

    private static string Etiqueta(InventoryDocumentType tipo, int linea, string? razones)
    {
        var motivo = razones switch
        {
            null => "diferencia",
            _ => string.Join(" y ", razones.Split(',').Select(r => r == CruceDeCompra.RazonCantidad ? "cantidad" : r == CruceDeCompra.RazonPrecio ? "precio" : r)),
        };
        var texto = $"Cruce {tipo.Code} · línea {linea} · {motivo}";
        return texto.Length > 80 ? texto[..80] : texto;
    }
}
