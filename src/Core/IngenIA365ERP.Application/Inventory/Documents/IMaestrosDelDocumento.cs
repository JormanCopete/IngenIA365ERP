using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>Una bodega tal como la usa un documento. <see cref="Activa"/> = activada (FR-090); <see cref="Inactiva"/> = dada de baja para lo nuevo. (nuevo)</summary>
public sealed record BodegaDelDocumento(
    int Id,
    Guid PublicId,
    string Code,
    string Name,
    int BranchId,
    bool EsTransito,
    bool Activa,
    bool Inactiva,
    DateOnly? CutoffDate = null);

/// <summary>Un producto tal como lo usa una línea. (nuevo)</summary>
public sealed record ProductoDelDocumento(int Id, Guid PublicId, string Code, string Name, bool Inventariable, ProductStatus Status);

/// <summary>
/// La unidad de una línea para un producto: la base o una alterna, con su factor a la base, los decimales que admite y
/// los de la unidad base (FR-017). (nuevo)
/// </summary>
public sealed record UnidadDelDocumento(int Id, Guid PublicId, string Code, decimal Factor, int DecimalesPermitidos, int DecimalesDeLaBase, string? BaseUnitCode = null);

/// <summary>Una ubicación de bodega. (nuevo)</summary>
public sealed record UbicacionDelDocumento(int Id, Guid PublicId, string Code, string Name, int WarehouseId);

/// <summary>Una referencia de catálogo (causa de ajuste, canal de venta). (nuevo)</summary>
public sealed record ReferenciaDelCatalogo(int Id, Guid PublicId, string Code, string Name);

/// <summary>
/// El corte del módulo (<c>INV_Setup</c>, US3): antes de <see cref="StartDate"/> no hay documentos
/// (<c>Inventory.Document.DateBeforeCutoff</c>); en o antes de <see cref="LastClosedDate"/> el período está cerrado
/// (<c>Inventory.Period.Closed</c>). Nulos = sin restricción todavía. (nuevo)
/// </summary>
public sealed record CorteDeInventario(DateOnly? StartDate, DateOnly? LastClosedDate)
{
    public static CorteDeInventario SinCorte { get; } = new(null, null);
}

/// <summary>
/// Los maestros que el documento genérico referencia por columna y cuyas entidades crean las historias US1–US3 (feature
/// 012, T17; data-model §5): bodegas, ubicaciones, productos, unidades, causas, canales y el corte de <c>INV_Setup</c>.
/// El ciclo común (fase 3) los lee sólo por aquí, así compila y se prueba antes de que existan; US1 registra la
/// implementación sobre sus tablas y US3 el corte. Todo por <c>PublicId</c> hacia afuera y por <c>Id</c> hacia adentro.
/// Nunca filtra por alcance: eso lo hace quien pregunta con <c>IAlcanceDeInventario</c>. (nuevo)
/// </summary>
public interface IMaestrosDelDocumento
{
    Task<IReadOnlyList<BodegaDelDocumento>> BodegasAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct);

    Task<IReadOnlyList<BodegaDelDocumento>> BodegasPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task<IReadOnlyList<ProductoDelDocumento>> ProductosAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct);

    Task<IReadOnlyList<ProductoDelDocumento>> ProductosPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    /// <summary>La unidad <paramref name="unitPublicId"/> para el producto, si es su base o una de sus alternas; nula si no.</summary>
    Task<UnidadDelDocumento?> UnidadAsync(int productId, Guid unitPublicId, CancellationToken ct);

    Task<IReadOnlyList<UnidadDelDocumento>> UnidadesPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task<IReadOnlyList<UbicacionDelDocumento>> UbicacionesAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct);

    Task<IReadOnlyList<UbicacionDelDocumento>> UbicacionesPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task<ReferenciaDelCatalogo?> CausaDeAjusteAsync(Guid publicId, CancellationToken ct);

    Task<IReadOnlyList<ReferenciaDelCatalogo>> CausasDeAjustePorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task<ReferenciaDelCatalogo?> CanalDeVentaAsync(Guid publicId, CancellationToken ct);

    Task<IReadOnlyList<ReferenciaDelCatalogo>> CanalesDeVentaPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    Task<CorteDeInventario> CorteAsync(CancellationToken ct);
}

/// <summary>
/// Mientras US1 no registre los maestros reales: no hay bodegas, productos ni ubicaciones (todo lo referenciado es
/// 404, falla cerrado) y no hay corte. TryAdd en el contenedor, así la implementación real la reemplaza. (nuevo)
/// </summary>
public sealed class MaestrosDelDocumentoSinCatalogo : IMaestrosDelDocumento
{
    public Task<IReadOnlyList<BodegaDelDocumento>> BodegasAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct) => Nada<BodegaDelDocumento>();
    public Task<IReadOnlyList<BodegaDelDocumento>> BodegasPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) => Nada<BodegaDelDocumento>();
    public Task<IReadOnlyList<ProductoDelDocumento>> ProductosAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct) => Nada<ProductoDelDocumento>();
    public Task<IReadOnlyList<ProductoDelDocumento>> ProductosPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) => Nada<ProductoDelDocumento>();
    public Task<UnidadDelDocumento?> UnidadAsync(int productId, Guid unitPublicId, CancellationToken ct) => Task.FromResult<UnidadDelDocumento?>(null);
    public Task<IReadOnlyList<UnidadDelDocumento>> UnidadesPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) => Nada<UnidadDelDocumento>();
    public Task<IReadOnlyList<UbicacionDelDocumento>> UbicacionesAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct) => Nada<UbicacionDelDocumento>();
    public Task<IReadOnlyList<UbicacionDelDocumento>> UbicacionesPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) => Nada<UbicacionDelDocumento>();
    public Task<ReferenciaDelCatalogo?> CausaDeAjusteAsync(Guid publicId, CancellationToken ct) => Task.FromResult<ReferenciaDelCatalogo?>(null);
    public Task<IReadOnlyList<ReferenciaDelCatalogo>> CausasDeAjustePorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) => Nada<ReferenciaDelCatalogo>();
    public Task<ReferenciaDelCatalogo?> CanalDeVentaAsync(Guid publicId, CancellationToken ct) => Task.FromResult<ReferenciaDelCatalogo?>(null);
    public Task<IReadOnlyList<ReferenciaDelCatalogo>> CanalesDeVentaPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) => Nada<ReferenciaDelCatalogo>();
    public Task<CorteDeInventario> CorteAsync(CancellationToken ct) => Task.FromResult(CorteDeInventario.SinCorte);

    private static Task<IReadOnlyList<T>> Nada<T>() => Task.FromResult<IReadOnlyList<T>>([]);
}
