using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// El envío común de <see cref="VentasClient"/> y <c>MediosDePagoClient</c> (feature 012, I3, T633), en el molde de
/// <c>InventarioClient</c>: sin sesión no sale nada; la cabecera <c>Authorization</c> la pone el handler de la sesión
/// (<c>ElTokenDeSesionLoPoneElHandler</c>); con <see cref="ClaveDeOperacion"/> es una escritura idempotente que conserva la clave en el
/// reintento con el mismo contenido y la renueva tras el éxito; las consultas no la llevan. (nuevo)
/// </summary>
internal static class EnvioDeVentas
{
    public static async Task<ResultadoDeInventario<T>> EnviarAsync<T>(HttpClient http, CentralAuthClient auth, HttpMethod metodo, string url, object? cuerpo,
        ClaveDeOperacion? clave, CancellationToken ct)
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

    /// <summary>Una descarga (PDF del arqueo, del comprobante de un movimiento). Es una lectura: sin clave.</summary>
    public static async Task<InvitationApiResult<ArchivoDescargado>> DescargarAsync(HttpClient http, CentralAuthClient auth, string url, string nombrePorDefecto,
        CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
            return InvitationApiResult<ArchivoDescargado>.Success(await ArchivoAsync(resp, nombrePorDefecto, ct));
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }

    public static async Task<ArchivoDescargado> ArchivoAsync(HttpResponseMessage resp, string nombrePorDefecto, CancellationToken ct)
    {
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var nombre = resp.Content.Headers.ContentDisposition?.FileNameStar
                     ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                     ?? nombrePorDefecto;
        return new ArchivoDescargado(nombre, resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", bytes);
    }

    /// <summary>Arma la query string con los pares que traen valor (<c>a=1&amp;b=2</c>, sin «?»).</summary>
    public static string Query(params (string Nombre, string? Valor)[] pares) => InventarioClient.Query(pares);

    /// <summary><paramref name="ruta"/> con la query si hay algo que poner.</summary>
    public static string ConQuery(string ruta, string query) => InventarioClient.ConQuery(ruta, query);

    public static string? Texto(Guid? id) => id?.ToString();

    public static string? Texto(bool? valor) => valor is { } v ? (v ? "true" : "false") : null;

    public static string? Texto(DateOnly? fecha) => fecha?.ToString("yyyy-MM-dd");

    public static string? Texto(int? numero) => numero?.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
