using System.Net;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Shared.Services;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Tests.Sesion;

/// <summary>Reloj que sólo avanza cuando la prueba lo dice.</summary>
internal sealed class RelojFijo(DateTime utcNow) : TimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
    public void Avanzar(TimeSpan cuanto) => UtcNow = UtcNow.Add(cuanto);
}

/// <summary>El sessionStorage del navegador, en un diccionario.</summary>
internal sealed class AlmacenEnMemoria : ISecureStorage
{
    public Dictionary<string, string> Datos { get; } = new(StringComparer.Ordinal);

    public Task SetAsync(string key, string value) { Datos[key] = value; return Task.CompletedTask; }
    public Task<string?> GetAsync(string key) => Task.FromResult(Datos.TryGetValue(key, out var v) ? v : null);
    public bool Remove(string key) => Datos.Remove(key);
    public void RemoveAll() => Datos.Clear();
}

/// <summary>
/// El servidor, reducido a una función. Registra cada petición que le llega, con
/// una copia del cuerpo, para que la prueba afirme sobre lo que salió.
/// </summary>
internal sealed class ServidorFalso : HttpMessageHandler
{
    public List<PeticionVista> Peticiones { get; } = [];
    public Func<HttpRequestMessage, int, HttpResponseMessage> Responder { get; set; } =
        (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

    /// <summary>Se dispara al recibir una petición; sirve para sostener varias en vuelo a la vez.</summary>
    public Func<HttpRequestMessage, Task>? AntesDeResponder { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        var vista = new PeticionVista(
            request.Method,
            request.RequestUri!.PathAndQuery,
            request.Headers.Authorization?.Parameter,
            cuerpo,
            request.Options.TryGetValue(RenovadorDeSesion.SinSesion, out var sin) && sin);
        lock (Peticiones) Peticiones.Add(vista);
        if (AntesDeResponder is not null) await AntesDeResponder(request);
        return Responder(request, Peticiones.Count);
    }

    public IReadOnlyList<PeticionVista> A(string ruta) => Peticiones.Where(p => p.Ruta == ruta).ToList();

    public static HttpResponseMessage Json(HttpStatusCode status, object cuerpo) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json"),
    };

    public static HttpResponseMessage SobreDeError(HttpStatusCode status, string code) =>
        Json(status, new { code, message = "x", traceId = "t" });
}

internal sealed record PeticionVista(HttpMethod Metodo, string Ruta, string? Bearer, string? Cuerpo, bool SinSesion);

/// <summary>Fábrica de <c>HttpClient</c> que siempre devuelve el mismo servidor falso.</summary>
internal sealed class FabricaDeUnSoloServidor(HttpMessageHandler raiz) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(raiz, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
}

internal static class Jwt
{
    /// <summary>Un JWT sin firma válida pero con el claim <c>exp</c> que el cliente lee.</summary>
    public static string ConVencimiento(DateTime expUtc, string sub = "ana")
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var exp = new DateTimeOffset(expUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        return $"{B64("{\"alg\":\"RS256\"}")}.{B64($"{{\"sub\":\"{sub}\",\"exp\":{exp}}}")}.firma";
    }
}
