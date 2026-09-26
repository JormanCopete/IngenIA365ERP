using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
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
public sealed record MovimientoDeKardex(
    InventoryDocumentLine Linea,
    int WarehouseId,
    decimal QuantityBase,
    ValoracionDelMovimiento Valoracion,
    decimal? CostoUnitario = null,
    KardexEntry? Origen = null,
    bool EsAnulacion = false,
    int? LocationId = null);

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
/// del mismo producto y ámbito se rechaza con <c>Inventory.Costing.RetroactiveNotAllowed</c> nombrando el posterior —siempre
/// hasta I5, sin mirar <c>Costeo.RetroactivosPermitidos</c>—, salvo las dos clases exentas de I1 (<see cref="EsExentoAsync"/>):
/// el saldo inicial de una bodega <c>NotActivated</c> (y su anulación) y los ajustes de un conteo (<c>CountAdjustmentOf</c>).
/// Para ellas el ámbito se recalcula con <see cref="Retroactivo.Insertar"/> desde el estado a la fecha del documento y la
/// historia posterior (índice <c>(ProductId, CostScopeWarehouseId, OperationDate, Id)</c>): las líneas <c>Retroactive</c> van
/// bajo este documento, fechadas en la salida afectada y en su bodega, y vuelven en <see cref="RegistroHecho.AjustesRetroactivos"/>
/// agrupadas por documento afectado; un saldo intermedio bajo cero con el negativo prohibido es
/// <c>Inventory.Stock.Insufficient</c>.
/// </para>
/// </summary>
public sealed class RegistroDeKardex(IApplicationDbContext db, ILectorDeParametros parametros, IDateTimeService reloj)
{
    /// <summary>Lo que hay que bloquear y el valor al costo estimado (para la política de aprobación), sin escribir nada.</summary>
    public async Task<Result<PreparacionDelRegistro>> PrepararAsync(InventoryDocument documento, IReadOnlyList<MovimientoDeKardex> movimientos, CancellationToken ct)
    {
        var leidos = await LeerParametrosAsync(documento.OperationDate, movimientos.Select(m => m.WarehouseId).Distinct().ToList(), ct);
        if (leidos.IsFailure) return Result.Failure<PreparacionDelRegistro>(leidos.Error);
        var p = leidos.Value;
        var ubicaciones = await UbicacionesPorDefectoAsync(movimientos, ct);
        if (ubicaciones.IsFailure) return Result.Failure<PreparacionDelRegistro>(ubicaciones.Error);

        var productos = movimientos.Select(m => m.Linea.ProductId).Distinct().ToList();
        var estados = await db.CostStates.AsNoTracking().Where(c => productos.Contains(c.ProductId)).ToListAsync(ct);

        var valor = 0m;
        foreach (var m in movimientos)
        {
            var estado = estados.FirstOrDefault(e => e.ProductId == m.Linea.ProductId && e.ScopeWarehouseId == p.AmbitoDe(m.WarehouseId));
            var vigente = estado is null ? 0m : (estado.Quantity > 0m ? estado.AverageCost : estado.LastUnitCost);
            valor += Math.Abs(m.QuantityBase) * (m.CostoUnitario ?? vigente);
        }

        var cerrojo = new PedidoDeCerrojo
        {
            Bodegas = movimientos.Select(m => m.WarehouseId).Distinct().ToList(),
            EstadosDeCosto = movimientos.Select(m => new ClaveDeEstadoDeCosto(m.Linea.ProductId, p.AmbitoDe(m.WarehouseId), p.Metodo)).Distinct().ToList(),
            Existencias = movimientos.Select(m => new ClaveDeExistencia(m.Linea.ProductId, m.WarehouseId)).Distinct().ToList(),
            Detalles = movimientos.Select(m => new ClaveDeDetalleDeExistencia(m.Linea.ProductId, m.WarehouseId, UbicacionDe(m, ubicaciones.Value), null))
                .Distinct().ToList(),
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

        // Las filas bloqueadas, seguidas (el cerrojo ya las creó; si falta alguna, se crea al escribir).
        var productos = movimientos.Select(m => m.Linea.ProductId).Distinct().ToList();
        var costos = (await db.CostStates.Where(c => productos.Contains(c.ProductId)).ToListAsync(ct))
            .ToDictionary(c => (c.ProductId, c.ScopeWarehouseId));
        var existencias = (await db.StockBalances.Where(s => productos.Contains(s.ProductId) && bodegas.Contains(s.WarehouseId)).ToListAsync(ct))
            .ToDictionary(s => (s.ProductId, s.WarehouseId));
        var detalles = (await db.StockDetails.Where(s => productos.Contains(s.ProductId) && bodegas.Contains(s.WarehouseId) && s.LotId == null).ToListAsync(ct))
            .ToDictionary(s => (s.ProductId, s.WarehouseId, s.LocationId));

        // --------------------------------------------------------------------- retroactivo (US3, T285) --
        var retro = await AmbitosRetroactivosAsync(documento, movimientos, p, ct);
        if (retro.IsFailure) return Result.Failure<RegistroHecho>(retro.Error);
        var ambitosRetroactivos = retro.Value;
        var retroactivos = new List<(MovimientoDeKardex Mov, int Ubicacion, int Ambito)>();

        // ------------------------------------------------------------------ primera pasada: en memoria --
        var estados = costos.ToDictionary(c => c.Key, c => new EstadoDeCosto(c.Value.Quantity, c.Value.Value, c.Value.AverageCost, c.Value.LastUnitCost));
        var fisicos = existencias.ToDictionary(e => e.Key, e => (Fisico: e.Value.Physical, Reservado: e.Value.Reserved));
        var porUbicacion = detalles.ToDictionary(d => d.Key, d => d.Value.Quantity);
        var plan = new List<(MovimientoDeKardex Mov, int Ubicacion, int Ambito, ResultadoDeCosteo Costo)>(movimientos.Count);
        var faltantes = new List<(MovimientoDeKardex Mov, int Ubicacion, decimal Disponible, bool PorUbicacion)>();

        foreach (var m in movimientos)
        {
            var producto = m.Linea.ProductId;
            var ubicacion = UbicacionDe(m, porDefecto.Value);
            var ambito = p.AmbitoDe(m.WarehouseId);
            var negativo = p.NegativoPermitido(m.WarehouseId);

            var (fisico, reservado) = fisicos.GetValueOrDefault((producto, m.WarehouseId));
            var enUbicacion = porUbicacion.GetValueOrDefault((producto, m.WarehouseId, ubicacion));
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
                porUbicacion[(producto, m.WarehouseId, ubicacion)] = enUbicacion + m.QuantityBase;
                retroactivos.Add((m, ubicacion, ambito));
                continue;
            }

            var estado = estados.GetValueOrDefault((producto, ambito)) ?? EstadoDeCosto.Vacio;
            var origen = m.Origen is null ? null : new ReferenciaDeKardex(m.Origen.Id == 0 ? null : m.Origen.Id, null);
            var costo = MotorDeCosteo.Aplicar(estado,
                new MovimientoDeCosto(m.QuantityBase, m.Valoracion, m.CostoUnitario, origen, m.EsAnulacion),
                new ParametrosDeCosteo(p.Metodo, p.Montos, negativo));
            if (!costo.Admitido)
            {
                faltantes.Add((m, ubicacion, costo.Rechazo!.Disponible, false));
                continue;
            }

            estados[(producto, ambito)] = costo.Estado;
            fisicos[(producto, m.WarehouseId)] = (fisico + m.QuantityBase, reservado);
            porUbicacion[(producto, m.WarehouseId, ubicacion)] = enUbicacion + m.QuantityBase;
            plan.Add((m, ubicacion, ambito, costo));
        }

        var recalculados = new List<(MovimientoDeKardex Primero, int Ambito, ResultadoRetroactivo Resultado, IReadOnlyDictionary<long, KardexEntry> Afectadas)>();
        foreach (var grupo in retroactivos.GroupBy(r => (r.Mov.Linea.ProductId, r.Ambito)))
        {
            var deLaClave = grupo.ToList();
            var (pedido, afectadas) = await PedidoRetroactivoAsync(documento, grupo.Key.ProductId, grupo.Key.Ambito, deLaClave.Select(r => r.Mov).ToList(),
                new ParametrosDeCosteo(p.Metodo, p.Montos, deLaClave.All(r => p.NegativoPermitido(r.Mov.WarehouseId))), ct);
            var resultado = Retroactivo.Insertar(pedido);
            if (!resultado.Admitido)
            {
                faltantes.Add((deLaClave[0].Mov, deLaClave[0].Ubicacion, resultado.Rechazo!.Disponible, false));
                continue;
            }
            estados[grupo.Key] = resultado.EstadoFinal;
            for (var i = 0; i < deLaClave.Count; i++) plan.Add((deLaClave[i].Mov, deLaClave[i].Ubicacion, grupo.Key.Ambito, resultado.Nuevos[i]));
            recalculados.Add((deLaClave[0].Mov, grupo.Key.Ambito, resultado, afectadas));
        }

        if (faltantes.Count > 0)
            return Result.Failure<RegistroHecho>(await ErrorDeExistenciaAsync(faltantes, sugerencia, ct));

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
                    ProductId = m.Linea.ProductId,
                    WarehouseId = m.WarehouseId,
                    LocationId = ubicacion,
                    LotId = m.Linea.LotId,
                    SerialId = m.Linea.SerialId,
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

                var clave = (m.Linea.ProductId, m.WarehouseId);
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
                        ProductId = primero.Linea.ProductId,
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

        // Proyecciones: el estado final de cada ámbito, la existencia de cada bodega y ubicación tocada.
        foreach (var (producto, ambito) in plan.Select(x => (x.Mov.Linea.ProductId, x.Ambito)).Distinct())
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
        foreach (var (producto, bodega) in plan.Select(x => (x.Mov.Linea.ProductId, x.Mov.WarehouseId)).Distinct())
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
        foreach (var (producto, bodega, ubicacion) in plan.Select(x => (x.Mov.Linea.ProductId, x.Mov.WarehouseId, x.Ubicacion)).Distinct())
        {
            if (!detalles.TryGetValue((producto, bodega, ubicacion), out var fila))
            {
                fila = new StockDetail { ProductId = producto, WarehouseId = bodega, LocationId = ubicacion };
                db.StockDetails.Add(fila);
                detalles[(producto, bodega, ubicacion)] = fila;
            }
            fila.Quantity = porUbicacion[(producto, bodega, ubicacion)];
        }

        // La línea del documento: su costo (el valor de lo que movió) y la ubicación por defecto si no traía.
        foreach (var porLinea in escritos.GroupBy(e => e.Movimiento.Linea))
        {
            var primero = porLinea.First();
            var linea = porLinea.Key;
            linea.LocationId ??= primero.LocationId;
            var principales = primero.Lineas.Where(l => l.Kind != KardexEntryKind.CostAdjustment).ToList();
            var total = Math.Abs(principales.Sum(l => l.TotalCost));
            var cantidad = Math.Abs(principales.Sum(l => l.QuantityBase));
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
            var estado = new EstadoDeCosto(fila.Quantity, fila.Value, fila.AverageCost, fila.LastUnitCost);
            var resultado = MotorDeCosteo.DiferenciaDePrecio(estado,
                new PedidoDeDiferenciaDePrecio(ReferenciaDeKardex.A(entrada.Id), pedida.CantidadFacturada, pedida.Diferencia), p.Montos);

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

    // ------------------------------------------------------------------------------------ retroactivo --

    /// <summary>
    /// Los ámbitos (producto, ámbito de costo) donde el documento dejaría un movimiento anterior a otro ya registrado. Si hay
    /// alguno y la clase no está exenta, <c>Inventory.Costing.RetroactiveNotAllowed</c> nombrando el primer movimiento posterior.
    /// </summary>
    private async Task<Result<HashSet<(int ProductId, int Ambito)>>> AmbitosRetroactivosAsync(
        InventoryDocument documento, IReadOnlyList<MovimientoDeKardex> movimientos, ParametrosDelKardex p, CancellationToken ct)
    {
        var claves = movimientos.Select(m => (m.Linea.ProductId, Ambito: p.AmbitoDe(m.WarehouseId))).ToHashSet();
        var productos = claves.Select(c => c.ProductId).Distinct().ToList();
        var fecha = documento.OperationDate;
        var posteriores = (await db.KardexEntries.AsNoTracking()
                .Where(k => productos.Contains(k.ProductId) && k.OperationDate > fecha)
                .GroupBy(k => new { k.ProductId, k.CostScopeWarehouseId })
                .Select(g => new { g.Key.ProductId, g.Key.CostScopeWarehouseId, Fecha = g.Min(k => k.OperationDate) })
                .ToListAsync(ct))
            .Where(x => claves.Contains((x.ProductId, x.CostScopeWarehouseId)))
            .ToList();
        if (posteriores.Count == 0) return Result.Success(new HashSet<(int, int)>());

        if (await EsExentoAsync(documento, ct))
            return Result.Success(posteriores.Select(x => (x.ProductId, x.CostScopeWarehouseId)).ToHashSet());

        var primero = movimientos.First(m => posteriores.Any(x => x.ProductId == m.Linea.ProductId && x.CostScopeWarehouseId == p.AmbitoDe(m.WarehouseId)));
        var ambito = p.AmbitoDe(primero.WarehouseId);
        var fechaPosterior = posteriores.First(x => x.ProductId == primero.Linea.ProductId && x.CostScopeWarehouseId == ambito).Fecha;
        var posterior = await db.KardexEntries.AsNoTracking()
            .Where(k => k.ProductId == primero.Linea.ProductId && k.CostScopeWarehouseId == ambito && k.OperationDate == fechaPosterior)
            .OrderBy(k => k.Id)
            .Join(db.InventoryDocuments, k => k.DocumentId, d => d.Id, (k, d) => new { d.PublicId, d.Prefix, d.Number })
            .FirstAsync(ct);
        var codigo = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == primero.Linea.ProductId).Select(x => x.Code).FirstAsync(ct);
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
                movimiento = clase == DocumentClass.SupplierReturn
                    ? new MovimientoDeCosto(k.QuantityBase, ValoracionDelMovimiento.DevolucionDeEntrada, k.UnitCost)
                    : new MovimientoDeCosto(k.QuantityBase, ValoracionDelMovimiento.AlCostoVigente);
            else if (principales.FirstOrDefault(s => s.Kind == KardexEntryKind.Exit && s.DocumentId == k.DocumentId && s.DocumentLineId == k.DocumentLineId && s.Id < k.Id) is { } salida)
                movimiento = new MovimientoDeCosto(k.QuantityBase, ValoracionDelMovimiento.AlCostoDeOrigen, k.UnitCost, ReferenciaDeKardex.A(salida.Id));
            else
                movimiento = new MovimientoDeCosto(k.QuantityBase, ValoracionDelMovimiento.AlCostoIndicado, k.UnitCost);
            var sale = k.Kind == KardexEntryKind.Exit && clase is not (DocumentClass.TransferDispatch or DocumentClass.LocationMove);
            return new MovimientoRegistrado(k.Id, k.DocumentId, k.OperationDate, movimiento, valor[k.Id], k.UnitCost, sale);
        }).ToList();

