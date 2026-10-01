using System.Net;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Tests.Seguridad;
using IngenIA365ERP.Shared.Tests.Sesion;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// El servidor de prueba de los clientes de la integración contable (feature 012, US7, T532): anota cada petición —ruta con su
/// query, <c>Idempotency-Key</c>, <c>Authorization</c> y cuerpo— y responde lo que la prueba diga. Arma también la sesión con la
/// que los clientes salen (sin <c>AuthBearerHandler</c> en la cadena: si el cliente pusiera <c>Authorization</c>, se vería). (nuevo)
/// </summary>
internal sealed class ServidorDeIntegracion : HttpMessageHandler
{
    public static readonly DateTime Ahora = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    public List<VistaDePeticion> Vistas { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        Vistas.Add(new VistaDePeticion(
            request.Method.Method,
            request.RequestUri!.PathAndQuery,
            request.Headers.TryGetValues(ClaveDeOperacion.Cabecera, out var claves) ? claves.Single() : null,
            request.Headers.Authorization?.ToString(),
            cuerpo));
        return Responder(request);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, object cuerpo) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json"),
    };

    /// <summary>Un <see cref="HttpClient"/> con sesión vigente sobre este servidor, y su <see cref="CentralAuthClient"/>.</summary>
    public (HttpClient Http, CentralAuthClient Auth) ConSesion()
    {
        var sesion = new RenovadorDeSesion(new AlmacenEnMemoria(), new FabricaDeUnSoloServidor(this), new RelojFijo(Ahora));
        var http = new HttpClient(this, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
        var auth = new CentralAuthClient(http, sesion, new EstadoDeAutenticacionFalso());
        auth.AdoptSessionAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(12)).GetAwaiter().GetResult();
        Vistas.Clear();
        return (http, auth);
    }
}

internal sealed record VistaDePeticion(string Metodo, string Ruta, string? Clave, string? Authorization, string? Cuerpo);
