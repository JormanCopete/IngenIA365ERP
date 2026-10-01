using IngenIA365ERP.Shared.Services.Http;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Bodegas, tipos de bodega, ubicaciones y mínimos y máximos (feature 012, T232; contracts/api.md §4). Lo que está fuera del
/// alcance de quien pregunta no aparece en las listas y responde el mismo 404 que lo inexistente. La plantilla 7 se descarga
/// e importa con los métodos genéricos de <c>InventarioClient.Plantillas</c> sobre <see cref="RutaDeBodegas"/>. (nuevo)
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDeBodegas = Base + "/warehouses";
    public const string RutaDeTiposDeBodega = Base + "/warehouse-types";
    public const string RutaDeReorden = Base + "/reorder-policies";

    public Task<ResultadoDeInventario<IReadOnlyList<TipoDeBodegaDto>>> ListarTiposDeBodegaAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<TipoDeBodegaDto>>(HttpMethod.Get, ConInactivos(RutaDeTiposDeBodega, incluirInactivos), null, null, ct);

    public Task<ResultadoDeInventario<TipoDeBodegaDto>> GuardarTipoDeBodegaAsync(Guid? id, TipoDeBodegaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } i
            ? EnviarAsync<TipoDeBodegaDto>(HttpMethod.Put, $"{RutaDeTiposDeBodega}/{i}", request, clave, ct)
            : EnviarAsync<TipoDeBodegaDto>(HttpMethod.Post, RutaDeTiposDeBodega, request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<BodegaDto>>> ListarBodegasAsync(Guid? sucursal = null, bool incluirTransito = false,
        bool incluirInactivas = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<BodegaDto>>(HttpMethod.Get, ConQuery(RutaDeBodegas, Query(
            ("branchPublicId", sucursal?.ToString()), ("includeTransit", incluirTransito ? "true" : null),
            ("includeInactive", incluirInactivas ? "true" : null))), null, null, ct);

    /// <summary>La bodega con sus ubicaciones y su saldo inicial.</summary>
    public Task<ResultadoDeInventario<BodegaDto>> ObtenerBodegaAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<BodegaDto>(HttpMethod.Get, $"{RutaDeBodegas}/{id}", null, null, ct);

    /// <summary>La primera bodega operativa de una sucursal trae su tránsito (<c>transitWarehouse</c> fija su código).</summary>
    public Task<ResultadoDeInventario<AltaDeBodegaDto>> CrearBodegaAsync(CrearBodegaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<AltaDeBodegaDto>(HttpMethod.Post, RutaDeBodegas, request, clave, ct);

    public Task<ResultadoDeInventario<BodegaDto>> EditarBodegaAsync(Guid id, EditarBodegaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<BodegaDto>(HttpMethod.Put, $"{RutaDeBodegas}/{id}", request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<UbicacionDto>>> ListarUbicacionesAsync(Guid bodega, bool incluirInactivas = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<UbicacionDto>>(HttpMethod.Get, ConInactivos($"{RutaDeBodegas}/{bodega}/locations", incluirInactivas), null, null, ct);

    public Task<ResultadoDeInventario<UbicacionDto>> GuardarUbicacionAsync(Guid bodega, Guid? ubicacion, UbicacionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        ubicacion is { } u
            ? EnviarAsync<UbicacionDto>(HttpMethod.Put, $"{RutaDeBodegas}/{bodega}/locations/{u}", request, clave, ct)
            : EnviarAsync<UbicacionDto>(HttpMethod.Post, $"{RutaDeBodegas}/{bodega}/locations", request, clave, ct);

    public Task<ResultadoDeInventario<object>> CambiarUbicacionActivaAsync(Guid bodega, Guid ubicacion, bool activar, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<object>(HttpMethod.Post, $"{RutaDeBodegas}/{bodega}/locations/{ubicacion}/{(activar ? "reactivate" : "deactivate")}",
            new MotivoDeInventarioRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<PoliticaDeReordenDto>>> ListarReordenAsync(Guid? bodega = null, Guid? producto = null,
        bool? bajoElPunto = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<PoliticaDeReordenDto>>(HttpMethod.Get, ConQuery(RutaDeReorden, Query(
            ("warehousePublicId", bodega?.ToString()), ("productPublicId", producto?.ToString()),
            ("belowReorderPoint", bajoElPunto is true ? "true" : null), ("page", pagina.ToString()), ("pageSize", tamano.ToString()))), null, null, ct);

    /// <summary>Alta o cambio por (producto, bodega): <c>0 ≤ mínimo ≤ punto ≤ máximo</c>.</summary>
    public Task<ResultadoDeInventario<PoliticaDeReordenDto>> FijarReordenAsync(PoliticaDeReordenRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<PoliticaDeReordenDto>(HttpMethod.Put, RutaDeReorden, request, clave, ct);

    public Task<ResultadoDeInventario<object>> RetirarReordenAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<object>(HttpMethod.Delete, $"{RutaDeReorden}/{id}", null, clave, ct);

    /// <summary>Las sucursales contables, para elegir la de una bodega (<c>GET /api/core/branches</c>).</summary>
    public async Task<ResultadoDeInventario<IReadOnlyList<SucursalContableDto>>> ListarSucursalesAsync(CancellationToken ct = default)
    {
        var r = await EnviarAsync<PaginaDeInventarioDto<SucursalContableDto>>(HttpMethod.Get, "/api/core/branches?PageNumber=1&PageSize=500&SortBy=name", null, null, ct);
        return new ResultadoDeInventario<IReadOnlyList<SucursalContableDto>>(r.IsSuccess, r.Value?.Items, r.ErrorCode, r.ErrorMessage, r.StatusCode, r.Data, r.Repetida);
    }
}
