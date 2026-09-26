using System.Net.Http.Headers;
using System.Net.Http.Json;
using IngenIA365ERP.Shared.Models.Compras;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Compras;

/// <summary>
/// Cliente tipado de Compras (feature 012, T352; contracts/api.md §14), en el molde de <see cref="InventarioClient"/>. Es
/// <c>partial</c>: la base (este archivo) pone el envío común y el ciclo de cada clase; <c>.Recepciones</c>, <c>.Facturas</c>
/// (facturas, notas, prellenado y eventos RADIAN) y <c>.Devoluciones</c> suman lo suyo. (nuevo)
/// <list type="bullet">
/// <item>La cabecera <c>Authorization</c> la pone el handler de la sesión (<c>ElTokenDeSesionLoPoneElHandler</c>).</item>
/// <item>Toda escritura lleva la <c>Idempotency-Key</c> de la operación de pantalla (<see cref="ClaveDeOperacion"/>); el
/// prellenado desde el XML es una consulta y no la lleva.</item>
/// </list>
/// </summary>
public sealed partial class ComprasClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/inventory/purchases";

    /// <summary>La ruta de cada clase (§14).</summary>
    public static class Rutas
    {
        public const string Recepciones = Base + "/receipts";
        public const string Facturas = Base + "/supplier-invoices";
        public const string Notas = Base + "/supplier-notes";
        public const string Devoluciones = Base + "/returns";
        public const string CompraDirecta = Base + "/direct";
    }

    // ---------------------------------------------------------------------------------- ciclo común --

    public Task<ResultadoDeInventario<DocumentoDeCompraDto>> ObtenerAsync(string ruta, Guid id, CancellationToken ct = default) =>
        EnviarAsync<DocumentoDeCompraDto>(HttpMethod.Get, $"{ruta}/{id}", null, null, ct);

    /// <summary>Guarda el borrador: sin <paramref name="id"/> lo crea; con él lo reemplaza. Los impuestos vuelven como vista previa.</summary>
    public Task<ResultadoDeInventario<DocumentoDeInventarioDto>> GuardarAsync(string ruta, Guid? id, BorradorDeCompraRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        id is { } existente
            ? EnviarAsync<DocumentoDeInventarioDto>(HttpMethod.Put, $"{ruta}/{existente}", request, clave, ct)
            : EnviarAsync<DocumentoDeInventarioDto>(HttpMethod.Post, ruta, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> DescartarAsync(string ruta, Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{ruta}/{id}/discard", new MotivoDeInventarioRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeConfirmacionDto>> ConfirmarAsync(string ruta, Guid id, byte[]? rowVersion, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeConfirmacionDto>(HttpMethod.Post, $"{ruta}/{id}/confirm", new ConfirmacionRequest(rowVersion), clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeAnulacionDto>> AnularAsync(string ruta, Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeAnulacionDto>(HttpMethod.Post, $"{ruta}/{id}/void", new AnulacionRequest(motivo, null), clave, ct);

    // ---------------------------------------------------------------------------------------- envío --

    private async Task<ResultadoDeInventario<T>> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<T>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo, cuerpo.GetType());
            clave?.Aplicar(req, new { metodo = metodo.Method, url, cuerpo });
            using var resp = await http.SendAsync(req, ct);
            var resultado = await ResultadoDeInventario<T>.DesdeAsync(resp, ct);
            if (resultado.IsSuccess) clave?.Exito();
            return resultado;
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<T>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ResultadoDeInventario<T>.FormatoInesperado(ex.Message);
        }
    }

    /// <summary>Un archivo (multipart, campo <c>archivo</c>) para una consulta: sin clave, nada se guarda.</summary>
    private async Task<ResultadoDeInventario<T>> LeerArchivoAsync<T>(string url, string nombre, byte[] contenido, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<T>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            var multipart = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(contenido);
            bytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(bytes, "archivo", nombre);
            req.Content = multipart;
            using var resp = await http.SendAsync(req, ct);
            return await ResultadoDeInventario<T>.DesdeAsync(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<T>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ResultadoDeInventario<T>.FormatoInesperado(ex.Message);
        }
    }

    private static string Query(FiltroDeCompras f) => InventarioClient.Query(
        ("supplierPersonPublicId", f.SupplierPersonPublicId?.ToString()),
        ("status", f.Status?.ToString()),
        ("from", f.From?.ToString("yyyy-MM-dd")),
        ("to", f.To?.ToString("yyyy-MM-dd")),
        ("warehousePublicId", f.WarehousePublicId?.ToString()),
        ("paymentForm", f.PaymentForm),
        ("radianPending", f.RadianPending?.ToString().ToLowerInvariant()),
        ("number", f.Number),
        ("page", f.Page.ToString()),
        ("pageSize", f.PageSize.ToString()));
}
