using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Cliente tipado del módulo de Inventario (feature 012, T183; T45), en el molde de <c>ContabilidadClient</c> e
/// <c>ImpuestosClient</c>. Es <c>partial</c>: la base (este archivo) pone el envío común y cada historia suma su parte
/// en su propio archivo (<c>.Documentos</c>, <c>.TiposDeDocumento</c>, <c>.Plantillas</c>, <c>.Informes</c> desde la
/// fase 3; <c>.Catalogo</c>, <c>.Bodegas</c>, <c>.Existencias</c>, <c>.Ajustes</c>… después). Los DTO espejo viven en
/// <c>InventarioDtos.cs</c>. (nuevo)
///
/// <list type="bullet">
/// <item>La cabecera <c>Authorization</c> la pone el handler de la sesión (<c>ElTokenDeSesionLoPoneElHandler</c>); aquí
/// sólo se comprueba que haya sesión.</item>
/// <item>Toda escritura lleva la <c>Idempotency-Key</c> de la operación de pantalla (<see cref="ClaveDeOperacion"/>):
/// la pantalla crea una al iniciar la operación y la pasa en cada envío; el cliente la conserva en el reintento con el
/// mismo contenido y la renueva tras el éxito. Las consultas no la llevan.</item>
/// <item>Las respuestas son <see cref="ResultadoDeInventario{T}"/>: conservan el <c>data</c> del error.</item>
/// </list>
/// </summary>
public sealed partial class InventarioClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/inventory";

    // -------------------------------------------------------------------------------------------- envío --

    /// <summary>Una petición JSON; con <paramref name="clave"/> es una escritura idempotente.</summary>
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

    /// <summary>
    /// Un archivo (multipart, campo <c>file</c>) más campos de texto, con su clave: la huella de la operación es el
    /// nombre, el SHA-256 del contenido y los campos, así el mismo archivo reenviado conserva la clave.
    /// </summary>
    private async Task<ResultadoDeInventario<T>> SubirAsync<T>(string url, string archivo, byte[] contenido,
        IReadOnlyDictionary<string, string>? campos, ClaveDeOperacion clave, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<T>.SinToken();
        try
        {
            using var req = Multipart(url, archivo, contenido, campos, clave);
            using var resp = await http.SendAsync(req, ct);
            var resultado = await ResultadoDeInventario<T>.DesdeAsync(resp, ct);
            if (resultado.IsSuccess) clave.Exito();
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

    /// <summary>Como <see cref="SubirAsync{T}"/>, pero la respuesta es un archivo (la revisión en Excel).</summary>
    private async Task<InvitationApiResult<ArchivoDescargado>> SubirYDescargarAsync(string url, string archivo, byte[] contenido,
        IReadOnlyDictionary<string, string>? campos, ClaveDeOperacion clave, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = Multipart(url, archivo, contenido, campos, clave);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
            clave.Exito();
            return InvitationApiResult<ArchivoDescargado>.Success(await ArchivoAsync(resp, archivo, ct));
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }

    /// <summary>Una descarga (plantilla, informe exportado). Es una lectura: sin clave.</summary>
    private async Task<InvitationApiResult<ArchivoDescargado>> DescargarAsync(string url, string nombrePorDefecto, CancellationToken ct)
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

    private static HttpRequestMessage Multipart(string url, string archivo, byte[] contenido, IReadOnlyDictionary<string, string>? campos, ClaveDeOperacion clave)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        var multipart = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(contenido);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(bytes, "file", archivo);
        if (campos is not null)
            foreach (var (nombre, valor) in campos.OrderBy(c => c.Key, StringComparer.Ordinal)) multipart.Add(new StringContent(valor), nombre);
        req.Content = multipart;
        clave.Aplicar(req, new
        {
            url,
            archivo,
            sha256 = Convert.ToHexString(SHA256.HashData(contenido)),
            campos = campos?.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}={c.Value}").ToArray(),
        });
        return req;
    }

    private static async Task<ArchivoDescargado> ArchivoAsync(HttpResponseMessage resp, string nombrePorDefecto, CancellationToken ct)
    {
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var nombre = resp.Content.Headers.ContentDisposition?.FileNameStar
                     ?? resp.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                     ?? nombrePorDefecto;
        return new ArchivoDescargado(nombre, resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", bytes);
    }

    /// <summary>Arma la query string con los pares que traen valor (<c>a=1&amp;b=2</c>, sin «?»).</summary>
    internal static string Query(params (string Nombre, string? Valor)[] pares) =>
        string.Join('&', pares.Where(p => !string.IsNullOrWhiteSpace(p.Valor))
            .Select(p => $"{p.Nombre}={Uri.EscapeDataString(p.Valor!.Trim())}"));

    /// <summary><paramref name="ruta"/> con la query si hay algo que poner.</summary>
    internal static string ConQuery(string ruta, string query) => string.IsNullOrEmpty(query) ? ruta : $"{ruta}?{query}";
}
