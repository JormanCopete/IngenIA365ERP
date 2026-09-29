using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Inventory.Tracking;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>Lo que una línea del borrador pide de seguimiento: el lote (con su vencimiento) y la serie, como los digitó la persona. (nuevo)</summary>
public sealed record SeguimientoPedido(InventoryDocumentLine Linea, string? LotCode, DateOnly? ExpiryDate, string? SerialNumber);

/// <summary>El veredicto del seguimiento de un documento: lo que impide confirmarlo y lo que sólo se avisa (<c>warnings[]</c>). (nuevo)</summary>
public sealed record VeredictoDeSeguimiento(IReadOnlyList<Error> Errores, IReadOnlyList<Error> Avisos)
{
    public static VeredictoDeSeguimiento Ninguno { get; } = new([], []);
}

/// <summary>
/// Las reglas de lote, vencimiento y serie de las líneas de un documento (feature 012, I6, T923; FR-026; US15-4, US15-5; data-model
/// §1.11, §13; contracts/api.md §9.3). Tres momentos:
/// <list type="number">
/// <item><b>al guardar el borrador</b> (<see cref="ResolverAsync"/>): el código de lote y el número de serie que digitó la persona se
/// vuelven <c>LotId</c>/<c>SerialId</c> de la línea. En una línea que <b>entra</b> el lote nace si no existe —con su vencimiento; uno
/// que ya existe con otra fecha es <c>Inventory.Lot.ExpiryMismatch</c>— y la serie se registra sin proyección (el índice único
/// <c>(ProductId, SerialNumber)</c> es el árbitro entre dos borradores que la crean a la vez); en una que <b>sale</b> el lote y la serie
/// tienen que existir (<c>.NotFound</c>). Un producto que no los controla no los admite (<c>.NotTracked</c>);</item>
/// <item><b>al confirmar, fuera del cerrojo</b> (<see cref="EvaluarAsync"/>, paso 1 del flujo §1.3; al guardar, las mismas reglas vuelven
/// en <c>warnings[]</c>): una entrada con lote exige el lote (<c>Inventory.Lot.Required</c>) y, si controla vencimiento, su fecha; una
/// serie es una unidad y no entra si ya está en existencia (<c>Inventory.Serial.AlreadyInStock</c>) ni sale si no está en la bodega
/// (<c>.NotInStock</c>); en las clases de venta (<see cref="Kardex.RegistroDeKardex.ClasesDeVenta"/>) manda <c>Ventas.LoteVencido</c>:
/// con <c>Bloquear</c> un lote vencido —el elegido, o el único que alcanzaría por la sugerencia— es <c>Inventory.Lot.Expired</c>; con
/// <c>Advertir</c>, un aviso;</item>
/// <item><b>dentro del cerrojo</b> lo repite <see cref="Kardex.RegistroDeKardex"/> sobre la existencia bloqueada (el reparto FEFO de las
/// salidas sin lote, la existencia por lote, la proyección de la serie): es el que decide.</item>
/// </list>
/// Sólo mira las clases cuyas líneas digita la persona con su lote o su serie (<see cref="ClasesConSeguimientoEnLaLinea"/>): las que
/// siguen a un origen (devoluciones, notas, recepción de traslado, anulación) toman el lote y la serie de la fila de origen en el
/// registro. (nuevo)
/// </summary>
public sealed class ReglasDeSeguimiento(IApplicationDbContext db, ILectorDeParametros parametros, IDateTimeService reloj)
{
    /// <summary>Las clases cuyas líneas llevan el lote o la serie que digitó la persona.</summary>
    public static readonly IReadOnlySet<DocumentClass> ClasesConSeguimientoEnLaLinea = new HashSet<DocumentClass>
    {
        DocumentClass.PurchaseReceipt, DocumentClass.PositiveAdjustment, DocumentClass.OpeningBalance,
        DocumentClass.NegativeAdjustment, DocumentClass.InternalConsumption, DocumentClass.WriteOff, DocumentClass.Assembly,
        DocumentClass.TransferDispatch, DocumentClass.LocationMove,
        DocumentClass.Shipment, DocumentClass.SalesInvoice, DocumentClass.PosEquivalentDocument, DocumentClass.NonElectronicSalesReceipt,
    };

