using System.Net;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Tests.Seguridad;
using IngenIA365ERP.Shared.Tests.Sesion;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// El doble del servidor para los clientes de Cartera (feature 013, fase 4, T0266): anota cada petición con su ruta, su método, la clave de
/// operación y la cabecera <c>Authorization</c> que el CLIENTE haya puesto. La sesión se arma sin <c>AuthBearerHandler</c> en la cadena, a
/// propósito: si un cliente pusiera <c>Authorization</c> a mano, se vería aquí. (nuevo)
/// </summary>
internal sealed class ServidorDeCartera : HttpMessageHandler
{
    private static readonly DateTime Ahora = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    public List<PeticionVista> Vistas { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => Json(HttpStatusCode.OK, new { });

    /// <summary>Un <see cref="HttpClient"/> y una sesión vigente sobre este servidor.</summary>
    public (HttpClient Http, CentralAuthClient Auth) Sesion()
    {
        var sesion = new RenovadorDeSesion(new AlmacenEnMemoria(), new FabricaDeUnSoloServidor(this), new RelojFijo(Ahora));
        var http = new HttpClient(this, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
        var auth = new CentralAuthClient(http, sesion, new EstadoDeAutenticacionFalso());
        auth.AdoptSessionAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(12)).GetAwaiter().GetResult();
        Vistas.Clear();
        return (http, auth);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Vistas.Add(new PeticionVista(
            request.Method.Method,
            request.RequestUri!.PathAndQuery,
            request.Headers.TryGetValues(ClaveDeOperacion.Cabecera, out var claves) ? claves.Single() : null,
            request.Headers.Authorization?.ToString()));
        return Task.FromResult(Responder(request));
    }

    public static HttpResponseMessage Json(HttpStatusCode status, object cuerpo) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json"),
    };

    /// <summary>El 422 de un permiso que depende del cuerpo (api.md §1.4).</summary>
    public static HttpResponseMessage PermisoRequerido(params string[] permisos) => Json(HttpStatusCode.UnprocessableEntity, new
    {
        code = "Lending.Permission.Required",
        message = "Le falta un permiso para esta acción.",
        data = new { permissionCodes = permisos },
        traceId = "t",
    });
}

internal sealed record PeticionVista(string Metodo, string Ruta, string? Clave, string? Authorization);
