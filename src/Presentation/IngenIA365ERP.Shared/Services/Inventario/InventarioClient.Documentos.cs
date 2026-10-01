using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// El documento de inventario (feature 012, T183; contracts/api.md §9): la lista y el detalle genéricos
/// (<c>/api/inventory/documents</c>, sin escritura) y el ciclo común sobre la ruta de cada grupo —guardar el borrador,
/// descartarlo, confirmarlo y anularlo—. Cada historia llama estos métodos con la ruta de su grupo
/// (<see cref="RutasDeGrupo"/>); lo propio de su clase (despachar, recibir, capturar un conteo…) va en su parcial.
/// </summary>
public sealed partial class InventarioClient
{
    /// <summary>Las rutas de los grupos con el ciclo común (§9.1). Compras publica una por clase bajo <c>/purchases/…</c>.</summary>
    public static class RutasDeGrupo
    {
        public const string Documentos = Base + "/documents";
        public const string Ajustes = Base + "/adjustments";
        public const string Traslados = Base + "/transfers";
        public const string Conteos = Base + "/counts";
        public const string SaldoInicial = Base + "/opening-balances";
    }

    /// <summary>La lista (genérica o la de un grupo): sólo de los grupos que la persona ve y de las bodegas de su alcance.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeDocumentoDto>>> ListarDocumentosAsync(FiltroDeDocumentosDeInventario filtro,
        string ruta = RutasDeGrupo.Documentos, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeDocumentoDto>>(HttpMethod.Get, ConQuery(ruta, Query(
            ("group", filtro.Group?.ToString()),
            ("class", filtro.Class?.ToString()),
            ("documentTypePublicId", filtro.DocumentTypePublicId?.ToString()),
            ("status", filtro.Status?.ToString()),
            ("from", filtro.From?.ToString("yyyy-MM-dd")),
            ("to", filtro.To?.ToString("yyyy-MM-dd")),
            ("warehousePublicId", filtro.WarehousePublicId?.ToString()),
            ("counterpartyPersonPublicId", filtro.CounterpartyPersonPublicId?.ToString()),
            ("number", filtro.Number),
            ("search", filtro.Search),
            ("page", filtro.Page.ToString()),
            ("pageSize", filtro.PageSize.ToString()))), null, null, ct);

    /// <summary>El detalle, con lo que el servidor permite hacer (<c>AllowedActions</c>).</summary>
    public Task<ResultadoDeInventario<DocumentoDeInventarioDto>> ObtenerDocumentoAsync(Guid id, string ruta = RutasDeGrupo.Documentos, CancellationToken ct = default) =>
        EnviarAsync<DocumentoDeInventarioDto>(HttpMethod.Get, $"{ruta}/{id}", null, null, ct);

    /// <summary>Guarda el borrador: sin <paramref name="id"/> lo crea (<c>POST</c>); con él reemplaza cabecera y líneas (<c>PUT</c>, con <c>rowVersion</c>).</summary>
    public Task<ResultadoDeInventario<DocumentoDeInventarioDto>> GuardarBorradorAsync(string ruta, Guid? id, BorradorDeInventarioRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        id is { } existente
            ? EnviarAsync<DocumentoDeInventarioDto>(HttpMethod.Put, $"{ruta}/{existente}", request, clave, ct)
            : EnviarAsync<DocumentoDeInventarioDto>(HttpMethod.Post, ruta, request, clave, ct);

    /// <summary>Descarta el borrador con motivo: queda <c>Discarded</c>, se conserva y no consume número.</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> DescartarAsync(string ruta, Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{ruta}/{id}/discard", new MotivoDeInventarioRequest(motivo), clave, ct);

    /// <summary>
    /// Confirma por el flujo canónico. Un 422 trae en <c>data</c> lo que la persona necesita para corregir (la existencia
    /// que falta, el período cerrado…): <see cref="ResultadoDeInventario{T}.Entero"/> y <see cref="ResultadoDeInventario{T}.Dato{TDato}"/>.
    /// </summary>
    public Task<ResultadoDeInventario<ResultadoDeConfirmacionDto>> ConfirmarAsync(string ruta, Guid id, byte[]? rowVersion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeConfirmacionDto>(HttpMethod.Post, $"{ruta}/{id}/confirm", new ConfirmacionRequest(rowVersion), clave, ct);

    /// <summary>Anula con un documento contrario, con motivo y, si el tipo lo admite, otra fecha que hoy.</summary>
    public Task<ResultadoDeInventario<ResultadoDeAnulacionDto>> AnularAsync(string ruta, Guid id, string motivo, DateOnly? fecha, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeAnulacionDto>(HttpMethod.Post, $"{ruta}/{id}/void", new AnulacionRequest(motivo, fecha), clave, ct);
}
