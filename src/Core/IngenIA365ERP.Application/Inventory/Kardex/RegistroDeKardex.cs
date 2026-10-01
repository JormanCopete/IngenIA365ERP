using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Kardex;

/// <summary>
/// Un movimiento que una estrategia de clase le pide al registro (feature 012, T251). (nuevo)
/// </summary>
/// <param name="Linea">La línea del documento de la que nace (la cantidad ya va en unidad base).</param>
/// <param name="WarehouseId">La bodega que se mueve.</param>
/// <param name="QuantityBase">Con signo: + entrada, − salida; nunca cero.</param>
/// <param name="Valoracion">Cómo se valora (<see cref="ValoracionDelMovimiento"/>).</param>
/// <param name="CostoUnitario">El costo que trae: el indicado, el de la línea de origen o el de la entrada devuelta.</param>
/// <param name="Origen">La fila del kardex de origen (la que se revierte o cuyo costo se sigue).</param>
/// <param name="EsAnulacion">Revierte <paramref name="Origen"/> (<c>ReversesEntryId</c>).</param>
/// <param name="LocationId">La ubicación; nula = la de la línea o, sin ella, la por defecto de la bodega.</param>
/// <param name="AlCostoDe">
/// US10 (T367, T368; nuevo): una entrada que viaja al costo con que salió <b>otro movimiento de esta misma llamada</b> (la entrada
/// al tránsito del despacho, la entrada a la ubicación de destino de un movimiento entre ubicaciones). El registro la valora
/// <see cref="ValoracionDelMovimiento.AlCostoDeOrigen"/> con el costo unitario de la salida ya calculada; si las dos quedan en el
/// mismo ámbito de costo, el movimiento es neutro y el último costo del ámbito no cambia. Va después de su salida en la lista.
/// </param>
public sealed record MovimientoDeKardex(
    InventoryDocumentLine Linea,
    int WarehouseId,
    decimal QuantityBase,
    ValoracionDelMovimiento Valoracion,
    decimal? CostoUnitario = null,
    KardexEntry? Origen = null,
    bool EsAnulacion = false,
    int? LocationId = null,
    MovimientoDeKardex? AlCostoDe = null)
{
    /// <summary>
    /// I6 (T926; nuevo): el producto que se mueve cuando no es el de la línea —un componente de un combo o de un ensamble—. Nulo = el de
    /// la línea.
    /// </summary>
    public int? ProductId { get; init; }

    /// <summary>I6 (T923, T924; nuevo): el lote que se mueve cuando no es el de la línea ni el del origen (el reparto FEFO de una salida).</summary>
    public int? LotId { get; init; }

    /// <summary>I6 (T923, T924; nuevo): la serie que se mueve cuando no es la de la línea ni la del origen.</summary>
    public int? SerialId { get; init; }

    /// <summary>
    /// I6 (T928; nuevo): la entrada del kit de un ensamble, al costo de lo que consumieron <b>estas salidas de esta misma llamada</b>
    /// (Σ de su valor ÷ la cantidad, con el residuo por <c>Redondeo</c>: <c>MotorDeCosteo.EntradaDeEnsamble</c>). Va después de ellas.
    /// </summary>
    public IReadOnlyList<MovimientoDeKardex>? AlCostoConsumidoDe { get; init; }

    /// <summary>El producto que se mueve.</summary>
    public int Producto => ProductId ?? Linea.ProductId;

    /// <summary>El lote que se mueve: el propio, el de la fila de origen o el de la línea (sólo si el producto es el de la línea).</summary>
    public int? Lote => LotId ?? Origen?.LotId ?? (ProductId is null ? Linea.LotId : null);

    /// <summary>La serie que se mueve: la propia, la de la fila de origen o la de la línea (sólo si el producto es el de la línea).</summary>
    public int? Serie => SerialId ?? Origen?.SerialId ?? (ProductId is null ? Linea.SerialId : null);
}

/// <summary>
/// Los parámetros con que el registro valora a la fecha de operación: <c>Costeo.Metodo</c>, <c>Costeo.Ambito</c>,
/// <c>Redondeo.Montos</c> y <c>Existencias.StockNegativoPermitido</c> por bodega (general con excepción por bodega). (nuevo)
/// </summary>
public sealed record ParametrosDelKardex(CostMethod Metodo, CostScope Ambito, RedondeoDeMontos Montos, IReadOnlyDictionary<int, bool> NegativoPorBodega)
{
    /// <summary>El ámbito de costo de una bodega: 0 (cooperativa) o la bodega misma.</summary>
    public int AmbitoDe(int bodegaId) => Ambito == CostScope.Warehouse ? bodegaId : 0;

    public bool NegativoPermitido(int bodegaId) => NegativoPorBodega.TryGetValue(bodegaId, out var permitido) && permitido;
}

/// <summary>Lo que el registro necesita antes de bloquear: las filas a bloquear y el valor al costo estimado. (nuevo)</summary>
public sealed record PreparacionDelRegistro(PedidoDeCerrojo Cerrojo, decimal ValorEstimado);

/// <summary>Un movimiento ya escrito: sus filas del kardex (principal y ajustes) y la explicación del costo. (nuevo)</summary>
public sealed record MovimientoEscrito(MovimientoDeKardex Movimiento, int LocationId, IReadOnlyList<KardexEntry> Lineas, ExplicacionDeCosto Explicacion);

/// <summary>Una línea <c>Retroactive</c> escrita, con la porción que corrige (en existencia o vendida). (nuevo)</summary>
public sealed record LineaRetroactivaEscrita(KardexEntry Fila, PorcionDelAjuste Porcion);

/// <summary>
/// Los ajustes <c>Retroactive</c> que el retroactivo mínimo dejó sobre UN documento afectado (T285): con ellos se arma un
/// <c>AjusteDeCostoReconocido</c> por documento (<c>EmisionDeInventario.AjustesRetroactivosAsync</c>). (nuevo)
/// </summary>
public sealed record AjusteRetroactivoRegistrado(int AffectedDocumentId, decimal EnExistencia, decimal Vendida, IReadOnlyList<LineaRetroactivaEscrita> Lineas);

/// <summary>
/// Una diferencia de precio que pide la factura o la nota del proveedor (US9, T341): la línea del documento, la entrada del
/// kardex que corrige, la cantidad facturada en unidad base y la diferencia de costo total. (nuevo)
/// </summary>
public sealed record DiferenciaDePrecioPedida(InventoryDocumentLine Linea, KardexEntry Entrada, decimal CantidadFacturada, decimal Diferencia);

/// <summary>Una diferencia de precio ya escrita: lo que quedó en existencia, lo que pasó a lo vendido y sus filas. (nuevo)</summary>
public sealed record DiferenciaDePrecioRegistrada(DiferenciaDePrecioPedida Pedida, decimal EnExistencia, decimal Vendida, IReadOnlyList<KardexEntry> Lineas);

/// <summary>
/// La porción de unos costos adicionales que toca a una línea de recepción (US13, T800): la línea del documento <c>LandedCost</c> (o
/// de su anulación) que registra, la entrada del kardex de la recepción y el reparto de <see cref="Prorrateo"/>. En la anulación el
/// reparto va con los signos contrarios. (nuevo)
/// </summary>
public sealed record CostoAdicionalPedido(InventoryDocumentLine Linea, KardexEntry Entrada, RepartoDeLinea Reparto);

/// <summary>Un costo adicional ya escrito: lo que quedó en existencia, lo que pasó a costo de venta y sus filas. (nuevo)</summary>
public sealed record CostoAdicionalRegistrado(CostoAdicionalPedido Pedido, decimal EnExistencia, decimal Vendida, IReadOnlyList<KardexEntry> Lineas);

/// <summary>
/// El impacto en costos de un borrador (US16, T840; FR-045; api.md §9.3): si es retroactivo y, por cada (producto, ámbito) que
/// recalcula, lo que devolvió <see cref="MotorDeCosteo.SimularImpacto"/> —el mismo cálculo que hará la confirmación—. Lo deja el
/// registro en modo simulación (<see cref="RegistroDeKardex.Simular"/>), sin escribir nada. (nuevo)
/// </summary>
public sealed record ImpactoDelBorrador(bool EsRetroactivo, IReadOnlyList<ImpactoPorAmbito> PorAmbito)
{
    public static ImpactoDelBorrador Ninguno { get; } = new(false, []);
}

/// <summary>El impacto de un (producto, ámbito) del borrador. (nuevo)</summary>
public sealed record ImpactoPorAmbito(int ProductId, int ScopeWarehouseId, ImpactoEnCostos Impacto);

/// <summary>
/// Una línea <c>MethodChange</c> que pide el cambio de método o de ámbito de costeo (US16, T836, T841): la línea del documento
/// <c>CostAdjustment</c> del sistema (una por producto), la bodega y ubicación donde se anota, el ámbito, la cantidad que mueve entre
/// ámbitos (0 en un cambio de método; −/+ en uno de ámbito: sale del anterior y entra al nuevo), el costo, el valor y, a PEPS, la capa
/// única que abre (la de <c>MotorDeCosteo.CambiarMetodo</c>). (nuevo)
/// </summary>
public sealed record LineaDeCambioDeMetodo(
    InventoryDocumentLine Linea, int WarehouseId, int LocationId, int Ambito, decimal QuantityBase, decimal UnitCost, decimal TotalCost,
    CapaDeCosto? Capa = null);

/// <summary>Lo que dejó el registro de un documento. (nuevo)</summary>
public sealed record RegistroHecho(IReadOnlyList<MovimientoEscrito> Movimientos)
{
    /// <summary>Las líneas de los movimientos del documento (no incluye los ajustes retroactivos sobre otros documentos).</summary>
    public IEnumerable<KardexEntry> Lineas => Movimientos.SelectMany(m => m.Lineas);

    /// <summary>Los ajustes <c>Retroactive</c> sobre movimientos posteriores, uno por documento afectado (T285).</summary>
    public IReadOnlyList<AjusteRetroactivoRegistrado> AjustesRetroactivos { get; init; } = [];
}

/// <summary>
/// <b>El único escritor</b> del kardex y de sus proyecciones (feature 012, T251; FR-001 a FR-004, FR-034; decisiones-transversales
/// §1.3 paso 7, T18; data-model §3.1–§3.4). Lo llama la estrategia de cada clase dentro del cerrojo: recibe los movimientos en
/// unidad base, lee a la fecha de operación el método, el ámbito, el redondeo y el negativo por bodega (<see cref="ILectorDeParametros"/>),
/// comprueba la disponibilidad (física − reservada, y la de la ubicación) salvo <c>Existencias.StockNegativoPermitido</c>, pide a
/// <see cref="MotorDeCosteo"/> las líneas de cada movimiento, agrega las <see cref="KardexEntry"/> y actualiza
/// <c>INV_StockBalances</c>, <c>INV_StockDetails</c> e <c>INV_CostStates</c>. Escribe también el costo y la ubicación por
/// defecto en la línea del documento (que todavía es borrador en el seguimiento). <b>Nunca guarda</b>: el <c>SaveChanges</c> es
/// del ciclo común. Lo vigila <c>NadieEscribeElKardexFueraDelRegistro</c>.
///
/// <para>
/// Dos pasadas: la primera calcula todo en memoria y junta <b>todas</b> las líneas que no caben
/// (<c>Inventory.Stock.Insufficient</c> con <c>data.lines[]</c>); sólo si todas caben, la segunda escribe. Así un rechazo no
/// deja nada a medio escribir en el seguimiento.
/// </para>
/// <para>
/// Las filas de proyección las crea el cerrojo (<c>INSERT … ON CONFLICT</c>) antes de bloquearlas; si una falta (un anfitrión
/// sin cerrojo real, las pruebas), la crea aquí. Las salidas en negativo pendientes no se reconstruyen del kardex: la
/// regularización usa <c>Value / Quantity</c> del ámbito sin <c>AffectsEntryId</c> (research R10, decisión de US3).
/// </para>
/// <para>
/// <b>Retroactivo</b> (US3, T285; FR-045, T18): un documento que deja un movimiento con fecha anterior a otro ya registrado
/// del mismo producto y ámbito se rechaza con <c>Inventory.Costing.RetroactiveNotAllowed</c> nombrando el posterior —hasta I5
/// sin mirar <c>Costeo.RetroactivosPermitidos</c>; desde I5 con la puerta de T838 (parámetro, días máximos y D6, T42g)—, salvo las dos clases exentas de I1 (<see cref="EsExentoAsync"/>):
/// el saldo inicial de una bodega <c>NotActivated</c> (y su anulación) y los ajustes de un conteo (<c>CountAdjustmentOf</c>).
/// Para ellas el ámbito se recalcula con <see cref="Retroactivo.Insertar"/> desde el estado a la fecha del documento y la
/// historia posterior (índice <c>(ProductId, CostScopeWarehouseId, OperationDate, Id)</c>): las líneas <c>Retroactive</c> van
/// bajo este documento, fechadas en la salida afectada y en su bodega, y vuelven en <see cref="RegistroHecho.AjustesRetroactivos"/>
/// agrupadas por documento afectado; un saldo intermedio bajo cero con el negativo prohibido es
/// <c>Inventory.Stock.Insufficient</c>.
/// </para>
/// </summary>
public sealed class RegistroDeKardex(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    EntregaDelComercio entrega = CatalogoDeParametros.EntregaVigente)
{
    /// <summary>El código con que termina un registro en modo simulación (nunca llega a la pantalla: lo consume la consulta).</summary>
    public const string CodigoSimulacionTerminada = "Inventory.Costing.SimulationDone";