    /// <summary>El valor de <c>Ventas.LoteVencido</c> a la fecha; sin poder leerlo (una entrega anterior a I6), el defecto seguro <c>Bloquear</c>.</summary>
    public static async Task<PoliticaDeLoteVencido> PoliticaAsync(ILectorDeParametros parametros, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.VentasLoteVencido, fecha, ct: ct);
        return leido.IsSuccess && string.Equals(leido.Value.Texto, nameof(PoliticaDeLoteVencido.Advertir), StringComparison.OrdinalIgnoreCase)
            ? PoliticaDeLoteVencido.Advertir
            : PoliticaDeLoteVencido.Bloquear;
    }

    /// <summary>
    /// ¿La línea entra? Las clases de entrada sí; en un ensamble, sólo la línea del kit; las de salida, traslado y movimiento entre
    /// ubicaciones (el lote es el del origen), no.
    /// </summary>
    public static bool Entra(DocumentClass clase, ProductKind claseDelProducto) => ClasesDeDocumento.De(clase).Effect switch
    {
        InventoryEffect.Entry => true,
        InventoryEffect.Both => clase == DocumentClass.Assembly && claseDelProducto == ProductKind.Kit,
        _ => false,
    };

    // ------------------------------------------------------------------------------------------ al guardar --

    /// <summary>
    /// Convierte el lote y la serie digitados en <c>LotId</c>/<c>SerialId</c> de cada línea (paso 1 de la clase). Crea el lote o la serie
    /// de una entrada que todavía no existe (se guardan con el borrador). Falla con el primer error; quien llama descarta lo del intento.
    /// </summary>
    public async Task<Result> ResolverAsync(InventoryDocument documento, IReadOnlyList<SeguimientoPedido> pedidos, CancellationToken ct)
    {
        if (pedidos.Count == 0) return Result.Success();
        var ids = pedidos.Select(p => p.Linea.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.Kind, p.TracksLot, p.TracksSerial, p.TracksExpiry }).ToDictionaryAsync(p => p.Id, ct);

        foreach (var pedido in pedidos)
        {
            var linea = pedido.Linea;
            if (!productos.TryGetValue(linea.ProductId, out var producto)) continue;
            var lote = Normalizar(pedido.LotCode);
            var serie = Normalizar(pedido.SerialNumber);
            var entra = Entra(documento.Class, producto.Kind);

            Lot? delLote = null;
            if (lote is null)
            {
                if (pedido.ExpiryDate is not null && !producto.TracksLot) return Result.Failure(CatalogErrors.LotNotTracked(producto.Code));
                linea.LotId = null;
                linea.Lot = null;
            }
            else
            {
                if (!producto.TracksLot) return Result.Failure(CatalogErrors.LotNotTracked(producto.Code));
                delLote = db.Lots.Local.FirstOrDefault(l => l.ProductId == producto.Id && l.Code == lote)
                          ?? await db.Lots.FirstOrDefaultAsync(l => l.ProductId == producto.Id && l.Code == lote, ct);
                if (delLote is null)
                {
                    if (!entra) return Result.Failure(CatalogErrors.LotNotFound(producto.Code, lote));
                    if (producto.TracksExpiry && pedido.ExpiryDate is null) return Result.Failure(CatalogErrors.LotExpiryRequired(producto.Code));
                    delLote = new Lot { ProductId = producto.Id, Code = lote, ExpiryDate = pedido.ExpiryDate };
                    db.Lots.Add(delLote);
                }
                else if (pedido.ExpiryDate is { } fecha && delLote.ExpiryDate != fecha)
                {
                    return Result.Failure(CatalogErrors.LotExpiryMismatch(producto.Code, lote, delLote.ExpiryDate));
                }
                linea.Lot = delLote;
                if (delLote.Id != 0) linea.LotId = delLote.Id;
            }

            if (serie is null)
            {
                linea.SerialId = null;
                linea.Serial = null;
                continue;
            }
            if (!producto.TracksSerial) return Result.Failure(CatalogErrors.SerialNotTracked(producto.Code));
            if (linea.QuantityBase != 1m) return Result.Failure(CatalogErrors.SerialQuantityNotOne(producto.Code, serie));
            var registrada = db.Serials.Local.FirstOrDefault(s => s.ProductId == producto.Id && s.SerialNumber == serie)
                             ?? await db.Serials.FirstOrDefaultAsync(s => s.ProductId == producto.Id && s.SerialNumber == serie, ct);
            if (registrada is null)
            {
                if (!entra) return Result.Failure(CatalogErrors.SerialNotFound(producto.Code, serie));
                registrada = new Serial { ProductId = producto.Id, SerialNumber = serie, Lot = delLote };
                db.Serials.Add(registrada);
            }
            linea.Serial = registrada;
            if (registrada.Id != 0) linea.SerialId = registrada.Id;
        }
        return Result.Success();
    }

    // ----------------------------------------------------------------------------------- al confirmar (fuera) --

    /// <summary>Las reglas de seguimiento del documento, fuera del cerrojo: los errores (el primero detiene la confirmación) y los avisos.</summary>
    public async Task<VeredictoDeSeguimiento> EvaluarAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (!ClasesConSeguimientoEnLaLinea.Contains(documento.Class)) return VeredictoDeSeguimiento.Ninguno;
        var vivas = documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber).ToList();
        if (vivas.Count == 0) return VeredictoDeSeguimiento.Ninguno;

        var ids = vivas.Select(l => l.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && (p.TracksLot || p.TracksSerial))
            .Select(p => new { p.Id, p.Code, p.Kind, p.TracksLot, p.TracksSerial, p.TracksExpiry }).ToDictionaryAsync(p => p.Id, ct);
        if (productos.Count == 0) return VeredictoDeSeguimiento.Ninguno;

        var esVenta = Kardex.RegistroDeKardex.ClasesDeVenta.Contains(documento.Class);
        var hoy = reloj.HoyLocal;
        var politica = esVenta ? await PoliticaAsync(parametros, hoy, ct) : PoliticaDeLoteVencido.Advertir;
        var lotesCitados = vivas.Select(l => l.LotId).OfType<int>().Distinct().ToList();
        var lotes = await db.Lots.AsNoTracking().Where(l => lotesCitados.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var seriesCitadas = vivas.Select(l => l.SerialId).OfType<int>().Distinct().ToList();
        var series = await db.Serials.AsNoTracking().Where(s => seriesCitadas.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        var errores = new List<Error>();
        var avisos = new List<Error>();
        var tomado = new Dictionary<(int, int), decimal>();
        foreach (var linea in vivas)
        {
            if (!productos.TryGetValue(linea.ProductId, out var producto)) continue;
            var entra = Entra(documento.Class, producto.Kind);
            var lote = linea.Lot ?? (linea.LotId is int lid && lotes.TryGetValue(lid, out var leido) ? leido : null);

            if (producto.TracksLot)
            {
                if (entra)
                {
                    if (lote is null) errores.Add(CatalogErrors.LotRequired(producto.Code));
                    else if (producto.TracksExpiry && lote.ExpiryDate is null) errores.Add(CatalogErrors.LotExpiryRequired(producto.Code));
                }
                else if (esVenta && lote is not null)
                {
                    if (SelectorDeLotes.EstaVencido(lote.ExpiryDate, hoy))
                        (politica == PoliticaDeLoteVencido.Bloquear ? errores : avisos).Add(CatalogErrors.LotExpired(producto.Code, lote.Code, lote.ExpiryDate));
                }
                else if (esVenta && documento.WarehouseId is int bodega)
                {
                    // La sugerencia que hará el registro (sin bloquear): si sólo alcanza con lo vencido, lo dice ya.
                    var sugerencia = await SugerirAsync(producto.Id, bodega, linea.QuantityBase, hoy, politica, admiteVencidos: false, tomado, ct);
                    if (!sugerencia.Completo && sugerencia.Excluidos.Count > 0 && sugerencia.Excluidos.Sum(x => x.Disponible) >= sugerencia.Faltante)
                        errores.Add(CatalogErrors.LotExpired(producto.Code, sugerencia.Excluidos[0].Codigo, sugerencia.Excluidos[0].Vencimiento));
                    else if (sugerencia.HayVencidos)
                        foreach (var vencido in sugerencia.Asignaciones.Where(a => a.Vencido))
                            avisos.Add(CatalogErrors.LotExpired(producto.Code, vencido.Lote.Codigo, vencido.Lote.Vencimiento));
                }
            }

            if (producto.TracksSerial)
            {
                var serie = linea.Serial ?? (linea.SerialId is int sid && series.TryGetValue(sid, out var s) ? s : null);
                if (serie is null) errores.Add(CatalogErrors.SerialRequired(producto.Code));
                else if (linea.QuantityBase != 1m) errores.Add(CatalogErrors.SerialQuantityNotOne(producto.Code, serie.SerialNumber));
                else if (entra && serie.InStockWarehouseId is not null) errores.Add(CatalogErrors.SerialAlreadyInStock(producto.Code, serie.SerialNumber));
                else if (!entra && serie.InStockWarehouseId != documento.WarehouseId) errores.Add(CatalogErrors.SerialNotInStock(producto.Code, serie.SerialNumber));
            }
        }
        return new VeredictoDeSeguimiento(errores, avisos);
    }

    /// <summary>La regla de la clase al confirmar: el primer error, si lo hay (los avisos se leen de <see cref="EvaluarAsync"/>).</summary>
    public async Task<Result<IReadOnlyList<Error>>> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return Result.Success<IReadOnlyList<Error>>([]);
        var veredicto = await EvaluarAsync(contexto.Documento, ct);
        return veredicto.Errores.Count > 0
            ? Result.Failure<IReadOnlyList<Error>>(veredicto.Errores[0])
            : Result.Success(veredicto.Avisos);
    }

    // ---------------------------------------------------------------------------------------- la sugerencia --

    /// <summary>
    /// El reparto que se sugiere para una salida de <paramref name="cantidad"/> del producto en la bodega (FEFO, <see cref="SelectorDeLotes"/>),
    /// sobre la existencia por lote leída sin bloqueo (la pantalla, el POS y el aviso del borrador; el registro vuelve a repartir dentro del
    /// cerrojo). <paramref name="tomado"/> descuenta lo que ya tomaron otras líneas del mismo documento.
    /// </summary>
    public async Task<ResultadoDeLotes> SugerirAsync(int productoId, int bodegaId, decimal cantidad, DateOnly hoy, PoliticaDeLoteVencido politica,
        bool admiteVencidos, Dictionary<(int, int), decimal>? tomado, CancellationToken ct)
    {
        var disponibles = await DisponiblesAsync(productoId, bodegaId, ct);
        if (tomado is not null)
            disponibles = disponibles.Select(d => d with { Disponible = d.Disponible - tomado.GetValueOrDefault((productoId, d.LotId)) }).ToList();
        var reparto = SelectorDeLotes.Repartir(new PedidoDeLotes(disponibles, cantidad, hoy, politica) { AdmiteVencidos = admiteVencidos });
        if (tomado is not null)
            foreach (var a in reparto.Asignaciones) tomado[(productoId, a.Lote.LotId)] = tomado.GetValueOrDefault((productoId, a.Lote.LotId)) + a.Cantidad;
        return reparto;
    }

    /// <summary>La política de <c>Ventas.LoteVencido</c> vigente hoy.</summary>
    public Task<PoliticaDeLoteVencido> PoliticaVigenteAsync(CancellationToken ct) => PoliticaAsync(parametros, reloj.HoyLocal, ct);

    /// <summary>La sugerencia con la política vigente de la cooperativa para una venta (el POS, T929).</summary>
    public async Task<ResultadoDeLotes> SugerirParaVenderAsync(int productoId, int bodegaId, decimal cantidad, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        return await SugerirAsync(productoId, bodegaId, cantidad, hoy, await PoliticaAsync(parametros, hoy, ct), admiteVencidos: false, null, ct);
    }

    /// <summary>Los lotes con existencia del producto en la bodega (todas sus ubicaciones), en unidad base.</summary>
    public async Task<IReadOnlyList<LoteDisponible>> DisponiblesAsync(int productoId, int bodegaId, CancellationToken ct) =>
        await db.StockDetails.AsNoTracking()
            .Where(s => s.ProductId == productoId && s.WarehouseId == bodegaId && s.LotId != null && s.Quantity > 0m)
            .GroupBy(s => s.LotId!.Value)
            .Select(g => new { LotId = g.Key, Cantidad = g.Sum(s => s.Quantity) })
            .Join(db.Lots.AsNoTracking(), x => x.LotId, l => l.Id, (x, l) => new LoteDisponible(l.Id, l.Code, l.ExpiryDate, x.Cantidad))
            .ToListAsync(ct);

    /// <summary>Recortado y en mayúsculas, como se guardan el lote y la serie; vacío = nulo.</summary>
    public static string? Normalizar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim().ToUpperInvariant();
}
