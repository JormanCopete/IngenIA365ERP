using IngenIA365ERP.Shared.Services.Http;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Tipos de documento y su numeración (feature 012, T183; contracts/api.md §8): las clases fijas, la lista, el detalle
/// con el historial de consecutivos, alta, edición, cambio de consecutivo, inactivar y reactivar. La plantilla 8 se
/// descarga e importa con los métodos genéricos de <c>InventarioClient.Plantillas</c> sobre <see cref="RutaDeTipos"/>.
/// </summary>
public sealed partial class InventarioClient
{
    /// <summary>La ruta base de los tipos (y de su plantilla 8).</summary>
    public const string RutaDeTipos = Base + "/document-types";

    public Task<ResultadoDeInventario<IReadOnlyList<ClaseDeDocumentoDto>>> ClasesAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ClaseDeDocumentoDto>>(HttpMethod.Get, $"{RutaDeTipos}/classes", null, null, ct);

    /// <summary>Los tipos, filtrados por <c>DocumentClassGroup</c> y <c>DocumentClass</c> si se dan.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<TipoDeDocumentoDto>>> ListarTiposAsync(int? grupo = null, int? clase = null,
        bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<TipoDeDocumentoDto>>(HttpMethod.Get,
            ConQuery(RutaDeTipos, Query(("group", grupo?.ToString()), ("class", clase?.ToString()), ("includeInactive", incluirInactivos ? "true" : null))),
            null, null, ct);

    /// <summary>El tipo con el historial de consecutivos (<c>Sequences</c>).</summary>
    public Task<ResultadoDeInventario<TipoDeDocumentoDto>> ObtenerTipoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<TipoDeDocumentoDto>(HttpMethod.Get, $"{RutaDeTipos}/{id}", null, null, ct);

    public Task<ResultadoDeInventario<TipoDeDocumentoDto>> CrearTipoAsync(CrearTipoDeDocumentoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<TipoDeDocumentoDto>(HttpMethod.Post, RutaDeTipos, request, clave, ct);

    public Task<ResultadoDeInventario<TipoDeDocumentoDto>> EditarTipoAsync(Guid id, EditarTipoDeDocumentoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<TipoDeDocumentoDto>(HttpMethod.Put, $"{RutaDeTipos}/{id}", request, clave, ct);

    /// <summary>Un consecutivo nuevo (cambio de prefijo o de número) con motivo; cierra el vigente la víspera.</summary>
    public Task<ResultadoDeInventario<TipoDeDocumentoDto>> AgregarConsecutivoAsync(Guid id, ConsecutivoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<TipoDeDocumentoDto>(HttpMethod.Post, $"{RutaDeTipos}/{id}/sequences", request, clave, ct);

    public Task<ResultadoDeInventario<TipoDeDocumentoDto>> InactivarTipoAsync(Guid id, MotivoDeInventarioRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<TipoDeDocumentoDto>(HttpMethod.Post, $"{RutaDeTipos}/{id}/deactivate", request, clave, ct);

    public Task<ResultadoDeInventario<TipoDeDocumentoDto>> ReactivarTipoAsync(Guid id, MotivoDeInventarioRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<TipoDeDocumentoDto>(HttpMethod.Post, $"{RutaDeTipos}/{id}/reactivate", request, clave, ct);
}
