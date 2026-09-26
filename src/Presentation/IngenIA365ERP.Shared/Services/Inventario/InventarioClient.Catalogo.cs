using IngenIA365ERP.Shared.Services.Http;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// El catálogo de Inventario (feature 012, T232; contracts/api.md §3): unidades, categorías, marcas, grupos contables, canales,
/// causas de ajuste, productos con sus unidades alternas, códigos de barras e impuestos, y la búsqueda del lector (que recibe el
/// <see cref="CancellationToken"/> de la tecla para cancelar la anterior). Toda escritura lleva la <c>Idempotency-Key</c> de su
/// operación de pantalla; la cabecera <c>Authorization</c> la pone el handler de la sesión. Las plantillas 2 a 6 se descargan
/// e importan con los métodos genéricos de <c>InventarioClient.Plantillas</c> sobre las rutas de aquí. (nuevo)
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDeUnidades = Base + "/units";
    public const string RutaDeCategorias = Base + "/product-categories";
    public const string RutaDeMarcas = Base + "/brands";
    public const string RutaDeGruposContables = Base + "/accounting-groups";
    public const string RutaDeProductos = Base + "/products";
    public const string RutaDeCausas = Base + "/adjustment-causes";
    public const string RutaDeCanales = Base + "/sales-channels";

    private static string ConInactivos(string ruta, bool incluir) => incluir ? $"{ruta}?includeInactive=true" : ruta;

    // ----------------------------------------------------------------------------------------- catálogos simples --

    public Task<ResultadoDeInventario<IReadOnlyList<UnidadDeMedidaDto>>> ListarUnidadesAsync(bool incluirInactivas = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<UnidadDeMedidaDto>>(HttpMethod.Get, ConInactivos(RutaDeUnidades, incluirInactivas), null, null, ct);

    public Task<ResultadoDeInventario<UnidadDeMedidaDto>> GuardarUnidadAsync(Guid? id, UnidadRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } i
            ? EnviarAsync<UnidadDeMedidaDto>(HttpMethod.Put, $"{RutaDeUnidades}/{i}", request, clave, ct)
            : EnviarAsync<UnidadDeMedidaDto>(HttpMethod.Post, RutaDeUnidades, request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CategoriaDto>>> ListarCategoriasAsync(bool incluirInactivas = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CategoriaDto>>(HttpMethod.Get, ConInactivos(RutaDeCategorias, incluirInactivas), null, null, ct);

    public Task<ResultadoDeInventario<CategoriaDto>> GuardarCategoriaAsync(Guid? id, CategoriaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } i
            ? EnviarAsync<CategoriaDto>(HttpMethod.Put, $"{RutaDeCategorias}/{i}", request, clave, ct)
            : EnviarAsync<CategoriaDto>(HttpMethod.Post, RutaDeCategorias, request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<MarcaDto>>> ListarMarcasAsync(bool incluirInactivas = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<MarcaDto>>(HttpMethod.Get, ConInactivos(RutaDeMarcas, incluirInactivas), null, null, ct);

    public Task<ResultadoDeInventario<MarcaDto>> GuardarMarcaAsync(Guid? id, CodigoYNombreRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } i
            ? EnviarAsync<MarcaDto>(HttpMethod.Put, $"{RutaDeMarcas}/{i}", request, clave, ct)
            : EnviarAsync<MarcaDto>(HttpMethod.Post, RutaDeMarcas, request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<GrupoContableDto>>> ListarGruposContablesAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<GrupoContableDto>>(HttpMethod.Get, ConInactivos(RutaDeGruposContables, incluirInactivos), null, null, ct);

    public Task<ResultadoDeInventario<GrupoContableDto>> GuardarGrupoContableAsync(Guid? id, GrupoContableRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } i
            ? EnviarAsync<GrupoContableDto>(HttpMethod.Put, $"{RutaDeGruposContables}/{i}", request, clave, ct)
            : EnviarAsync<GrupoContableDto>(HttpMethod.Post, RutaDeGruposContables, request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CanalDeVentaDto>>> ListarCanalesAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CanalDeVentaDto>>(HttpMethod.Get, ConInactivos(RutaDeCanales, incluirInactivos), null, null, ct);

    public Task<ResultadoDeInventario<CanalDeVentaDto>> GuardarCanalAsync(Guid? id, CodigoYNombreRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } i
            ? EnviarAsync<CanalDeVentaDto>(HttpMethod.Put, $"{RutaDeCanales}/{i}", request, clave, ct)
            : EnviarAsync<CanalDeVentaDto>(HttpMethod.Post, RutaDeCanales, request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<CausaDeAjusteDto>>> ListarCausasAsync(bool incluirInactivas = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CausaDeAjusteDto>>(HttpMethod.Get, ConInactivos(RutaDeCausas, incluirInactivas), null, null, ct);

    public Task<ResultadoDeInventario<CausaDeAjusteDto>> GuardarCausaAsync(Guid? id, CausaDeAjusteRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } i
            ? EnviarAsync<CausaDeAjusteDto>(HttpMethod.Put, $"{RutaDeCausas}/{i}", request, clave, ct)
            : EnviarAsync<CausaDeAjusteDto>(HttpMethod.Post, RutaDeCausas, request, clave, ct);

    /// <summary>Inactivar o reactivar una fila de cualquier catálogo del módulo (<c>{ruta}/{id}/deactivate|reactivate</c>) con motivo.</summary>
    public Task<ResultadoDeInventario<object>> CambiarActivoAsync(string rutaBase, Guid id, bool activar, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<object>(HttpMethod.Post, $"{rutaBase.TrimEnd('/')}/{id}/{(activar ? "reactivate" : "deactivate")}", new MotivoDeInventarioRequest(motivo), clave, ct);

    // ------------------------------------------------------------------------------------------------- productos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ProductoDeListaDto>>> ListarProductosAsync(string? buscar = null, Guid? categoria = null,
        Guid? marca = null, Guid? grupo = null, int? clase = null, int? estado = null, int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ProductoDeListaDto>>(HttpMethod.Get, ConQuery(RutaDeProductos, Query(
            ("search", buscar), ("categoryPublicId", categoria?.ToString()), ("brandPublicId", marca?.ToString()),
            ("accountingGroupPublicId", grupo?.ToString()), ("kind", clase?.ToString()), ("status", estado?.ToString()),
            ("page", pagina.ToString()), ("pageSize", tamano.ToString()))), null, null, ct);

    public Task<ResultadoDeInventario<ProductoDto>> ObtenerProductoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<ProductoDto>(HttpMethod.Get, $"{RutaDeProductos}/{id}", null, null, ct);

    /// <summary>
    /// La búsqueda del lector y del teclado (§3.5): primero la lectura exacta por código de barras o código (<c>exact</c>, con el
    /// empaque), luego los términos. <paramref name="ct"/> es el de la tecla: la pantalla cancela la búsqueda anterior.
    /// </summary>
    public Task<ResultadoDeInventario<BusquedaDeProductosDto>> BuscarProductosAsync(string q, Guid? bodega = null, bool incluirInactivos = false,
        int? tomar = null, IReadOnlyList<int>? clases = null, CancellationToken ct = default) =>
        EnviarAsync<BusquedaDeProductosDto>(HttpMethod.Get, ConQuery($"{RutaDeProductos}/search", Query(
            ("q", q), ("warehousePublicId", bodega?.ToString()), ("includeInactive", incluirInactivos ? "true" : null),
            ("take", tomar?.ToString()), ("kinds", clases is { Count: > 0 } ? string.Join(",", clases) : null))), null, null, ct);

    public Task<ResultadoDeInventario<ProductoDto>> CrearProductoAsync(CrearProductoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ProductoDto>(HttpMethod.Post, RutaDeProductos, request, clave, ct);

    public Task<ResultadoDeInventario<ProductoDto>> EditarProductoAsync(Guid id, EditarProductoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ProductoDto>(HttpMethod.Put, $"{RutaDeProductos}/{id}", request, clave, ct);

    /// <summary>Activo, inactivo o bloqueado con motivo; el mismo estado responde <c>Inventory.Product.StatusUnchanged</c>.</summary>
    public Task<ResultadoDeInventario<ProductoDto>> CambiarEstadoDeProductoAsync(Guid id, EstadoDeProductoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ProductoDto>(HttpMethod.Post, $"{RutaDeProductos}/{id}/status", request, clave, ct);

    /// <summary>Sólo sin historia; si no, <c>Inventory.Product.HasHistory</c> con <c>data.alternatives</c>.</summary>
    public Task<ResultadoDeInventario<object>> BorrarProductoAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<object>(HttpMethod.Delete, $"{RutaDeProductos}/{id}", null, clave, ct);

    public Task<ResultadoDeInventario<UnidadAlternaDto>> AgregarUnidadAlternaAsync(Guid producto, UnidadAlternaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<UnidadAlternaDto>(HttpMethod.Post, $"{RutaDeProductos}/{producto}/units", request, clave, ct);

    public Task<ResultadoDeInventario<UnidadAlternaDto>> EditarUnidadAlternaAsync(Guid producto, Guid unidad, UnidadAlternaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<UnidadAlternaDto>(HttpMethod.Put, $"{RutaDeProductos}/{producto}/units/{unidad}", request, clave, ct);

    public Task<ResultadoDeInventario<object>> RetirarUnidadAlternaAsync(Guid producto, Guid unidad, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<object>(HttpMethod.Delete, $"{RutaDeProductos}/{producto}/units/{unidad}", null, clave, ct);

    public Task<ResultadoDeInventario<CodigoDeBarrasDto>> AgregarCodigoDeBarrasAsync(Guid producto, CodigoDeBarrasRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<CodigoDeBarrasDto>(HttpMethod.Post, $"{RutaDeProductos}/{producto}/barcodes", request, clave, ct);

    public Task<ResultadoDeInventario<object>> RetirarCodigoDeBarrasAsync(Guid producto, Guid codigo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<object>(HttpMethod.Delete, $"{RutaDeProductos}/{producto}/barcodes/{codigo}", null, clave, ct);

    public Task<ResultadoDeInventario<ImpuestosDelProductoDto>> ImpuestosDelProductoAsync(Guid producto, CancellationToken ct = default) =>
        EnviarAsync<ImpuestosDelProductoDto>(HttpMethod.Get, $"{RutaDeProductos}/{producto}/taxes", null, null, ct);

    /// <summary>Reemplaza el conjunto de impuestos; rige para lo que se confirme después.</summary>
    public Task<ResultadoDeInventario<ImpuestosDelProductoDto>> FijarImpuestosDelProductoAsync(Guid producto, ImpuestosDelProductoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ImpuestosDelProductoDto>(HttpMethod.Put, $"{RutaDeProductos}/{producto}/taxes", request, clave, ct);
}
