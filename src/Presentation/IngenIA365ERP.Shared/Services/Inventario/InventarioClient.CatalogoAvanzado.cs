using IngenIA365ERP.Shared.Services.Http;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// El catálogo avanzado de Inventario (feature 012, I6, T936; US15; contracts/api.md §3, §17.1): atributos de variante, variantes de una
/// plantilla, componentes de un combo o kit, y los lotes (en orden FEFO, con el sugerido) y las series de un producto. Toda escritura lleva
/// la <c>Idempotency-Key</c> de su operación de pantalla; las lecturas no; la cabecera <c>Authorization</c> la pone el handler de la sesión.
/// (nuevo)
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDeAtributosDeVariante = Base + "/variant-attributes";
    public const string RutaDeLotes = Base + "/lots";
    public const string RutaDeSeries = Base + "/serials";

    // --------------------------------------------------------------------------------------------- atributos --

    public Task<ResultadoDeInventario<IReadOnlyList<AtributoDeVarianteDto>>> ListarAtributosDeVarianteAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<AtributoDeVarianteDto>>(HttpMethod.Get, ConInactivos(RutaDeAtributosDeVariante, incluirInactivos), null, null, ct);

    /// <summary>Crea (<paramref name="id"/> nulo) o cambia un atributo con sus valores; un código usado por variantes no cambia.</summary>
    public Task<ResultadoDeInventario<AtributoDeVarianteDto>> GuardarAtributoDeVarianteAsync(Guid? id, AtributoDeVarianteRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        id is { } i
            ? EnviarAsync<AtributoDeVarianteDto>(HttpMethod.Put, $"{RutaDeAtributosDeVariante}/{i}", request, clave, ct)
            : EnviarAsync<AtributoDeVarianteDto>(HttpMethod.Post, RutaDeAtributosDeVariante, request, clave, ct);

    // --------------------------------------------------------------------------------------------- variantes --

    public Task<ResultadoDeInventario<IReadOnlyList<VarianteDto>>> ListarVariantesAsync(Guid plantilla, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<VarianteDto>>(HttpMethod.Get, $"{RutaDeProductos}/{plantilla}/variants", null, null, ct);

    /// <summary>Genera las combinaciones que faltan de la plantilla; las que ya existían vuelven en <c>AlreadyExisting</c>.</summary>
    public Task<ResultadoDeInventario<VariantesGeneradasDto>> GenerarVariantesAsync(Guid plantilla, GenerarVariantesRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<VariantesGeneradasDto>(HttpMethod.Post, $"{RutaDeProductos}/{plantilla}/variants", request, clave, ct);

    // ------------------------------------------------------------------------------------------- componentes --

    public Task<ResultadoDeInventario<ComponentesDelProductoDto>> ObtenerComponentesAsync(Guid producto, CancellationToken ct = default) =>
        EnviarAsync<ComponentesDelProductoDto>(HttpMethod.Get, $"{RutaDeProductos}/{producto}/components", null, null, ct);

    /// <summary>Reemplaza los componentes; rige para lo que se venda o ensamble después. Los errores vienen todos en <c>data.errors[]</c>.</summary>
    public Task<ResultadoDeInventario<ComponentesDelProductoDto>> FijarComponentesAsync(Guid producto, ComponentesRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<ComponentesDelProductoDto>(HttpMethod.Put, $"{RutaDeProductos}/{producto}/components", request, clave, ct);

    // -------------------------------------------------------------------------------------- lotes y series --

    /// <summary>Los lotes con existencia del producto en la bodega (o en las del alcance), en orden FEFO; los vencidos sólo si se piden.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<LoteDto>>> ListarLotesAsync(Guid producto, Guid? bodega = null, bool incluirVencidos = false,
        CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<LoteDto>>(HttpMethod.Get, ConQuery(RutaDeLotes, Query(
            ("productPublicId", producto.ToString()), ("warehousePublicId", bodega?.ToString()), ("includeExpired", incluirVencidos ? "true" : null))),
            null, null, ct);

    /// <summary>Las series del producto: en la bodega, o las del alcance y las que ya salieron; <paramref name="enExistencia"/> filtra.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<SerieDto>>> ListarSeriesAsync(Guid producto, Guid? bodega = null, bool? enExistencia = null,
        CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<SerieDto>>(HttpMethod.Get, ConQuery(RutaDeSeries, Query(
            ("productPublicId", producto.ToString()), ("warehousePublicId", bodega?.ToString()),
            ("inStock", enExistencia is { } e ? (e ? "true" : "false") : null))), null, null, ct);
}
