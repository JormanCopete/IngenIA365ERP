using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Traslados en dos pasos (feature 012, US10, T379; contracts/api.md §11) sobre <c>/api/inventory/transfers</c>: la lista con su
/// estado derivado, el detalle, el borrador del despacho por el ciclo común, despachar, recibir, las diferencias y su resolución,
/// anular un despacho no recibido y los destinos permitidos. Toda escritura lleva la <see cref="ClaveDeOperacion"/> de la operación
/// de pantalla. El movimiento entre ubicaciones va por la ruta de ajustes (<see cref="GuardarAjusteAsync"/>, clase 18).
/// </summary>
public sealed partial class InventarioClient
{
    /// <summary>La clase del despacho (<c>DocumentClass.TransferDispatch</c>).</summary>
    public const int ClaseDespachoDeTraslado = 16;

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeTrasladoDto>>> ListarTrasladosAsync(FiltroDeTraslados filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeTrasladoDto>>(HttpMethod.Get, ConQuery(RutasDeGrupo.Traslados, Query(
            ("state", filtro.State),
            ("originWarehousePublicId", filtro.OriginWarehousePublicId?.ToString()),
            ("destinationWarehousePublicId", filtro.DestinationWarehousePublicId?.ToString()),
            ("from", filtro.From?.ToString("yyyy-MM-dd")),
            ("to", filtro.To?.ToString("yyyy-MM-dd")),
            ("page", filtro.Page.ToString()),
            ("pageSize", filtro.PageSize.ToString()))), null, null, ct);

    /// <summary>El traslado: el despacho (con sus <c>AllowedActions</c>), sus recepciones, las líneas y las diferencias.</summary>
    public Task<ResultadoDeInventario<TrasladoDto>> ObtenerTrasladoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<TrasladoDto>(HttpMethod.Get, $"{RutasDeGrupo.Traslados}/{id}", null, null, ct);

    /// <summary>Guarda el borrador del despacho (origen, destino y líneas).</summary>
    public Task<ResultadoDeInventario<DocumentoDeInventarioDto>> GuardarTrasladoAsync(Guid? id, BorradorDeInventarioRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        GuardarBorradorAsync(RutasDeGrupo.Traslados, id, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> DescartarTrasladoAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        DescartarAsync(RutasDeGrupo.Traslados, id, motivo, clave, ct);

    /// <summary>Despacha: con niveles de aprobación queda en aprobación sin número; un 422 trae en <c>data</c> lo que falta.</summary>
    public Task<ResultadoDeInventario<ResultadoDeConfirmacionDto>> DespacharTrasladoAsync(Guid id, byte[]? rowVersion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeConfirmacionDto>(HttpMethod.Post, $"{RutasDeGrupo.Traslados}/{id}/dispatch", new ConfirmacionRequest(rowVersion), clave, ct);

    /// <summary>Recibe: crea y confirma la recepción; lo que falta o sobra queda como diferencia pendiente.</summary>
    public Task<ResultadoDeInventario<ResultadoDeRecepcionDto>> RecibirTrasladoAsync(Guid id, RecibirTrasladoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeRecepcionDto>(HttpMethod.Post, $"{RutasDeGrupo.Traslados}/{id}/receive", request, clave, ct);

    /// <summary>Las diferencias de los traslados del alcance: <paramref name="estado"/> Pending, InApproval o Resolved.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<DiferenciaDeTrasladoDto>>> ListarDiferenciasDeTrasladoAsync(string? estado, Guid? bodega = null,
        int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<DiferenciaDeTrasladoDto>>(HttpMethod.Get, ConQuery($"{RutasDeGrupo.Traslados}/discrepancies", Query(
            ("status", estado),
            ("warehousePublicId", bodega?.ToString()),
            ("page", pagina.ToString()),
            ("pageSize", tamano.ToString()))), null, null, ct);

    /// <summary>Pide resolver una diferencia: crea el documento y lo deja en aprobación de otra persona.</summary>
    public Task<ResultadoDeInventario<ResultadoDeResolucionDto>> ResolverDiferenciaAsync(Guid diferencia, ResolverDiferenciaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeResolucionDto>(HttpMethod.Post, $"{RutasDeGrupo.Traslados}/discrepancies/{diferencia}/resolve", request, clave, ct);

    /// <summary>Anula un despacho no recibido: la mercancía vuelve del tránsito al origen.</summary>
    public Task<ResultadoDeInventario<ResultadoDeAnulacionDto>> AnularTrasladoAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        AnularAsync(RutasDeGrupo.Traslados, id, motivo, null, clave, ct);

    /// <summary>Las bodegas a las que se puede trasladar: todas las operativas y activas, sin existencias ni valores.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<DestinoDeTrasladoDto>>> ListarDestinosDeTrasladoAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<DestinoDeTrasladoDto>>(HttpMethod.Get, $"{RutasDeGrupo.Traslados}/destinations", null, null, ct);
}
