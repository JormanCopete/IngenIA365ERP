using IngenIA365ERP.Shared.Models.Compras;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Compras;

/// <summary>
/// Compras completas (feature 012, I5, T810; contracts/api.md §14.8, §14.9): solicitudes y órdenes (listas por clase, detalle con lo
/// pendiente por línea, el PDF de la orden, el envío al proveedor y el cierre del saldo), el cruce a tres vías (la lista por estado y
/// proveedor, y el de una factura), los costos adicionales (lista y detalle con su reparto) y la emisión RADIAN desde el ERP. Guardar,
/// confirmar, descartar y anular van por el ciclo común de <see cref="ComprasClient"/> sobre <see cref="Rutas.Solicitudes"/>,
/// <see cref="Rutas.Ordenes"/> y <see cref="Rutas.CostosAdicionales"/>. Sin cabecera <c>Authorization</c> a mano: la pone el handler de la
/// sesión (<c>ElTokenDeSesionLoPoneElHandler</c>); toda escritura lleva la clave de la operación de pantalla. (nuevo)
/// </summary>
public sealed partial class ComprasClient
{
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeDocumentoDto>>> ListarSolicitudesAsync(FiltroDeCompras filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeDocumentoDto>>(HttpMethod.Get, InventarioClient.ConQuery(Rutas.Solicitudes, Query(filtro)), null, null, ct);

    /// <summary>La solicitud con <c>neededBy</c> y, por línea, lo pendiente por ordenar.</summary>
    public Task<ResultadoDeInventario<DocumentoDeCompraDto>> ObtenerSolicitudAsync(Guid id, CancellationToken ct = default) => ObtenerAsync(Rutas.Solicitudes, id, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeDocumentoDto>>> ListarOrdenesAsync(FiltroDeCompras filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeDocumentoDto>>(HttpMethod.Get, InventarioClient.ConQuery(Rutas.Ordenes, Query(filtro)), null, null, ct);

    /// <summary>La orden con la entrega esperada, sus condiciones, el cierre del saldo y, por línea, lo pendiente por recibir.</summary>
    public Task<ResultadoDeInventario<DocumentoDeCompraDto>> ObtenerOrdenAsync(Guid id, CancellationToken ct = default) => ObtenerAsync(Rutas.Ordenes, id, ct);

    /// <summary>La orden en PDF. Es una lectura: sin clave.</summary>
    public async Task<InvitationApiResult<ArchivoDescargado>> DescargarOrdenEnPdfAsync(Guid id, CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{Rutas.Ordenes}/{id}/pdf");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var nombre = resp.Content.Headers.ContentDisposition?.FileNameStar
                         ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                         ?? $"orden-{id:N}.pdf";
            return InvitationApiResult<ArchivoDescargado>.Success(new ArchivoDescargado(nombre, resp.Content.Headers.ContentType?.MediaType ?? "application/pdf", bytes));
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }

    /// <summary>Envía el PDF de la orden confirmada al correo dado o, sin él, al del proveedor en el maestro (204).</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> EnviarOrdenAsync(Guid id, string? correo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Rutas.Ordenes}/{id}/send", new EnviarOrdenRequest(string.IsNullOrWhiteSpace(correo) ? null : correo.Trim()), clave, ct);

    /// <summary>Cierra lo pendiente por recibir de la orden, con motivo: ya no admite recepciones (204).</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> CerrarSaldoDeOrdenAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Rutas.Ordenes}/{id}/close-balance", new CerrarSaldoRequest(motivo), clave, ct);

    /// <summary>Las líneas del cruce de las facturas visibles, por estado (<see cref="EstadosDelCruce"/>) y proveedor.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<LineaDelCruceDto>>> ListarCruceAsync(int? estado, Guid? proveedor, int pagina = 1, int tamano = 20,
        CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<LineaDelCruceDto>>(HttpMethod.Get, InventarioClient.ConQuery(Rutas.Cruce, InventarioClient.Query(
            ("status", estado?.ToString()),
            ("supplierPersonPublicId", proveedor?.ToString()),
            ("page", pagina.ToString()),
            ("pageSize", tamano.ToString()))), null, null, ct);

    /// <summary>El cruce de una factura, línea por línea (vacío si no se cruzó contra una orden).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<LineaDelCruceDto>>> CruceDeFacturaAsync(Guid factura, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<LineaDelCruceDto>>(HttpMethod.Get, $"{Rutas.Facturas}/{factura}/match", null, null, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeDocumentoDto>>> ListarCostosAdicionalesAsync(FiltroDeCompras filtro, CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeDocumentoDto>>(HttpMethod.Get, InventarioClient.ConQuery(Rutas.CostosAdicionales, Query(filtro)), null, null, ct);

    /// <summary>Los costos adicionales con su reparto (<c>Document.LandedCost</c>).</summary>
    public Task<ResultadoDeInventario<DocumentoDeCompraDto>> ObtenerCostosAdicionalesAsync(Guid id, CancellationToken ct = default) =>
        ObtenerAsync(Rutas.CostosAdicionales, id, ct);

    /// <summary>
    /// El ERP emite el acuse (30) y/o el recibo del bien (32) por el canal de facturación electrónica (202): quedan pendientes y los lleva
    /// el procesador (<c>Inventory.Purchases.EmitRadianEvent</c>).
    /// </summary>
    public Task<ResultadoDeInventario<EmisionRadianDto>> EmitirEventosRadianAsync(Guid factura, IReadOnlyList<int> eventos, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<EmisionRadianDto>(HttpMethod.Post, $"{Rutas.Facturas}/{factura}/radian-events/emit", new EmitirEventosRadianRequest(eventos), clave, ct);
}