    private bool _simulando;
    private readonly Dictionary<InventoryDocument, List<AjusteRetroactivoRegistrado>> _retroactivosSinEmitir = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// US16 (T840): la próxima llamada a <see cref="RegistrarAsync"/> calcula todo —disponibilidad, puerta del retroactivo, el
    /// retroactivo con <see cref="MotorDeCosteo.SimularImpacto"/>— y, en lugar de escribir, deja el resultado en <see cref="Simulado"/> y
    /// termina con <see cref="CodigoSimulacionTerminada"/>. Así <c>cost-impact</c> corre el mismo efecto de la clase que la confirmación.
    /// </summary>
    public void Simular()
    {
        _simulando = true;
        Simulado = null;
    }

    /// <summary>Lo que dejó la simulación; nulo si el efecto no llegó al registro.</summary>
    public ImpactoDelBorrador? Simulado { get; private set; }

    /// <summary>
    /// US16 (T839): los ajustes <c>Retroactive</c> que dejó el registro de <paramref name="documento"/> y que todavía no se emitieron; la
    /// confirmación los toma una vez, después de los mensajes de la clase, y arma un <c>AjusteDeCostoReconocido</c> por documento afectado.
    /// </summary>
    public IReadOnlyList<AjusteRetroactivoRegistrado> TomarAjustesRetroactivos(InventoryDocument documento)
    {
        if (!_retroactivosSinEmitir.Remove(documento, out var lista)) return [];
        return lista;
    }

    // ------------------------------------------------------------------------------ validación previa (I2) --

    /// <summary>
    /// Filas de kardex <b>provisionales</b> de un borrador, para armar sus mensajes antes del cerrojo (feature 012, T520; T30;
    /// contracts/contabilidad.md §4.1, §4.2): una por línea viva en <paramref name="bodegaId"/>, en unidad base, con el signo del
    /// kardex (negativo en las salidas) y al costo que tendría hoy —el que propone <paramref name="costoPropuesto"/> para la línea
    /// (el digitado, el de compra) si lo da; si no, el promedio vigente del producto leído <b>sin bloqueo</b> (el de la bodega si el costo es por bodega, si
    /// no el de la cooperativa; sin existencia, el último costo)—. No se agregan al contexto: sólo alimentan los mismos
    /// constructores de contenido de la confirmación. El costo definitivo lo pone <c>RegistroDeKardex</c> dentro del cerrojo, y
    /// lo único que depende de él en Contabilidad es la regla de «valor cero». <paramref name="bodegaDeLasFilas"/> pone las filas en
    /// otra bodega que la del costo (la entrada al tránsito de un despacho, al costo del origen). Vive aquí porque sólo el registro
    /// crea filas del kardex (<c>NadieEscribeElKardexFueraDelRegistro</c>), aunque éstas nunca se agregan al contexto. (nuevo)
    /// </summary>
    public async Task<IReadOnlyList<KardexEntry>> FilasProvisionalesAsync(
        InventoryDocument documento, int bodegaId, KardexEntryKind sentido, Func<InventoryDocumentLine, decimal?>? costoPropuesto, CancellationToken ct,
        int? bodegaDeLasFilas = null)
    {
        var vivas = documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber).ToList();
        if (vivas.Count == 0) return [];
        var productos = vivas.Select(l => l.ProductId).Distinct().ToList();
        var estados = await db.CostStates.AsNoTracking().Where(c => productos.Contains(c.ProductId)).ToListAsync(ct);
        var signo = sentido == KardexEntryKind.Exit ? -1m : 1m;