        var pedido = new PedidoRetroactivo(inicial, historia,
            nuevos.Select(m => new MovimientoRetroactivo(fecha, documento.Id,
                new MovimientoDeCosto(m.QuantityBase, m.Valoracion, m.CostoUnitario,
                    m.Origen is null ? null : new ReferenciaDeKardex(m.Origen.Id == 0 ? null : m.Origen.Id, null), m.EsAnulacion))).ToList(),
            parametrosDeCosteo);
        return (pedido, principales.ToDictionary(k => k.Id));
    }

    // ------------------------------------------------------------------------------------------ apoyo --

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

    private async Task<Error> ErrorDeExistenciaAsync(
        List<(MovimientoDeKardex Mov, int Ubicacion, decimal Disponible, bool PorUbicacion)> faltantes, string? sugerencia, CancellationToken ct)
    {
        var productoIds = faltantes.Select(f => f.Mov.Linea.ProductId).Distinct().ToList();
        var bodegaIds = faltantes.Select(f => f.Mov.WarehouseId).Distinct().ToList();
        var ubicacionIds = faltantes.Select(f => f.Ubicacion).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(x => productoIds.Contains(x.Id)).Select(x => new { x.Id, x.PublicId, x.Code }).ToDictionaryAsync(x => x.Id, ct);
        var bodegas = await db.Warehouses.AsNoTracking().Where(x => bodegaIds.Contains(x.Id)).Select(x => new { x.Id, x.PublicId }).ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);
        var ubicaciones = await db.WarehouseLocations.AsNoTracking().Where(x => ubicacionIds.Contains(x.Id)).Select(x => new { x.Id, x.PublicId }).ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);

        var lineas = faltantes.Select(f =>
        {
            var producto = productos.GetValueOrDefault(f.Mov.Linea.ProductId);
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
