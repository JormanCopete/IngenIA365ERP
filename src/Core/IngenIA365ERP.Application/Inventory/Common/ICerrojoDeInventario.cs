using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// El cerrojo pesimista de la confirmación (feature 012, T15, T138; decisiones-transversales §1.3 paso 6; data-model §0
/// y §5.5). Bloquea, dentro de la transacción en curso y con la conexión del propio <c>ApplicationDbContext</c>, en este
/// orden canónico y ordenando por <c>Id</c> en cada tabla:
/// <list type="number">
/// <item><c>INV_Setup</c>, compartido (exclusivo sólo al cerrar o reabrir un período);</item>
/// <item><c>INV_Warehouses</c>, compartido, por Id;</item>
/// <item><c>INV_Documents</c> de los orígenes, exclusivo (sin modificarlas: dos derivados que consumen el mismo origen
/// no se pasan de lo pendiente);</item>
/// <item><c>INV_CostStates</c>, <c>INV_StockBalances</c> e <c>INV_StockDetails</c>, exclusivos; antes de bloquearlas crea
/// las filas de proyección que falten;</item>
/// <item>y, al final, la fila de numeración (<see cref="BloquearNumeracionAsync"/>, que llama <c>Numerador</c>).</item>
/// </list>
/// Todos los que confirman bloquean en el mismo orden, así que no hay abrazo mortal; <c>RowVersion</c> queda como
/// segunda defensa. Se llama fuera de toda validación previa (T30) y sólo dentro de <c>TransaccionExplicita</c>. La
/// implementación vive en <c>Persistence/Inventory/CerrojoDeInventario</c> con el SQL de cada motor.
/// </summary>
public interface ICerrojoDeInventario
{
    /// <summary>Pasos 1 a 4 del orden canónico. Lo que el pedido no nombra no se bloquea.</summary>
    Task BloquearAsync(PedidoDeCerrojo pedido, CancellationToken ct = default);

    /// <summary>Paso 5: la fila de <c>INV_DocumentSequences</c> que va a numerar, en exclusivo y al final.</summary>
    Task BloquearNumeracionAsync(int documentSequenceId, CancellationToken ct = default);
}

/// <summary>Cómo se toma <c>INV_Setup</c>: compartido al confirmar, exclusivo al cerrar o reabrir un período. (nuevo)</summary>
public enum ModoDeBloqueoDelSetup
{
    Compartido = 1,
    Exclusivo = 2,
}

/// <summary>Una fila de <c>INV_CostStates</c>: producto y ámbito (0 = cooperativa) con el método vigente. (nuevo)</summary>
public sealed record ClaveDeEstadoDeCosto(int ProductId, int ScopeWarehouseId, CostMethod Method);

/// <summary>Una fila de <c>INV_StockBalances</c>: producto y bodega. (nuevo)</summary>
public sealed record ClaveDeExistencia(int ProductId, int WarehouseId);

/// <summary>Una fila de <c>INV_StockDetails</c>: producto, bodega, ubicación y lote (nulo sin lote). (nuevo)</summary>
public sealed record ClaveDeDetalleDeExistencia(int ProductId, int WarehouseId, int LocationId, int? LotId);

/// <summary>
/// Lo que una confirmación necesita bloquear (T15). Las colecciones pueden venir en cualquier orden y con repetidos: el
/// cerrojo las ordena y las deduplica. (nuevo)
/// </summary>
public sealed record PedidoDeCerrojo
{
    public ModoDeBloqueoDelSetup Setup { get; init; } = ModoDeBloqueoDelSetup.Compartido;

    public IReadOnlyCollection<int> Bodegas { get; init; } = [];

    /// <summary><c>INV_Documents</c> de los que nace el documento (data-model §5.5).</summary>
    public IReadOnlyCollection<int> DocumentosDeOrigen { get; init; } = [];

    public IReadOnlyCollection<ClaveDeEstadoDeCosto> EstadosDeCosto { get; init; } = [];

    public IReadOnlyCollection<ClaveDeExistencia> Existencias { get; init; } = [];

    public IReadOnlyCollection<ClaveDeDetalleDeExistencia> Detalles { get; init; } = [];
}