        return vivas.Select(l =>
        {
            var estado = estados.FirstOrDefault(e => e.ProductId == l.ProductId && e.ScopeWarehouseId == bodegaId)
                         ?? estados.FirstOrDefault(e => e.ProductId == l.ProductId && e.ScopeWarehouseId == 0);
            var vigente = estado is null ? 0m : (estado.Quantity > 0m ? estado.AverageCost : estado.LastUnitCost);
            var unitario = costoPropuesto?.Invoke(l) is decimal propuesto and > 0m ? propuesto : vigente;
            return new KardexEntry
            {
                DocumentId = documento.Id,
                DocumentLineId = l.Id,
                ProductId = l.ProductId,
                WarehouseId = bodegaDeLasFilas ?? bodegaId,
                OperationDate = documento.OperationDate,
                Kind = sentido,
                QuantityBase = signo * l.QuantityBase,
                UnitCost = unitario,
                TotalCost = signo * Math.Round(l.QuantityBase * unitario, 2, MidpointRounding.AwayFromZero),
            };
        }).ToList();
    }

    /// <summary>
    /// Filas provisionales de unos <b>movimientos</b> (I6, T926): una por movimiento, en su producto —el componente de un combo— y su
    /// bodega, al promedio vigente leído sin bloqueo (o al costo que traen). Como <see cref="FilasProvisionalesAsync"/>, nunca se agregan al
    /// contexto: sólo alimentan la validación previa contable. (nuevo)
    /// </summary>
    public async Task<IReadOnlyList<KardexEntry>> FilasProvisionalesDeAsync(InventoryDocument documento, IReadOnlyList<MovimientoDeKardex> movimientos, CancellationToken ct)
    {
        if (movimientos.Count == 0) return [];
        var productos = movimientos.Select(m => m.Producto).Distinct().ToList();
        var estados = await db.CostStates.AsNoTracking().Where(c => productos.Contains(c.ProductId)).ToListAsync(ct);
        var filas = new Dictionary<MovimientoDeKardex, KardexEntry>(ReferenceEqualityComparer.Instance);
        foreach (var m in movimientos)
        {
            var estado = estados.FirstOrDefault(e => e.ProductId == m.Producto && e.ScopeWarehouseId == m.WarehouseId)
                         ?? estados.FirstOrDefault(e => e.ProductId == m.Producto && e.ScopeWarehouseId == 0);
            var vigente = estado is null ? 0m : (estado.Quantity > 0m ? estado.AverageCost : estado.LastUnitCost);
            var unitario = m.CostoUnitario is decimal traido and > 0m ? traido : vigente;
            decimal? total = null;
            if (m.AlCostoConsumidoDe is { Count: > 0 } consumidas)
            {
                // El kit de un ensamble (T928): lo que consumieron sus componentes en esta misma lista.
                total = Math.Abs(consumidas.Where(filas.ContainsKey).Sum(c => filas[c].TotalCost));
                unitario = m.QuantityBase == 0m ? 0m : Redondeo.CostoUnitario(total.Value / Math.Abs(m.QuantityBase));
            }
            filas[m] = new KardexEntry
            {
                DocumentId = documento.Id,
                DocumentLineId = m.Linea.Id,
                ProductId = m.Producto,
                WarehouseId = m.WarehouseId,
                OperationDate = documento.OperationDate,
                Kind = m.QuantityBase < 0m ? KardexEntryKind.Exit : KardexEntryKind.Entry,
                QuantityBase = m.QuantityBase,
                UnitCost = unitario,
                TotalCost = total ?? Math.Round(m.QuantityBase * unitario, 2, MidpointRounding.AwayFromZero),
            };
        }
        return movimientos.Select(m => filas[m]).ToList();
    }

    /// <summary>Lo que hay que bloquear y el valor al costo estimado (para la política de aprobación), sin escribir nada.</summary>
    public async Task<Result<PreparacionDelRegistro>> PrepararAsync(InventoryDocument documento, IReadOnlyList<MovimientoDeKardex> movimientos, CancellationToken ct)
    {
        var leidos = await LeerParametrosAsync(documento.OperationDate, movimientos.Select(m => m.WarehouseId).Distinct().ToList(), ct);
        if (leidos.IsFailure) return Result.Failure<PreparacionDelRegistro>(leidos.Error);
        var p = leidos.Value;
        var ubicaciones = await UbicacionesPorDefectoAsync(movimientos, ct);
        if (ubicaciones.IsFailure) return Result.Failure<PreparacionDelRegistro>(ubicaciones.Error);

        var productos = movimientos.Select(m => m.Producto).Distinct().ToList();
        var estados = await db.CostStates.AsNoTracking().Where(c => productos.Contains(c.ProductId)).ToListAsync(ct);

        var valor = 0m;
        foreach (var m in movimientos)
        {
            var estado = estados.FirstOrDefault(e => e.ProductId == m.Producto && e.ScopeWarehouseId == p.AmbitoDe(m.WarehouseId));
            var vigente = estado is null ? 0m : (estado.Quantity > 0m ? estado.AverageCost : estado.LastUnitCost);
            valor += Math.Abs(m.QuantityBase) * (m.CostoUnitario ?? vigente);
        }

        var cerrojo = new PedidoDeCerrojo
        {
            Bodegas = movimientos.Select(m => m.WarehouseId).Distinct().ToList(),
            EstadosDeCosto = movimientos.Select(m => new ClaveDeEstadoDeCosto(m.Producto, p.AmbitoDe(m.WarehouseId), p.Metodo)).Distinct().ToList(),
            Existencias = movimientos.Select(m => new ClaveDeExistencia(m.Producto, m.WarehouseId)).Distinct().ToList(),
            // I6 (T924): el detalle del lote que se mueve (el reparto FEFO de una salida sin lote se hace dentro del cerrojo, sobre
            // filas que ya existen y que protege la fila de existencia del producto en la bodega).
            Detalles = movimientos.Select(m => new ClaveDeDetalleDeExistencia(m.Producto, m.WarehouseId, UbicacionDe(m, ubicaciones.Value), m.Lote))
                .Distinct().ToList(),
            // I6 (T925): las series que se mueven, por Id y en exclusivo, después de los detalles.
            Series = movimientos.Select(m => m.Serie).OfType<int>().Distinct().ToList(),
        };
        return Result.Success(new PreparacionDelRegistro(cerrojo, Math.Round(valor, 2, MidpointRounding.AwayFromZero)));
    }

    /// <summary>
    /// Registra los movimientos del documento (paso 7 del flujo canónico, dentro del cerrojo). Falla con
    /// <c>Inventory.Stock.Insufficient</c> —todas las líneas que no caben— sin tocar nada; <paramref name="sugerencia"/> va en
    /// <c>data.suggestion</c> (la anulación de una entrada ya consumida propone <c>NegativeAdjustment</c> o <c>SupplierReturn</c>).
    /// </summary>
    public async Task<Result<RegistroHecho>> RegistrarAsync(
        InventoryDocument documento, IReadOnlyList<MovimientoDeKardex> movimientos, CancellationToken ct, string? sugerencia = null)
    {
        if (movimientos.Any(m => m.QuantityBase == 0m))
            throw new ArgumentException("Un movimiento de kardex no puede tener cantidad cero.", nameof(movimientos));

        var bodegas = movimientos.Select(m => m.WarehouseId).Distinct().ToList();
        var leidos = await LeerParametrosAsync(documento.OperationDate, bodegas, ct);
        if (leidos.IsFailure) return Result.Failure<RegistroHecho>(leidos.Error);
        var p = leidos.Value;
        var porDefecto = await UbicacionesPorDefectoAsync(movimientos, ct);
        if (porDefecto.IsFailure) return Result.Failure<RegistroHecho>(porDefecto.Error);

        // I6 (T923, T924): lote y serie dentro del cerrojo —el reparto FEFO de las salidas sin lote, lo que hereda la entrada que viaja al
        // costo de su salida, las reglas de la serie—. Sin productos con seguimiento, los movimientos siguen iguales.
        var seguimiento = await SeguimientoAsync(documento, movimientos, porDefecto.Value, ct);
        if (seguimiento.IsFailure)
        {
            _simulando = false;
            return Result.Failure<RegistroHecho>(seguimiento.Error);
        }
        movimientos = seguimiento.Value.Movimientos;
        var series = seguimiento.Value.Series;

        // Las filas bloqueadas, seguidas (el cerrojo ya las creó; si falta alguna, se crea al escribir).
        var productos = movimientos.Select(m => m.Producto).Distinct().ToList();
        var costos = (await db.CostStates.Where(c => productos.Contains(c.ProductId)).ToListAsync(ct))
            .ToDictionary(c => (c.ProductId, c.ScopeWarehouseId));
        var existencias = (await db.StockBalances.Where(s => productos.Contains(s.ProductId) && bodegas.Contains(s.WarehouseId)).ToListAsync(ct))
            .ToDictionary(s => (s.ProductId, s.WarehouseId));
        // I6 (T924): la existencia por ubicación y lote (lote nulo en los productos sin seguimiento, como hasta I5).
        var detalles = (await db.StockDetails.Where(s => productos.Contains(s.ProductId) && bodegas.Contains(s.WarehouseId)).ToListAsync(ct))
            .ToDictionary(s => (s.ProductId, s.WarehouseId, s.LocationId, s.LotId));

        // PEPS (I5, T836): las capas vivas de los productos, seguidas (las protege la fila de INV_CostStates de su ámbito, ya bloqueada),
        // y las de los consumos que devuelve la anulación de una salida (aunque ya estén agotadas).
        var capasPorEntrada = new Dictionary<long, CostLayer>();
        var consumosDelOrigen = new Dictionary<long, IReadOnlyList<ConsumoDeCapa>>();
        if (p.Metodo == CostMethod.Fifo)
        {
            foreach (var capa in await db.CostLayers.Where(c => productos.Contains(c.ProductId) && c.RemainingQuantity > 0m).ToListAsync(ct))
                capasPorEntrada[capa.EntryKardexEntryId] = capa;
            var salidasAnuladas = movimientos.Where(m => m.EsAnulacion && m.QuantityBase > 0m && m.Origen is { Id: > 0 }).Select(m => m.Origen!.Id).Distinct().ToList();
            if (salidasAnuladas.Count > 0)
            {
                var consumos = await db.LayerConsumptions.AsNoTracking().Where(c => salidasAnuladas.Contains(c.ExitKardexEntryId)).OrderBy(c => c.Id).ToListAsync(ct);
                var deLasCapas = consumos.Select(c => c.LayerId).Distinct().ToList();
                foreach (var capa in await db.CostLayers.Where(c => deLasCapas.Contains(c.Id)).ToListAsync(ct))
                    capasPorEntrada.TryAdd(capa.EntryKardexEntryId, capa);
                var porId = capasPorEntrada.Values.ToDictionary(c => c.Id);
                foreach (var grupo in consumos.GroupBy(c => c.ExitKardexEntryId))
                    consumosDelOrigen[grupo.Key] = grupo.Where(c => porId.ContainsKey(c.LayerId)).Select(c =>
                    {
                        var capa = porId[c.LayerId];
                        return new ConsumoDeCapa(ReferenciaDeKardex.A(grupo.Key),
                            new CapaDeCosto(ReferenciaDeKardex.A(capa.EntryKardexEntryId), capa.OperationDate, capa.OriginalQuantity, 0m, c.UnitCost),
                            c.Quantity, c.UnitCost, Redondeo.Monto(c.Quantity * c.UnitCost, p.Montos));
                    }).ToList();
            }
        }

        // --------------------------------------------------------------------- retroactivo (US3, T285) --
        var retro = await AmbitosRetroactivosAsync(documento, movimientos, p, ct);
        if (retro.IsFailure)
        {
            _simulando = false;
            return Result.Failure<RegistroHecho>(retro.Error);
        }
        var ambitosRetroactivos = retro.Value;
        var retroactivos = new List<(MovimientoDeKardex Mov, int Ubicacion, int Ambito)>();

        // ------------------------------------------------------------------ primera pasada: en memoria --
        var estados = costos.ToDictionary(c => c.Key, c => new EstadoDeCosto(c.Value.Quantity, c.Value.Value, c.Value.AverageCost, c.Value.LastUnitCost)
        {
            Capas = CapasDe(capasPorEntrada.Values, c.Key.ProductId, c.Key.ScopeWarehouseId),
        });
        var fisicos = existencias.ToDictionary(e => e.Key, e => (Fisico: e.Value.Physical, Reservado: e.Value.Reserved));
        var porUbicacion = detalles.ToDictionary(d => d.Key, d => d.Value.Quantity);
        // Lo que hay en la ubicación: la del lote que se mueve o, en una salida sin lote de un producto con lote (no alcanzó el reparto), la
        // suma de sus lotes.
        decimal EnUbicacion(MovimientoDeKardex m, int ubicacion) =>
            m.Lote is null && seguimiento.Value.ConLote.Contains(m.Producto)
                ? porUbicacion.Where(x => x.Key.ProductId == m.Producto && x.Key.WarehouseId == m.WarehouseId && x.Key.LocationId == ubicacion).Sum(x => x.Value)
                : porUbicacion.GetValueOrDefault((m.Producto, m.WarehouseId, ubicacion, m.Lote));
        var plan = new List<(MovimientoDeKardex Mov, int Ubicacion, int Ambito, ResultadoDeCosteo Costo)>(movimientos.Count);
        var faltantes = new List<(MovimientoDeKardex Mov, int Ubicacion, decimal Disponible, bool PorUbicacion)>();

        foreach (var m in movimientos)
        {
            var producto = m.Producto;
            var ubicacion = UbicacionDe(m, porDefecto.Value);
            var ambito = p.AmbitoDe(m.WarehouseId);
            var negativo = p.NegativoPermitido(m.WarehouseId);

            var (fisico, reservado) = fisicos.GetValueOrDefault((producto, m.WarehouseId));
            var clave = (producto, m.WarehouseId, ubicacion, m.Lote);
            var enUbicacion = EnUbicacion(m, ubicacion);
            if (m.QuantityBase < 0m && !negativo)
            {
                var disponible = fisico - reservado;
                if (disponible + m.QuantityBase < 0m || enUbicacion + m.QuantityBase < 0m)
                {
                    faltantes.Add((m, ubicacion, Math.Max(0m, Math.Min(disponible, enUbicacion)), enUbicacion < disponible));
                    continue;
                }
            }

            if (ambitosRetroactivos.Contains((producto, ambito)))
            {
                // El ámbito se recalcula entero con el retroactivo, después de esta pasada.
                fisicos[(producto, m.WarehouseId)] = (fisico + m.QuantityBase, reservado);
                porUbicacion[clave] = porUbicacion.GetValueOrDefault(clave) + m.QuantityBase;
                retroactivos.Add((m, ubicacion, ambito));
                continue;
            }

            var estado = estados.GetValueOrDefault((producto, ambito)) ?? EstadoDeCosto.Vacio;
            var origen = m.Origen is null ? null : new ReferenciaDeKardex(m.Origen.Id == 0 ? null : m.Origen.Id, null);
            var (valoracion, costoQueTrae) = (m.Valoracion, m.CostoUnitario);
            var mismoAmbitoQueSuSalida = false;
            if (m.AlCostoDe is { } salida)
            {
                // US10: la entrada sigue el costo de la salida de esta misma llamada (tránsito, cambio de ubicación).
                if (faltantes.Any(f => ReferenceEquals(f.Mov, salida))) continue;
                var previa = plan.FirstOrDefault(x => ReferenceEquals(x.Mov, salida));
                if (previa.Mov is null)
                    throw new InvalidOperationException("Un movimiento AlCostoDe va después de su salida, en la misma llamada.");
                (valoracion, costoQueTrae) = (ValoracionDelMovimiento.AlCostoDeOrigen, CostoUnitarioDe(previa.Costo));
                mismoAmbitoQueSuSalida = previa.Ambito == ambito;
            }
            ResultadoDeCosteo costo;
            if (m.AlCostoConsumidoDe is { Count: > 0 } consumidas)
            {
                // I6 (T928): el kit entra por exactamente lo que salió de sus componentes en esta llamada (con el residuo visible).
                if (faltantes.Any(f => consumidas.Any(c => ReferenceEquals(f.Mov, c)))) continue;
                var consumido = Math.Abs(plan.Where(x => consumidas.Any(c => ReferenceEquals(x.Mov, c))).Sum(x => x.Costo.Valor));
                costo = MotorDeCosteo.EntradaDeEnsamble(estado, m.QuantityBase, consumido, new ParametrosDeCosteo(p.Metodo, p.Montos, negativo), documento.OperationDate);
            }
            else
            {
                costo = MotorDeCosteo.Aplicar(estado,
                    new MovimientoDeCosto(m.QuantityBase, valoracion, costoQueTrae, origen, m.EsAnulacion)
                    {
                        OperationDate = documento.OperationDate,
                        ConsumosDelOrigen = m.EsAnulacion && m.Origen is { Id: > 0 } o && consumosDelOrigen.TryGetValue(o.Id, out var devueltos) ? devueltos : [],
                    },
                    new ParametrosDeCosteo(p.Metodo, p.Montos, negativo));
            }
            if (!costo.Admitido)
            {
                faltantes.Add((m, ubicacion, costo.Rechazo!.Disponible, false));
                continue;
            }
            // Un cambio de lugar dentro del mismo ámbito no es una compra: el último costo del ámbito se conserva.
            if (mismoAmbitoQueSuSalida) costo = costo with { Estado = costo.Estado with { LastUnitCost = estado.LastUnitCost } };

            estados[(producto, ambito)] = costo.Estado;
            fisicos[(producto, m.WarehouseId)] = (fisico + m.QuantityBase, reservado);
            porUbicacion[clave] = porUbicacion.GetValueOrDefault(clave) + m.QuantityBase;
            plan.Add((m, ubicacion, ambito, costo));
        }

        var recalculados = new List<(MovimientoDeKardex Primero, int Ambito, ResultadoRetroactivo Resultado, IReadOnlyDictionary<long, KardexEntry> Afectadas)>();
        var impactos = new List<ImpactoPorAmbito>();
        foreach (var grupo in retroactivos.GroupBy(r => (ProductId: r.Mov.Producto, r.Ambito)))
        {
            var deLaClave = grupo.ToList();
            var (pedido, afectadas) = await PedidoRetroactivoAsync(documento, grupo.Key.ProductId, grupo.Key.Ambito, deLaClave.Select(r => r.Mov).ToList(),
                new ParametrosDeCosteo(p.Metodo, p.Montos, deLaClave.All(r => p.NegativoPermitido(r.Mov.WarehouseId))), ct);
            ResultadoRetroactivo resultado;
            if (_simulando)
            {
                // T840: el mismo cálculo que la confirmación, por la puerta de la simulación.
                var impacto = MotorDeCosteo.SimularImpacto(pedido);
                impactos.Add(new ImpactoPorAmbito(grupo.Key.ProductId, grupo.Key.Ambito, impacto));
                resultado = impacto.Resultado;
            }
            else
            {
                resultado = Retroactivo.Insertar(pedido);
            }
            if (!resultado.Admitido)
            {
                // D6: con PEPS no hay retroactivo, tampoco para las dos excepciones de I1 (T42b).
                if (resultado.Rechazo!.Codigo == Retroactivo.CodigoRequierePromedioPonderado)
                {
                    _simulando = false;
                    return Result.Failure<RegistroHecho>(await RequierePromedioAsync(deLaClave[0].Mov, ct));
                }
                faltantes.Add((deLaClave[0].Mov, deLaClave[0].Ubicacion, resultado.Rechazo!.Disponible, false));
                continue;
            }
            estados[grupo.Key] = resultado.EstadoFinal;
            for (var i = 0; i < deLaClave.Count; i++) plan.Add((deLaClave[i].Mov, deLaClave[i].Ubicacion, grupo.Key.Ambito, resultado.Nuevos[i]));
            recalculados.Add((deLaClave[0].Mov, grupo.Key.Ambito, resultado, afectadas));
        }

        if (faltantes.Count > 0)
        {
            _simulando = false;
            return Result.Failure<RegistroHecho>(await ErrorDeExistenciaAsync(faltantes, sugerencia, ct));
        }

        if (_simulando)
        {
            // T840: nada se escribe; la consulta lee el impacto y no guarda lo que el efecto haya tocado en el seguimiento.
            _simulando = false;
            Simulado = new ImpactoDelBorrador(impactos.Any(i => i.Impacto.EsRetroactivo), impactos);
            return Result.Failure<RegistroHecho>(new Error(CodigoSimulacionTerminada, "Simulación del impacto en costos: no se escribió nada."));
        }

        // ------------------------------------------------------------------------ segunda pasada: escribir --
        var ahora = reloj.UtcNow;
        var propuestas = new Dictionary<LineaDeKardexPropuesta, KardexEntry>(ReferenceEqualityComparer.Instance);
        var escritos = new List<MovimientoEscrito>(plan.Count);
        var fechas = new Dictionary<(int, int), DateOnly>();

        foreach (var (m, ubicacion, ambito, costo) in plan)
        {
            var filas = new List<KardexEntry>(costo.Lineas.Count);
            foreach (var propuesta in costo.Lineas)
            {
                var (revierteId, revierte) = Resolver(propuesta.ReversesEntry, m, propuestas);
                var (afectaId, afecta) = Resolver(propuesta.AffectsEntry, m, propuestas);
                var fila = new KardexEntry
                {
                    DocumentId = documento.Id,
                    DocumentLineId = m.Linea.Id,
                    ProductId = m.Producto,
                    WarehouseId = m.WarehouseId,
                    LocationId = ubicacion,
                    LotId = m.Lote,
                    SerialId = m.Serie,
                    OperationDate = propuesta.OperationDate ?? documento.OperationDate,
                    RegisteredAt = ahora,
                    Kind = propuesta.Kind,
                    Reason = propuesta.Reason,
                    QuantityBase = propuesta.QuantityBase,
                    UnitCost = propuesta.UnitCost,
                    TotalCost = propuesta.TotalCost,
                    CostScopeWarehouseId = ambito,
                    CostMethod = p.Metodo,
                    ReversesEntryId = revierteId,
                    ReversesEntry = revierte,
                    AffectsEntryId = afectaId,
                    AffectsEntry = afecta,
                };
                db.KardexEntries.Add(fila);
                propuestas[propuesta] = fila;
                filas.Add(fila);

                var clave = (m.Producto, m.WarehouseId);
                if (!fechas.TryGetValue(clave, out var ultima) || fila.OperationDate > ultima) fechas[clave] = fila.OperationDate;
            }
            escritos.Add(new MovimientoEscrito(m, ubicacion, filas, costo.Explicacion));
        }

        // Los ajustes Retroactive: bajo este documento, en la bodega y la fecha de la salida afectada, uno por documento afectado.
        var ajustesRetroactivos = new List<AjusteRetroactivoRegistrado>();
        foreach (var (primero, ambito, resultado, afectadas) in recalculados)
        {
            foreach (var porDocumento in resultado.PorDocumento)
            {
                var filas = new List<LineaRetroactivaEscrita>(porDocumento.Lineas.Count);
                foreach (var propuesta in porDocumento.Lineas)
                {
                    var afectada = afectadas[propuesta.AffectsEntry!.EntryId!.Value];
                    var fila = new KardexEntry
                    {
                        DocumentId = documento.Id,
                        DocumentLineId = primero.Linea.Id,
                        ProductId = primero.Producto,
                        WarehouseId = afectada.WarehouseId,
                        LocationId = afectada.LocationId,
                        OperationDate = propuesta.OperationDate ?? afectada.OperationDate,
                        RegisteredAt = ahora,
                        Kind = KardexEntryKind.CostAdjustment,
                        Reason = KardexReason.Retroactive,
                        QuantityBase = 0m,
                        UnitCost = propuesta.UnitCost,
                        TotalCost = propuesta.TotalCost,
                        CostScopeWarehouseId = ambito,
                        CostMethod = p.Metodo,
                        AffectsEntryId = afectada.Id,
                    };
                    db.KardexEntries.Add(fila);
                    filas.Add(new LineaRetroactivaEscrita(fila, propuesta.Porcion ?? PorcionDelAjuste.EnExistencia));
                }
                ajustesRetroactivos.Add(new AjusteRetroactivoRegistrado((int)porDocumento.DocumentId, porDocumento.EnExistencia, porDocumento.Vendida, filas));
            }
        }

        // PEPS (T836): las capas que nacieron, los consumos y lo que queda de cada capa según el estado final del ámbito.
        if (p.Metodo == CostMethod.Fifo)
            EscribirCapas(plan.Select(x => (x.Mov.Producto, x.Ambito, x.Costo)).ToList(), estados, capasPorEntrada, propuestas, documento.OperationDate);

        if (ajustesRetroactivos.Count > 0)
        {
            if (!_retroactivosSinEmitir.TryGetValue(documento, out var pendientes)) _retroactivosSinEmitir[documento] = pendientes = [];
            pendientes.AddRange(ajustesRetroactivos);
        }

        // Proyecciones: el estado final de cada ámbito, la existencia de cada bodega y ubicación tocada.
        foreach (var (producto, ambito) in plan.Select(x => (x.Mov.Producto, x.Ambito)).Distinct())
        {
            if (!costos.TryGetValue((producto, ambito), out var fila))
            {
                fila = new CostState { ProductId = producto, ScopeWarehouseId = ambito };
                db.CostStates.Add(fila);
                costos[(producto, ambito)] = fila;
            }
            var final = estados[(producto, ambito)];
            fila.Method = p.Metodo;
            fila.Quantity = final.Quantity;
            fila.Value = final.Value;
            fila.AverageCost = final.AverageCost;
            fila.LastUnitCost = final.LastUnitCost;
        }
        foreach (var (producto, bodega) in plan.Select(x => (x.Mov.Producto, x.Mov.WarehouseId)).Distinct())
        {
            if (!existencias.TryGetValue((producto, bodega), out var fila))
            {
                fila = new StockBalance { ProductId = producto, WarehouseId = bodega };
                db.StockBalances.Add(fila);
                existencias[(producto, bodega)] = fila;
            }
            fila.Physical = fisicos[(producto, bodega)].Fisico;
            var fecha = fechas[(producto, bodega)];
            if (fila.LastMovementDate is null || fecha > fila.LastMovementDate) fila.LastMovementDate = fecha;
        }
        foreach (var (producto, bodega, ubicacion, lote) in plan.Select(x => (x.Mov.Producto, x.Mov.WarehouseId, x.Ubicacion, x.Mov.Lote)).Distinct())
        {
            if (!detalles.TryGetValue((producto, bodega, ubicacion, lote), out var fila))
            {
                fila = new StockDetail { ProductId = producto, WarehouseId = bodega, LocationId = ubicacion, LotId = lote };
                db.StockDetails.Add(fila);
                detalles[(producto, bodega, ubicacion, lote)] = fila;
            }
            fila.Quantity = porUbicacion[(producto, bodega, ubicacion, lote)];
        }

        // I6 (T924): la proyección de la serie —dónde está— en el orden de los movimientos (sale del origen, entra al tránsito).
        foreach (var (m, ubicacion, _, _) in plan)
        {
            if (m.Serie is not int serieId || !series.TryGetValue(serieId, out var serie)) continue;
            serie.InStockWarehouseId = m.QuantityBase > 0m ? m.WarehouseId : null;
            serie.InStockLocationId = m.QuantityBase > 0m ? ubicacion : null;
        }

        // La línea del documento: su costo (el valor de lo que movió) y la ubicación por defecto si no traía.
        foreach (var porLinea in escritos.GroupBy(e => e.Movimiento.Linea))
        {
            var primero = porLinea.First();
            var linea = porLinea.Key;
            linea.LocationId ??= primero.LocationId;
            // I6: los movimientos de la línea en el mismo sentido que el primero —los lotes de una salida repartida, los componentes de un
            // combo—; el otro sentido (la entrada al tránsito o a la otra ubicación) no es el costo de la línea.
            var mismoSentido = porLinea.Where(e => Math.Sign(e.Movimiento.QuantityBase) == Math.Sign(primero.Movimiento.QuantityBase)).ToList();
            var principales = mismoSentido.SelectMany(e => e.Lineas).Where(l => l.Kind != KardexEntryKind.CostAdjustment).ToList();
            var total = Math.Abs(principales.Sum(l => l.TotalCost));
            var cantidad = mismoSentido.Any(e => e.Movimiento.ProductId is not null) ? linea.QuantityBase : Math.Abs(principales.Sum(l => l.QuantityBase));
            linea.TotalCost = total;
            linea.UnitCost = principales.Count == 1 ? principales[0].UnitCost
                : cantidad == 0m ? 0m : Redondeo.CostoUnitario(total / cantidad);
        }
        documento.CostTotal = documento.Lines.Where(l => !l.IsDeleted).Sum(l => l.TotalCost ?? 0m);

        return Result.Success(new RegistroHecho(escritos) { AjustesRetroactivos = ajustesRetroactivos });
    }

    // ------------------------------------------------------------------------- diferencia de precio (US9) --

    /// <summary>
    /// Lo que bloquea un documento que sólo corrige costos (factura o nota del proveedor con diferencia de precio, US9, T341):
    /// los estados de costo de las entradas que corrige y sus bodegas.
    /// </summary>
    public async Task<Result<PedidoDeCerrojo>> CerrojoDeDiferenciasAsync(InventoryDocument documento, IReadOnlyList<DiferenciaDePrecioPedida> pedidas, CancellationToken ct)
    {
        if (pedidas.Count == 0) return Result.Success(new PedidoDeCerrojo());
        var bodegas = pedidas.Select(d => d.Entrada.WarehouseId).Distinct().ToList();
        var leidos = await LeerParametrosAsync(documento.OperationDate, bodegas, ct);
        if (leidos.IsFailure) return Result.Failure<PedidoDeCerrojo>(leidos.Error);
        var p = leidos.Value;
        return Result.Success(new PedidoDeCerrojo
        {
            Bodegas = bodegas,
            EstadosDeCosto = pedidas.Select(d => new ClaveDeEstadoDeCosto(d.Entrada.ProductId, p.AmbitoDe(d.Entrada.WarehouseId), p.Metodo)).Distinct().ToList(),
        });
    }

    /// <summary>
    /// Registra las diferencias de precio de una factura o nota del proveedor contra las entradas de sus recepciones (US9,
    /// T341; FR-050, E6): pide a <see cref="MotorDeCosteo.DiferenciaDePrecio"/> el reparto entre existencia y vendido, escribe
    /// las líneas <c>CostAdjustment</c> <c>PriceDifference</c> bajo el documento (fechadas en él, sobre la entrada) y mueve el
    /// valor del ámbito. Sin cantidades, no mira disponibilidad. Nunca guarda.
    /// </summary>
    public async Task<Result<IReadOnlyList<DiferenciaDePrecioRegistrada>>> RegistrarDiferenciasDePrecioAsync(
        InventoryDocument documento, IReadOnlyList<DiferenciaDePrecioPedida> pedidas, CancellationToken ct)
    {
        var conDiferencia = pedidas.Where(d => d.Diferencia != 0m).ToList();
        if (conDiferencia.Count == 0) return Result.Success<IReadOnlyList<DiferenciaDePrecioRegistrada>>([]);

        var bodegas = conDiferencia.Select(d => d.Entrada.WarehouseId).Distinct().ToList();
        var leidos = await LeerParametrosAsync(documento.OperationDate, bodegas, ct);
        if (leidos.IsFailure) return Result.Failure<IReadOnlyList<DiferenciaDePrecioRegistrada>>(leidos.Error);
        var p = leidos.Value;

        var productos = conDiferencia.Select(d => d.Entrada.ProductId).Distinct().ToList();
        var costos = (await db.CostStates.Where(c => productos.Contains(c.ProductId)).ToListAsync(ct))
            .ToDictionary(c => (c.ProductId, c.ScopeWarehouseId));
        var ahora = reloj.UtcNow;
        var hechas = new List<DiferenciaDePrecioRegistrada>(conDiferencia.Count);

        foreach (var pedida in conDiferencia)
        {
            var entrada = pedida.Entrada;
            var ambito = p.AmbitoDe(entrada.WarehouseId);
            if (!costos.TryGetValue((entrada.ProductId, ambito), out var fila))
            {
                fila = new CostState { ProductId = entrada.ProductId, ScopeWarehouseId = ambito, Method = p.Metodo };
                db.CostStates.Add(fila);
                costos[(entrada.ProductId, ambito)] = fila;
            }
            var capas = p.Metodo == CostMethod.Fifo ? await CapasVivasAsync(entrada.ProductId, ambito, ct) : [];
            var estado = new EstadoDeCosto(fila.Quantity, fila.Value, fila.AverageCost, fila.LastUnitCost) { Capas = CapasDe(capas, entrada.ProductId, ambito) };
            var resultado = MotorDeCosteo.DiferenciaDePrecio(estado,
                new PedidoDeDiferenciaDePrecio(ReferenciaDeKardex.A(entrada.Id), pedida.CantidadFacturada, pedida.Diferencia), p.Montos, p.Metodo);
            Revaluar(capas, resultado.Estado);

            var filas = new List<KardexEntry>(resultado.Lineas.Count);
            foreach (var propuesta in resultado.Lineas)
            {
                var escrita = new KardexEntry
                {
                    DocumentId = documento.Id,
                    DocumentLineId = pedida.Linea.Id,
                    ProductId = entrada.ProductId,
                    WarehouseId = entrada.WarehouseId,
                    LocationId = entrada.LocationId,
                    LotId = entrada.LotId,
                    OperationDate = documento.OperationDate,
                    RegisteredAt = ahora,
                    Kind = KardexEntryKind.CostAdjustment,
                    Reason = KardexReason.PriceDifference,
                    QuantityBase = 0m,
                    UnitCost = propuesta.UnitCost,
                    TotalCost = propuesta.TotalCost,
                    CostScopeWarehouseId = ambito,
                    CostMethod = p.Metodo,
                    AffectsEntryId = entrada.Id,
                };
                db.KardexEntries.Add(escrita);
                filas.Add(escrita);
            }
            fila.Method = p.Metodo;
            fila.Quantity = resultado.Estado.Quantity;
            fila.Value = resultado.Estado.Value;
            fila.AverageCost = resultado.Estado.AverageCost;
            fila.LastUnitCost = resultado.Estado.LastUnitCost;
            hechas.Add(new DiferenciaDePrecioRegistrada(pedida, DiferenciaDePrecio.EnExistencia(resultado), DiferenciaDePrecio.Vendida(resultado), filas));
        }
        return Result.Success<IReadOnlyList<DiferenciaDePrecioRegistrada>>(hechas);
    }

    // ------------------------------------------------------------------------- costos adicionales (US13) --

    /// <summary>
    /// Lo que bloquea un documento que sólo corrige el costo de unas entradas ya registradas (costos adicionales, US13, T800): los
    /// estados de costo de esas entradas y sus bodegas. El mismo cerrojo que <see cref="CerrojoDeDiferenciasAsync"/>. (nuevo)
    /// </summary>
    public async Task<Result<PedidoDeCerrojo>> CerrojoDeEntradasAsync(DateOnly fecha, IReadOnlyList<KardexEntry> entradas, CancellationToken ct)
    {
        if (entradas.Count == 0) return Result.Success(new PedidoDeCerrojo());
        var bodegas = entradas.Select(e => e.WarehouseId).Distinct().ToList();
        var leidos = await LeerParametrosAsync(fecha, bodegas, ct);
        if (leidos.IsFailure) return Result.Failure<PedidoDeCerrojo>(leidos.Error);
        var p = leidos.Value;
        return Result.Success(new PedidoDeCerrojo
        {
            Bodegas = bodegas,
            EstadosDeCosto = entradas.Select(e => new ClaveDeEstadoDeCosto(e.ProductId, p.AmbitoDe(e.WarehouseId), p.Metodo)).Distinct().ToList(),
        });
    }

    /// <summary>
    /// Registra los costos adicionales de un documento <c>LandedCost</c> (o su contrario, con el reparto en signo contrario) sobre las
    /// entradas de sus recepciones (US13, T800; FR-046; D5): pide a <see cref="MotorDeCosteo.CostoAdicional"/> las líneas
    /// <c>CostAdjustment</c> <c>LandedCost</c> —lo asignado sobre la entrada y, si algo ya salió, lo vendido con signo contrario—, las
    /// escribe bajo el documento con <c>AffectsEntryId</c> de la entrada, fechadas en él y en la bodega de la entrada, y mueve el valor del
    /// ámbito sólo por lo que quedó en existencia. Sin cantidades, no mira disponibilidad. Nunca guarda. (nuevo)
    /// </summary>
    public async Task<Result<IReadOnlyList<CostoAdicionalRegistrado>>> RegistrarCostosAdicionalesAsync(
        InventoryDocument documento, IReadOnlyList<CostoAdicionalPedido> pedidos, CancellationToken ct)
    {
        var conMonto = pedidos.Where(x => x.Reparto.AllocatedAmount != 0m).ToList();
        if (conMonto.Count == 0) return Result.Success<IReadOnlyList<CostoAdicionalRegistrado>>([]);

        var bodegas = conMonto.Select(x => x.Entrada.WarehouseId).Distinct().ToList();
        var leidos = await LeerParametrosAsync(documento.OperationDate, bodegas, ct);
        if (leidos.IsFailure) return Result.Failure<IReadOnlyList<CostoAdicionalRegistrado>>(leidos.Error);
        var p = leidos.Value;

        var productos = conMonto.Select(x => x.Entrada.ProductId).Distinct().ToList();
        var costos = (await db.CostStates.Where(c => productos.Contains(c.ProductId)).ToListAsync(ct))
            .ToDictionary(c => (c.ProductId, c.ScopeWarehouseId));
        var ahora = reloj.UtcNow;
        var hechos = new List<CostoAdicionalRegistrado>(conMonto.Count);

        foreach (var pedido in conMonto)
        {
            var entrada = pedido.Entrada;
            var ambito = p.AmbitoDe(entrada.WarehouseId);
            if (!costos.TryGetValue((entrada.ProductId, ambito), out var fila))
            {
                fila = new CostState { ProductId = entrada.ProductId, ScopeWarehouseId = ambito, Method = p.Metodo };
                db.CostStates.Add(fila);
                costos[(entrada.ProductId, ambito)] = fila;
            }
            var capas = p.Metodo == CostMethod.Fifo ? await CapasVivasAsync(entrada.ProductId, ambito, ct) : [];
            var estado = new EstadoDeCosto(fila.Quantity, fila.Value, fila.AverageCost, fila.LastUnitCost) { Capas = CapasDe(capas, entrada.ProductId, ambito) };
            var resultado = MotorDeCosteo.CostoAdicional(estado, ReferenciaDeKardex.A(entrada.Id), pedido.Reparto, p.Metodo, p.Montos);
            Revaluar(capas, resultado.Estado);

            var filas = new List<KardexEntry>(resultado.Lineas.Count);
            foreach (var propuesta in resultado.Lineas)
            {
                var escrita = new KardexEntry
                {
                    DocumentId = documento.Id,
                    DocumentLineId = pedido.Linea.Id,
                    ProductId = entrada.ProductId,
                    WarehouseId = entrada.WarehouseId,
                    LocationId = entrada.LocationId,
                    LotId = entrada.LotId,
                    OperationDate = documento.OperationDate,
                    RegisteredAt = ahora,
                    Kind = KardexEntryKind.CostAdjustment,
                    Reason = KardexReason.LandedCost,
                    QuantityBase = 0m,
                    UnitCost = propuesta.UnitCost,
                    TotalCost = propuesta.TotalCost,
                    CostScopeWarehouseId = ambito,
                    CostMethod = p.Metodo,
                    AffectsEntryId = entrada.Id,
                };
                db.KardexEntries.Add(escrita);
                filas.Add(escrita);
            }
            fila.Method = p.Metodo;
            fila.Quantity = resultado.Estado.Quantity;
            fila.Value = resultado.Estado.Value;
            fila.AverageCost = resultado.Estado.AverageCost;
            fila.LastUnitCost = resultado.Estado.LastUnitCost;
            hechos.Add(new CostoAdicionalRegistrado(pedido, Prorrateo.EnExistencia(resultado), Prorrateo.Vendida(resultado), filas));
        }
        return Result.Success<IReadOnlyList<CostoAdicionalRegistrado>>(hechos);
    }

    /// <summary>Las capas vivas de un ámbito, seguidas (T843): la diferencia de precio y los costos adicionales cambian su costo.</summary>
    private async Task<List<CostLayer>> CapasVivasAsync(int producto, int ambito, CancellationToken ct) =>
        await db.CostLayers.Where(c => c.ProductId == producto && c.ScopeWarehouseId == ambito && c.RemainingQuantity > 0m).ToListAsync(ct);

    /// <summary>T843: el costo nuevo de la capa que ajustó el motor (lo demás no cambia).</summary>
    private static void Revaluar(IReadOnlyList<CostLayer> capas, EstadoDeCosto estado)
    {
        foreach (var capa in estado.Capas)
            if (capa.Entrada.EntryId is { } id && capas.FirstOrDefault(c => c.EntryKardexEntryId == id) is { } entidad && entidad.UnitCost != capa.UnitCost)
                entidad.Revaluar(capa.UnitCost);
    }

    /// <summary>
    /// T843: lo que queda en existencia de cada entrada con PEPS —su capa restante— para la regla D5 del reparto de costos adicionales;
    /// sin capa viva, cero (ya salió todo). Nulo si el método vigente a <paramref name="fecha"/> no es PEPS.
    /// </summary>
    public async Task<IReadOnlyDictionary<long, decimal>?> RestantePorEntradaAsync(DateOnly fecha, IReadOnlyList<KardexEntry> entradas, CancellationToken ct)
    {
        var leidos = await LeerParametrosAsync(fecha, entradas.Select(e => e.WarehouseId).Distinct().ToList(), ct);
        if (leidos.IsFailure || leidos.Value.Metodo != CostMethod.Fifo) return null;
        var ids = entradas.Select(e => e.Id).ToList();
        var restantes = await db.CostLayers.Where(c => ids.Contains(c.EntryKardexEntryId)).ToDictionaryAsync(c => c.EntryKardexEntryId, c => c.RemainingQuantity, ct);
        return ids.Distinct().ToDictionary(id => id, id => restantes.GetValueOrDefault(id));
    }

    // ------------------------------------------------------------------------------------ retroactivo --

    /// <summary>
    /// Los ámbitos (producto, ámbito de costo) donde el documento dejaría un movimiento anterior a otro ya registrado. Si hay
    /// alguno y la clase no está exenta, <c>Inventory.Costing.RetroactiveNotAllowed</c> nombrando el primer movimiento posterior.
    /// </summary>
    private async Task<Result<HashSet<(int ProductId, int Ambito)>>> AmbitosRetroactivosAsync(
        InventoryDocument documento, IReadOnlyList<MovimientoDeKardex> movimientos, ParametrosDelKardex p, CancellationToken ct)
    {
        var claves = movimientos.Select(m => (ProductId: m.Producto, Ambito: p.AmbitoDe(m.WarehouseId))).ToHashSet();
        var productos = claves.Select(c => c.ProductId).Distinct().ToList();
        var fecha = documento.OperationDate;
        var posteriores = (await db.KardexEntries.AsNoTracking()
                .Where(k => productos.Contains(k.ProductId) && k.OperationDate > fecha)
                .GroupBy(k => new { k.ProductId, k.CostScopeWarehouseId })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.CostScopeWarehouseId,
                    Fecha = g.Min(k => k.OperationDate),
                    ConPeps = g.Any(k => k.CostMethod == CostMethod.Fifo || k.Reason == KardexReason.MethodChange),
                })
                .ToListAsync(ct))
            .Where(x => claves.Contains((x.ProductId, x.CostScopeWarehouseId)))
            .ToList();
        if (posteriores.Count == 0) return Result.Success(new HashSet<(int, int)>());

        var primero = movimientos.First(m => posteriores.Any(x => x.ProductId == m.Producto && x.CostScopeWarehouseId == p.AmbitoDe(m.WarehouseId)));
        var ambito = p.AmbitoDe(primero.WarehouseId);
        var admitidos = posteriores.Select(x => (x.ProductId, x.CostScopeWarehouseId)).ToHashSet();

        // D6 (T838, I5): con PEPS vigente, o con PEPS o un cambio de método después de la fecha, no hay retroactivo; tampoco para las dos
        // excepciones de I1, porque el motor no sabe reinsertar capas en el pasado (T42b).
        if (entrega >= EntregaDelComercio.I5 && (p.Metodo == CostMethod.Fifo || posteriores.Any(x => x.ConPeps)))
        {
            var conPeps = p.Metodo == CostMethod.Fifo ? primero
                : movimientos.First(m => posteriores.Any(x => x.ConPeps && x.ProductId == m.Producto && x.CostScopeWarehouseId == p.AmbitoDe(m.WarehouseId)));
            return Result.Failure<HashSet<(int, int)>>(await RequierePromedioAsync(conPeps, ct));
        }

        if (await EsExentoAsync(documento, ct)) return Result.Success(admitidos);

        var fechaPosterior = posteriores.First(x => x.ProductId == primero.Producto && x.CostScopeWarehouseId == ambito).Fecha;

        // US16 (T838): desde I5 el retroactivo general, con el parámetro y sus días máximos a la fecha de operación (FR-045). El
        // período cerrado ya lo rechazó el paso 1 (Inventory.Period.Closed).
        if (entrega >= EntregaDelComercio.I5)
        {
            var permitidos = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoRetroactivosPermitidos, fecha, ct: ct);
            if (permitidos.IsFailure) return Result.Failure<HashSet<(int, int)>>(permitidos.Error);
            if (permitidos.Value.Como<bool>())
            {
                var dias = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoRetroactivosDiasMaximos, fecha, ct: ct);
                if (dias.IsFailure) return Result.Failure<HashSet<(int, int)>>(dias.Error);
                var maximo = dias.Value.Como<int>();
                var primeraAdmitida = reloj.HoyLocal.AddDays(-maximo);
                return fecha < primeraAdmitida
                    ? Result.Failure<HashSet<(int, int)>>(InventoryErrors.RetroactiveTooOld(maximo, primeraAdmitida, fecha))
                    : Result.Success(admitidos);
            }
        }
        var posterior = await db.KardexEntries.AsNoTracking()
            .Where(k => k.ProductId == primero.Producto && k.CostScopeWarehouseId == ambito && k.OperationDate == fechaPosterior)
            .OrderBy(k => k.Id)
            .Join(db.InventoryDocuments, k => k.DocumentId, d => d.Id, (k, d) => new { d.PublicId, d.Prefix, d.Number })
            .FirstAsync(ct);
        var codigo = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == primero.Producto).Select(x => x.Code).FirstAsync(ct);
        return Result.Failure<HashSet<(int, int)>>(InventoryErrors.RetroactiveNotAllowed(primero.Linea.LineNumber, codigo, posterior.PublicId,
            Documents.VistaDeDocumentos.NumeroVisible(posterior.Prefix, posterior.Number), fechaPosterior));
    }

    /// <summary>
    /// Las dos clases que I1 admite con fecha anterior sin mirar <c>Costeo.RetroactivosPermitidos</c> (FR-045; preguntas D8 y
    /// D9, propuesta por defecto): el saldo inicial de una bodega <c>NotActivated</c> y su anulación, y los ajustes que genera un
    /// conteo aprobado (enlazados <c>CountAdjustmentOf</c>).
    /// </summary>
    public async Task<bool> EsExentoAsync(InventoryDocument documento, CancellationToken ct)
    {
        var saldoInicial = documento;
        if (documento.Class == DocumentClass.Voiding && documento.VoidsDocumentId is int anulado)
            saldoInicial = await db.InventoryDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == anulado, ct) ?? documento;
        if (saldoInicial.Class == DocumentClass.OpeningBalance && saldoInicial.WarehouseId is int bodega
            && await db.Warehouses.AsNoTracking().AnyAsync(w => w.Id == bodega && w.ActivationStatus == WarehouseActivationStatus.NotActivated, ct))
            return true;

        if (documento.Class is not (DocumentClass.PositiveAdjustment or DocumentClass.NegativeAdjustment)) return false;
        return db.DocumentLinks.Local.Any(l => l.Kind == DocumentLinkKind.CountAdjustmentOf && (l.SourceDocumentId == documento.Id || l.TargetDocumentId == documento.Id || l.SourceDocument == documento || l.TargetDocument == documento))
            || (documento.Id != 0 && await db.DocumentLinks.AsNoTracking().AnyAsync(l => l.Kind == DocumentLinkKind.CountAdjustmentOf
                && (l.SourceDocumentId == documento.Id || l.TargetDocumentId == documento.Id), ct));
    }

    /// <summary>
    /// El pedido del retroactivo para un ámbito: el estado a la fecha del documento (Σ del kardex en o antes de esa fecha, con
    /// el último costo de entrada) y la historia posterior, un <see cref="MovimientoRegistrado"/> por línea de entrada o salida
    /// con el valor de sus ajustes (los del mismo movimiento, y los retroactivos que ya lo afectaron). Cómo se valoró cada uno se
    /// reconstruye del kardex: la reversión de una salida o la entrada al tránsito siguen el costo de su origen, la reversión de
    /// una entrada sale al costo con que entró, toda otra salida sale al promedio y toda otra entrada entra a su costo registrado.
    /// </summary>
    private async Task<(PedidoRetroactivo Pedido, IReadOnlyDictionary<long, KardexEntry> Afectadas)> PedidoRetroactivoAsync(
        InventoryDocument documento, int producto, int ambito, IReadOnlyList<MovimientoDeKardex> nuevos, ParametrosDeCosteo parametrosDeCosteo, CancellationToken ct)
    {
        var fecha = documento.OperationDate;
        var delAmbito = db.KardexEntries.AsNoTracking().Where(k => k.ProductId == producto && k.CostScopeWarehouseId == ambito);

        var antes = await delAmbito.Where(k => k.OperationDate <= fecha)
            .GroupBy(k => 1)
            .Select(g => new { Cantidad = g.Sum(k => k.QuantityBase), Valor = g.Sum(k => k.TotalCost) })
            .FirstOrDefaultAsync(ct);
        var ultimoCosto = await delAmbito.Where(k => k.OperationDate <= fecha && k.Kind == KardexEntryKind.Entry)
            .OrderByDescending(k => k.OperationDate).ThenByDescending(k => k.Id).Select(k => (decimal?)k.UnitCost).FirstOrDefaultAsync(ct) ?? 0m;
        var inicial = EstadoDeCosto.Con(antes?.Cantidad ?? 0m, antes?.Valor ?? 0m, ultimoCosto);

        var posteriores = await delAmbito.Where(k => k.OperationDate > fecha).OrderBy(k => k.OperationDate).ThenBy(k => k.Id).ToListAsync(ct);
        var documentos = posteriores.Select(k => k.DocumentId).Distinct().ToList();
        var clases = await db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters().Where(d => documentos.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Class, ct);

        var principales = posteriores.Where(k => k.Kind != KardexEntryKind.CostAdjustment).ToList();
        var valor = principales.ToDictionary(k => k.Id, k => k.TotalCost);
        foreach (var ajuste in posteriores.Where(k => k.Kind == KardexEntryKind.CostAdjustment))
        {
            var duenio = ajuste.Reason == KardexReason.Retroactive && ajuste.AffectsEntryId is long afectada && valor.ContainsKey(afectada)
                ? afectada
                : principales.Where(k => k.DocumentId == ajuste.DocumentId && k.DocumentLineId == ajuste.DocumentLineId && k.Id < ajuste.Id)
                    .Select(k => (long?)k.Id).LastOrDefault()
                  ?? (ajuste.AffectsEntryId is long a && valor.ContainsKey(a) ? a : null);
            if (duenio is long d) valor[d] += ajuste.TotalCost;
        }

        var historia = principales.Select(k =>
        {
            var clase = clases.GetValueOrDefault(k.DocumentId);
            MovimientoDeCosto movimiento;
            if (k.ReversesEntryId is long revertida)
                movimiento = new MovimientoDeCosto(k.QuantityBase,
                    k.Kind == KardexEntryKind.Exit ? ValoracionDelMovimiento.DevolucionDeEntrada : ValoracionDelMovimiento.AlCostoDeOrigen,
                    k.UnitCost, ReferenciaDeKardex.A(revertida), EsAnulacion: true);
            else if (k.Kind == KardexEntryKind.Exit)
                movimiento = clase switch
                {
                    DocumentClass.SupplierReturn => new MovimientoDeCosto(k.QuantityBase, ValoracionDelMovimiento.DevolucionDeEntrada, k.UnitCost),
                    // US10: la salida del tránsito va al costo de la línea de despacho, no al promedio.
                    DocumentClass.TransferReceipt => new MovimientoDeCosto(k.QuantityBase, ValoracionDelMovimiento.AlCostoDeOrigen, k.UnitCost),
                    _ => new MovimientoDeCosto(k.QuantityBase, ValoracionDelMovimiento.AlCostoVigente),
                };
            else if (principales.FirstOrDefault(s => s.Kind == KardexEntryKind.Exit && s.DocumentId == k.DocumentId && s.DocumentLineId == k.DocumentLineId && s.Id < k.Id) is { } salida)
                movimiento = new MovimientoDeCosto(k.QuantityBase, ValoracionDelMovimiento.AlCostoDeOrigen, k.UnitCost, ReferenciaDeKardex.A(salida.Id));
            else
                movimiento = new MovimientoDeCosto(k.QuantityBase, ValoracionDelMovimiento.AlCostoIndicado, k.UnitCost);
            var sale = k.Kind == KardexEntryKind.Exit && clase is not (DocumentClass.TransferDispatch or DocumentClass.TransferReceipt or DocumentClass.LocationMove);
            return new MovimientoRegistrado(k.Id, k.DocumentId, k.OperationDate, movimiento, valor[k.Id], k.UnitCost, sale);
        }).ToList();

        var pedido = new PedidoRetroactivo(inicial, historia,
            nuevos.Select(m => new MovimientoRetroactivo(fecha, documento.Id,
                new MovimientoDeCosto(m.QuantityBase, m.Valoracion, m.CostoUnitario,
                    m.Origen is null ? null : new ReferenciaDeKardex(m.Origen.Id == 0 ? null : m.Origen.Id, null), m.EsAnulacion))).ToList(),
            parametrosDeCosteo);
        return (pedido, principales.ToDictionary(k => k.Id));
    }

    // ------------------------------------------------------------------------------------------ PEPS (T836) --

    /// <summary>Las capas vivas de un ámbito como las pide el motor, en orden PEPS <c>(OperationDate, EntryKardexEntryId)</c>.</summary>
    private static IReadOnlyList<CapaDeCosto> CapasDe(IEnumerable<CostLayer> capas, int producto, int ambito) =>
        capas.Where(c => c.ProductId == producto && c.ScopeWarehouseId == ambito && c.RemainingQuantity > 0m)
            .OrderBy(c => c.OperationDate).ThenBy(c => c.EntryKardexEntryId)
            .Select(c => new CapaDeCosto(ReferenciaDeKardex.A(c.EntryKardexEntryId), c.OperationDate, c.OriginalQuantity, c.RemainingQuantity, c.UnitCost))
            .ToList();

    /// <summary>
    /// Escribe lo que el motor dejó en PEPS (T836; data-model §3.5): una <c>INV_CostLayers</c> por capa nueva (aunque nazca consumida),
    /// un <c>INV_LayerConsumptions</c> por consumo —de la salida o, con cantidad negativa, de la anulación que devuelve— y lo que queda de
    /// cada capa del ámbito según su estado final (la que no aparece quedó agotada). Sólo agrega al seguimiento; nunca guarda.
    /// </summary>
    private void EscribirCapas(
        IReadOnlyList<(int ProductId, int Ambito, ResultadoDeCosteo Costo)> plan,
        IReadOnlyDictionary<(int, int), EstadoDeCosto> estados,
        IReadOnlyDictionary<long, CostLayer> capasPorEntrada,
        IReadOnlyDictionary<LineaDeKardexPropuesta, KardexEntry> propuestas,
        DateOnly fecha)
    {
        var nuevas = new Dictionary<LineaDeKardexPropuesta, CostLayer>(ReferenceEqualityComparer.Instance);
        foreach (var (producto, ambito, costo) in plan)
        {
            foreach (var capa in costo.CapasNuevas)
            {
                if (capa.Entrada.Linea is not { } linea || nuevas.ContainsKey(linea)) continue;
                var entidad = CostLayer.Desde(producto, ambito, propuestas[linea], capa.OperationDate ?? fecha, capa.OriginalQuantity, capa.OriginalQuantity, capa.UnitCost);
                db.CostLayers.Add(entidad);
                nuevas[linea] = entidad;
            }
        }

        CostLayer Entidad(ReferenciaDeKardex entrada) =>
            entrada.Linea is { } l && nuevas.TryGetValue(l, out var nueva) ? nueva
            : entrada.EntryId is { } id && capasPorEntrada.TryGetValue(id, out var existente) ? existente
            : entrada.Linea is { } propuesta && propuestas.TryGetValue(propuesta, out var fila) && capasPorEntrada.TryGetValue(fila.Id, out var porFila) ? porFila
            : throw new InvalidOperationException("El motor nombró una capa que el registro no conoce.");

        foreach (var (_, _, costo) in plan)
        {
            foreach (var consumo in costo.Consumos)
            {
                var (salidaId, salida) = consumo.Salida.Linea is { } l
                    ? (propuestas[l].Id, propuestas[l].Id == 0 ? propuestas[l] : null)
                    : (consumo.Salida.EntryId ?? 0L, (KardexEntry?)null);
                var capa = Entidad(consumo.Capa.Entrada);
                db.LayerConsumptions.Add(new LayerConsumption
                {
                    ExitKardexEntryId = salidaId,
                    ExitKardexEntry = salida,
                    LayerId = capa.Id,
                    Layer = capa.Id == 0 ? capa : null,
                    Quantity = consumo.Quantity,
                    UnitCost = consumo.UnitCost,
                });
            }
        }

        foreach (var (producto, ambito) in plan.Select(x => (x.ProductId, x.Ambito)).Distinct())
        {
            var vistas = new HashSet<CostLayer>(ReferenceEqualityComparer.Instance);
            foreach (var capa in estados[(producto, ambito)].Capas)
            {
                var entidad = Entidad(capa.Entrada);
                entidad.Reconstruir(capa.RemainingQuantity);
                if (entidad.UnitCost != capa.UnitCost) entidad.Revaluar(capa.UnitCost);
                vistas.Add(entidad);
            }
            foreach (var agotada in capasPorEntrada.Values.Concat(nuevas.Values)
                         .Where(c => c.ProductId == producto && c.ScopeWarehouseId == ambito && !vistas.Contains(c) && c.RemainingQuantity != 0m))
                agotada.Reconstruir(0m);
        }
    }

    /// <summary>D6: el rechazo con PEPS, nombrando la línea y el producto.</summary>
    private async Task<Error> RequierePromedioAsync(MovimientoDeKardex m, CancellationToken ct)
    {
        var codigo = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == m.Producto).Select(x => x.Code).FirstAsync(ct);
        return InventoryErrors.RetroactiveRequiresWeightedAverage(m.Linea.LineNumber, codigo);
    }

    // ------------------------------------------------------------------------------ cambio de método (T836) --

    /// <summary>
    /// Lo que bloquea el cambio de método o de ámbito (US16, T841): los estados de costo de los dos ámbitos de cada línea.
    /// </summary>
    public static PedidoDeCerrojo CerrojoDelCambio(IReadOnlyList<LineaDeCambioDeMetodo> lineas, CostMethod metodo) => new()
    {
        Bodegas = lineas.Select(l => l.WarehouseId).Distinct().ToList(),
        EstadosDeCosto = lineas.Select(l => new ClaveDeEstadoDeCosto(l.Linea.ProductId, l.Ambito, metodo)).Distinct().ToList(),
    };

    /// <summary>
    /// Escribe el cambio de método o de ámbito de costeo (US16, T836, T841; FR-043; data-model §3.1, §3.5): una línea
    /// <c>MethodChange</c> por cada <see cref="LineaDeCambioDeMetodo"/> —sin cantidad en un cambio de método; con la cantidad que pasa de un
    /// ámbito al otro en uno de ámbito (<c>Exit</c> del anterior, <c>Entry</c> al nuevo)—, las capas PEPS que abren, el cierre de las capas
    /// del método anterior, y los estados de costo con el método nuevo. <paramref name="estadosFinales"/> es el estado de cada ámbito
    /// después del cambio (lo calculó <c>MotorDeCosteo.CambiarMetodo</c> o el reparto del cambio de ámbito). Nunca guarda.
    /// </summary>
    public async Task RegistrarCambioDeMetodoAsync(
        InventoryDocument documento, IReadOnlyList<LineaDeCambioDeMetodo> lineas, CostMethod metodo,
        IReadOnlyDictionary<(int ProductId, int Ambito), EstadoDeCosto> estadosFinales, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var productos = estadosFinales.Keys.Select(k => k.ProductId).Distinct().ToList();
        var costos = (await db.CostStates.Where(c => productos.Contains(c.ProductId)).ToListAsync(ct)).ToDictionary(c => (c.ProductId, c.ScopeWarehouseId));
        var vivas = await db.CostLayers.Where(c => productos.Contains(c.ProductId) && c.RemainingQuantity > 0m).ToListAsync(ct);

        foreach (var l in lineas)
        {
            var fila = new KardexEntry
            {
                DocumentId = documento.Id,
                DocumentLineId = l.Linea.Id,
                ProductId = l.Linea.ProductId,
                WarehouseId = l.WarehouseId,
                LocationId = l.LocationId,
                OperationDate = documento.OperationDate,
                RegisteredAt = ahora,
                Kind = l.QuantityBase > 0m ? KardexEntryKind.Entry : l.QuantityBase < 0m ? KardexEntryKind.Exit : KardexEntryKind.CostAdjustment,
                Reason = KardexReason.MethodChange,
                QuantityBase = l.QuantityBase,
                UnitCost = l.UnitCost,
                TotalCost = l.TotalCost,
                CostScopeWarehouseId = l.Ambito,
                CostMethod = metodo,
            };
            db.KardexEntries.Add(fila);
            if (l.Capa is { OriginalQuantity: > 0m } capa)
                db.CostLayers.Add(CostLayer.Desde(l.Linea.ProductId, l.Ambito, fila, documento.OperationDate, capa.OriginalQuantity, capa.RemainingQuantity, capa.UnitCost));
        }

        // Las capas del régimen anterior se cierran: desde el cambio, cada ámbito parte de su capa única (o del promedio).
        foreach (var capa in vivas) capa.Reconstruir(0m);

        foreach (var ((producto, ambito), final) in estadosFinales)
        {
            if (!costos.TryGetValue((producto, ambito), out var fila))
            {
                fila = new CostState { ProductId = producto, ScopeWarehouseId = ambito };
                db.CostStates.Add(fila);
                costos[(producto, ambito)] = fila;
            }
            fila.Method = metodo;
            fila.Quantity = final.Quantity;
            fila.Value = final.Value;
            fila.AverageCost = final.AverageCost;
            fila.LastUnitCost = final.LastUnitCost;
        }
        documento.CostTotal = 0m;
    }

    // ------------------------------------------------------------------------------------------ apoyo --

    /// <summary>El costo unitario con que salió un movimiento ya calculado (el de su línea principal, o el promedio de sus partes).</summary>
    private static decimal CostoUnitarioDe(ResultadoDeCosteo costo)
    {
        var principales = costo.Lineas.Where(l => l.Kind != KardexEntryKind.CostAdjustment).ToList();
        if (principales.Count == 1) return principales[0].UnitCost;
        var cantidad = Math.Abs(principales.Sum(l => l.QuantityBase));
        return cantidad == 0m ? 0m : Redondeo.CostoUnitario(Math.Abs(principales.Sum(l => l.TotalCost)) / cantidad);
    }

    /// <summary>Los parámetros del registro a la fecha de operación.</summary>
    public async Task<Result<ParametrosDelKardex>> LeerParametrosAsync(DateOnly fecha, IReadOnlyCollection<int> bodegas, CancellationToken ct)
    {
        var metodo = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoMetodo, fecha, ct: ct);
        if (metodo.IsFailure) return Result.Failure<ParametrosDelKardex>(metodo.Error);
        var ambito = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoAmbito, fecha, ct: ct);
        if (ambito.IsFailure) return Result.Failure<ParametrosDelKardex>(ambito.Error);
        var montos = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.RedondeoMontos, fecha, ct: ct);
        if (montos.IsFailure) return Result.Failure<ParametrosDelKardex>(montos.Error);

        var negativo = new Dictionary<int, bool>();
        foreach (var bodega in bodegas)
        {
            var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ExistenciasStockNegativoPermitido, fecha,
                ParameterScopeKind.Warehouse, bodega, ct);
            if (leido.IsFailure) return Result.Failure<ParametrosDelKardex>(leido.Error);
            negativo[bodega] = leido.Value.Como<bool>();
        }

        return Result.Success(new ParametrosDelKardex(
            metodo.Value.Texto == "Peps" ? CostMethod.Fifo : CostMethod.WeightedAverage,
            ambito.Value.Texto == "Bodega" ? CostScope.Warehouse : CostScope.Cooperative,
            Redondeo.MontosDesde(montos.Value.Texto),
            negativo));
    }

    /// <summary>La ubicación por defecto (<c>IsDefault</c>, activa) de cada bodega de un movimiento sin ubicación.</summary>
    private async Task<Result<IReadOnlyDictionary<int, int>>> UbicacionesPorDefectoAsync(IReadOnlyList<MovimientoDeKardex> movimientos, CancellationToken ct)
    {
        var sinUbicacion = movimientos.Where(m => (m.LocationId ?? m.Linea.LocationId) is null).Select(m => m.WarehouseId).Distinct().ToList();
        if (sinUbicacion.Count == 0) return Result.Success<IReadOnlyDictionary<int, int>>(new Dictionary<int, int>());

        var porDefecto = await db.WarehouseLocations.AsNoTracking()
            .Where(l => sinUbicacion.Contains(l.WarehouseId) && l.IsDefault && l.IsActive)
            .Select(l => new { l.WarehouseId, l.Id })
            .ToListAsync(ct);
        var mapa = porDefecto.GroupBy(l => l.WarehouseId).ToDictionary(g => g.Key, g => g.Min(l => l.Id));
        return sinUbicacion.All(mapa.ContainsKey)
            ? Result.Success<IReadOnlyDictionary<int, int>>(mapa)
            : Result.Failure<IReadOnlyDictionary<int, int>>(new Error("Inventory.Location.NotFound", "La bodega no tiene ubicación por defecto."));
    }

    private static int UbicacionDe(MovimientoDeKardex m, IReadOnlyDictionary<int, int> porDefecto) =>
        m.LocationId ?? m.Linea.LocationId ?? porDefecto[m.WarehouseId];

    /// <summary>
    /// Una referencia del motor como FK (fila ya escrita) o como navegación (fila de este mismo cálculo o un origen todavía sin
    /// guardar).
    /// </summary>
    private static (long? Id, KardexEntry? Fila) Resolver(ReferenciaDeKardex? referencia, MovimientoDeKardex m, Dictionary<LineaDeKardexPropuesta, KardexEntry> propuestas)
    {
        if (referencia is null) return (null, null);
        if (referencia.Linea is { } propuesta)
            return propuestas.TryGetValue(propuesta, out var fila)
                ? (fila.Id == 0 ? null : fila.Id, fila.Id == 0 ? fila : null)
                : throw new InvalidOperationException("El motor referenció una línea propuesta que todavía no se escribió.");
        if (referencia.EntryId is { } id) return (id, null);
        return m.Origen is { } origen ? (null, origen) : (null, null);
    }

    // ------------------------------------------------------------------------------ lotes y series (I6) --

    /// <summary>
    /// Las clases de venta en las que manda <c>Ventas.LoteVencido</c> (I6, T923; FR-026): la remisión, la factura, el documento equivalente
    /// del POS y el comprobante no electrónico. En las demás salidas (ajustes, bajas, traslados) un lote vencido sale sin política: la baja
    /// por vencimiento existe justamente para sacarlo. (nuevo)
    /// </summary>
    public static readonly IReadOnlySet<DocumentClass> ClasesDeVenta = new HashSet<DocumentClass>
    {
        DocumentClass.Shipment, DocumentClass.SalesInvoice, DocumentClass.PosEquivalentDocument, DocumentClass.NonElectronicSalesReceipt,
    };

    /// <summary>
    /// Lo que dejó el seguimiento: los movimientos con su lote y su serie, las series que tocan (seguidas: su proyección la escribe el
    /// registro) y los productos que se controlan por lote. (nuevo)
    /// </summary>
    private sealed record SeguimientoDelRegistro(
        IReadOnlyList<MovimientoDeKardex> Movimientos,
        IReadOnlyDictionary<int, Domain.Entities.Inventory.Catalog.Serial> Series,
        IReadOnlySet<int> ConLote);

    /// <summary>El valor de <c>Ventas.LoteVencido</c> a la fecha; si no se puede leer (una entrega anterior a I6), el defecto seguro <c>Bloquear</c>.</summary>
    public Task<Domain.Inventory.Tracking.PoliticaDeLoteVencido> PoliticaDeLoteVencidoAsync(DateOnly fecha, CancellationToken ct) =>
        Documents.ReglasDeSeguimiento.PoliticaAsync(parametros, fecha, ct);

    /// <summary>
    /// Lote y serie de los movimientos, dentro del cerrojo (I6, T923, T924; FR-026; data-model §1.11, §13):
    /// <list type="bullet">
    /// <item>una salida sin lote de un producto con lote se reparte por <see cref="Domain.Inventory.Tracking.SelectorDeLotes"/> (el que vence
    /// primero) sobre la existencia por lote de su ubicación; en una clase de venta con <c>Bloquear</c> los vencidos quedan fuera y, si sólo
    /// ellos alcanzan, <c>Inventory.Lot.Expired</c>; si ni así alcanza, la salida queda sin repartir y la disponibilidad la rechaza;</item>
    /// <item>un lote elegido y vencido en una venta con <c>Bloquear</c>: <c>Inventory.Lot.Expired</c>;</item>
    /// <item>una entrada de un producto con lote sin lote: <c>Inventory.Lot.Required</c>;</item>
    /// <item>la entrada que viaja al costo de una salida (tránsito, otra ubicación) hereda su lote y su serie, partida como ella;</item>
    /// <item>serie: obligatoria, de una unidad, y en orden: no entra la que ya está en existencia (<c>.AlreadyInStock</c>) ni sale la que no
    /// está en esa bodega (<c>.NotInStock</c>). La fila de la serie ya está bloqueada (T925).</item>
    /// </list>
    /// </summary>
    private async Task<Result<SeguimientoDelRegistro>> SeguimientoAsync(InventoryDocument documento, IReadOnlyList<MovimientoDeKardex> movimientos,
        IReadOnlyDictionary<int, int> porDefecto, CancellationToken ct)
    {
        var ids = movimientos.Select(m => m.Producto).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id) && (x.TracksLot || x.TracksSerial))
            .Select(x => new { x.Id, x.Code, x.TracksLot, x.TracksSerial })
            .ToDictionaryAsync(x => x.Id, ct);
        var vacio = new Dictionary<int, Domain.Entities.Inventory.Catalog.Serial>();
        if (productos.Count == 0) return Result.Success(new SeguimientoDelRegistro(movimientos, vacio, new HashSet<int>()));

        var conLote = productos.Values.Where(x => x.TracksLot).Select(x => x.Id).ToHashSet();
        var esVenta = ClasesDeVenta.Contains(documento.Class);
        var hoy = reloj.HoyLocal;
        var politica = esVenta ? await PoliticaDeLoteVencidoAsync(hoy, ct) : Domain.Inventory.Tracking.PoliticaDeLoteVencido.Advertir;

        // La existencia por lote de las salidas que hay que repartir (y de los lotes elegidos, para su vencimiento).
        var aRepartir = movimientos.Where(m => m.QuantityBase < 0m && conLote.Contains(m.Producto) && m.Lote is null && m.AlCostoDe is null).ToList();
        var productosARepartir = aRepartir.Select(m => m.Producto).Distinct().ToList();
        var bodegasARepartir = aRepartir.Select(m => m.WarehouseId).Distinct().ToList();
        var porLote = aRepartir.Count == 0
            ? []
            : await db.StockDetails.AsNoTracking()
                .Where(s => productosARepartir.Contains(s.ProductId) && bodegasARepartir.Contains(s.WarehouseId) && s.LotId != null && s.Quantity > 0m)
                .ToListAsync(ct);
        var lotesCitados = porLote.Select(s => s.LotId!.Value).Concat(movimientos.Select(m => m.Lote).OfType<int>()).Distinct().ToList();
        var lotes = await db.Lots.AsNoTracking().IgnoreQueryFilters().Where(l => lotesCitados.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, ct);
        var tomado = new Dictionary<(int, int, int, int), decimal>();

        var expandidos = new Dictionary<MovimientoDeKardex, IReadOnlyList<MovimientoDeKardex>>(ReferenceEqualityComparer.Instance);
        var resultado = new List<MovimientoDeKardex>(movimientos.Count);
        foreach (var m in movimientos)
        {
            if (!productos.TryGetValue(m.Producto, out var producto))
            {
                var propio = m.AlCostoConsumidoDe is { Count: > 0 } c
                    ? m with { AlCostoConsumidoDe = c.SelectMany(x => expandidos.TryGetValue(x, out var e) ? e : [x]).ToList() }
                    : m;
                expandidos[m] = [propio];
                resultado.Add(propio);
                continue;
            }

            IReadOnlyList<MovimientoDeKardex> nuevos;
            if (m.AlCostoDe is { } salida)
            {
                // La entrada al tránsito o a la otra ubicación: una por cada parte de su salida, con su lote y su serie.
                var partes = expandidos.TryGetValue(salida, out var e) ? e : [salida];
                nuevos = partes.Select(s => m with
                {
                    QuantityBase = Math.Abs(s.QuantityBase),
                    AlCostoDe = s,
                    LotId = m.LotId ?? s.Lote,
                    SerialId = m.SerialId ?? s.Serie,
                }).ToList();
            }
            else if (producto.TracksLot && m.QuantityBase < 0m && m.Lote is null)
            {
                var ubicacion = UbicacionDe(m, porDefecto);
                var disponibles = porLote
                    .Where(s => s.ProductId == m.Producto && s.WarehouseId == m.WarehouseId && s.LocationId == ubicacion)
                    .Select(s =>
                    {
                        var lote = lotes[s.LotId!.Value];
                        var libre = s.Quantity - tomado.GetValueOrDefault((m.Producto, m.WarehouseId, ubicacion, lote.Id));
                        return new Domain.Inventory.Tracking.LoteDisponible(lote.Id, lote.Code, lote.ExpiryDate, libre);
                    })
                    .ToList();
                var reparto = Domain.Inventory.Tracking.SelectorDeLotes.Repartir(
                    new Domain.Inventory.Tracking.PedidoDeLotes(disponibles, Math.Abs(m.QuantityBase), hoy, politica) { AdmiteVencidos = !esVenta });
                if (!reparto.Completo)
                {
                    // Sólo lo vencido alcanzaría: en una venta con Bloquear, el lote vencido es el motivo.
                    if (reparto.Excluidos.Sum(x => x.Disponible) >= reparto.Faltante && reparto.Excluidos.Count > 0)
                    {
                        var vencido = reparto.Excluidos[0];
                        return Result.Failure<SeguimientoDelRegistro>(Catalog.CatalogErrors.LotExpired(producto.Code, vencido.Codigo, vencido.Vencimiento));
                    }
                    nuevos = [m];
                }
                else
                {
                    nuevos = reparto.Asignaciones.Select(a => m with { QuantityBase = -a.Cantidad, LotId = a.Lote.LotId }).ToList();
                    foreach (var a in reparto.Asignaciones)
                    {
                        var clave = (m.Producto, m.WarehouseId, ubicacion, a.Lote.LotId);
                        tomado[clave] = tomado.GetValueOrDefault(clave) + a.Cantidad;
                    }
                }
            }
            else
            {
                nuevos = [m];
            }

            foreach (var n in nuevos)
            {
                if (producto.TracksLot && n.Lote is null && n.QuantityBase > 0m)
                    return Result.Failure<SeguimientoDelRegistro>(Catalog.CatalogErrors.LotRequired(producto.Code));
                if (producto.TracksLot && n.Lote is int elegido && n.QuantityBase < 0m && esVenta && !n.EsAnulacion
                    && politica == Domain.Inventory.Tracking.PoliticaDeLoteVencido.Bloquear
                    && lotes.TryGetValue(elegido, out var delLote) && Domain.Inventory.Tracking.SelectorDeLotes.EstaVencido(delLote.ExpiryDate, hoy))
                    return Result.Failure<SeguimientoDelRegistro>(Catalog.CatalogErrors.LotExpired(producto.Code, delLote.Code, delLote.ExpiryDate));
            }
            expandidos[m] = nuevos;
            resultado.AddRange(nuevos);
        }

        // Series: obligatorias, de una unidad, y en el orden de los movimientos.
        var seriesCitadas = resultado.Where(m => productos.TryGetValue(m.Producto, out var x) && x.TracksSerial).Select(m => m.Serie).OfType<int>().Distinct().ToList();
        var series = seriesCitadas.Count == 0
            ? vacio
            : await db.Serials.Where(s => seriesCitadas.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var donde = series.ToDictionary(s => s.Key, s => s.Value.InStockWarehouseId);
        foreach (var m in resultado)
        {
            if (!productos.TryGetValue(m.Producto, out var producto) || !producto.TracksSerial) continue;
            if (m.Serie is not int serieId || !series.TryGetValue(serieId, out var serie))
                return Result.Failure<SeguimientoDelRegistro>(Catalog.CatalogErrors.SerialRequired(producto.Code));
            if (Math.Abs(m.QuantityBase) != 1m)
                return Result.Failure<SeguimientoDelRegistro>(Catalog.CatalogErrors.SerialQuantityNotOne(producto.Code, serie.SerialNumber));
            if (m.QuantityBase > 0m)
            {
                if (donde[serieId] is not null) return Result.Failure<SeguimientoDelRegistro>(Catalog.CatalogErrors.SerialAlreadyInStock(producto.Code, serie.SerialNumber));
                donde[serieId] = m.WarehouseId;
            }
            else
            {
                if (donde[serieId] != m.WarehouseId) return Result.Failure<SeguimientoDelRegistro>(Catalog.CatalogErrors.SerialNotInStock(producto.Code, serie.SerialNumber));
                donde[serieId] = null;
            }
        }

        return Result.Success(new SeguimientoDelRegistro(resultado, series, conLote));
    }

    private async Task<Error> ErrorDeExistenciaAsync(
        List<(MovimientoDeKardex Mov, int Ubicacion, decimal Disponible, bool PorUbicacion)> faltantes, string? sugerencia, CancellationToken ct)
    {
        var productoIds = faltantes.Select(f => f.Mov.Producto).Distinct().ToList();
        var bodegaIds = faltantes.Select(f => f.Mov.WarehouseId).Distinct().ToList();
        var ubicacionIds = faltantes.Select(f => f.Ubicacion).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(x => productoIds.Contains(x.Id)).Select(x => new { x.Id, x.PublicId, x.Code }).ToDictionaryAsync(x => x.Id, ct);
        var bodegas = await db.Warehouses.AsNoTracking().Where(x => bodegaIds.Contains(x.Id)).Select(x => new { x.Id, x.PublicId }).ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);
        var ubicaciones = await db.WarehouseLocations.AsNoTracking().Where(x => ubicacionIds.Contains(x.Id)).Select(x => new { x.Id, x.PublicId }).ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);

        var lineas = faltantes.Select(f =>
        {
            var producto = productos.GetValueOrDefault(f.Mov.Producto);
            return new InventoryErrors.LineaSinExistencia(
                f.Mov.Linea.LineNumber,
                producto?.PublicId ?? Guid.Empty,
                producto?.Code ?? string.Empty,
                bodegas.GetValueOrDefault(f.Mov.WarehouseId),
                f.PorUbicacion || f.Mov.LocationId is not null || f.Mov.Linea.LocationId is not null ? ubicaciones.GetValueOrDefault(f.Ubicacion) : null,
                Math.Abs(f.Mov.QuantityBase),
                f.Disponible);
        }).ToList();
        return InventoryErrors.StockInsufficient(lineas, sugerencia);
    }
}
