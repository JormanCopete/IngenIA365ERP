using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services;
using IngenIA365ERP.Shared.Services.Security;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Sesion;

/// <summary>
/// El handler que aplica la renovación a cada petición del cliente <c>api</c>:
/// pone el Bearer vigente, y ante un 401 canjea y reintenta una sola vez.
/// </summary>
public class RenovacionDeSesionHandlerTests
{
    private static readonly DateTime Ahora = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly RelojFijo _reloj = new(Ahora);
    private readonly AlmacenEnMemoria _almacen = new();
    private readonly ServidorFalso _servidor = new();
    private readonly RenovadorDeSesion _sesion;
    private readonly HttpClient _http;

    public RenovacionDeSesionHandlerTests()
    {
        _sesion = new RenovadorDeSesion(_almacen, new FabricaDeUnSoloServidor(_servidor), _reloj);
        // La cadena real: renovación → bearer → servidor. El de cooperativa no aporta nada aquí.
        var cadena = new RenovacionDeSesionHandler(_sesion)
        {
            InnerHandler = new AuthBearerHandler(_almacen) { InnerHandler = _servidor },
        };
        _http = new HttpClient(cadena) { BaseAddress = new Uri("https://erp.pruebas") };
    }

    private async Task ConSesionAsync(TimeSpan leQuedaAlAccess)
    {
        await _sesion.AdoptarAsync(
            Jwt.ConVencimiento(Ahora.Add(leQuedaAlAccess)), Ahora.Add(leQuedaAlAccess),
            "refresh-1", Ahora.AddHours(12));
        _servidor.Peticiones.Clear();
    }

    private static HttpResponseMessage Renovacion(string access, string refresh) =>
        ServidorFalso.Json(HttpStatusCode.OK, new
        {
            accessToken = access,
            accessTokenExpiresAt = Ahora.AddMinutes(15),
            refreshToken = refresh,
            refreshTokenExpiresAt = Ahora.AddHours(11),
            expiresInSeconds = 900,
        });

    [Fact]
    public async Task Pone_el_bearer_vigente_sin_renovar_si_no_hace_falta()
    {
        await ConSesionAsync(TimeSpan.FromMinutes(10));

        var resp = await _http.GetAsync("/api/payroll/pay-periods");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var vista = _servidor.Peticiones.Should().ContainSingle().Subject;
        vista.Bearer.Should().Be(_sesion.AccessToken);
    }

    [Fact]
    public async Task Por_vencer_renueva_antes_de_enviar()
    {
        await ConSesionAsync(TimeSpan.FromSeconds(20));
        _servidor.Responder = (req, _) => req.RequestUri!.AbsolutePath == "/api/auth/refresh"
            ? Renovacion("access-2", "refresh-2")
            : new HttpResponseMessage(HttpStatusCode.OK);

        await _http.GetAsync("/api/payroll/pay-periods");

        _servidor.Peticiones.Select(p => p.Ruta).Should().Equal("/api/auth/refresh", "/api/payroll/pay-periods");
        _servidor.A("/api/payroll/pay-periods").Single().Bearer.Should().Be("access-2");
    }

    [Fact]
    public async Task Un_401_inesperado_renueva_y_reintenta_una_vez_con_el_mismo_cuerpo()
    {
        await ConSesionAsync(TimeSpan.FromMinutes(10));
        var original = _sesion.AccessToken;
        var vecesAlRecurso = 0;
        _servidor.Responder = (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == "/api/auth/refresh") return Renovacion("access-2", "refresh-2");
            // Primera vez: el servidor ya no acepta ese token (revocación, reloj…).
            return ++vecesAlRecurso == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.Created);
        };

        var resp = await _http.PostAsJsonAsync("/api/payroll/novelties", new { conceptCode = "HEX_NOCTURNA", quantity = 6 });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var intentos = _servidor.A("/api/payroll/novelties");
        intentos.Should().HaveCount(2);
        intentos[0].Bearer.Should().Be(original);
        intentos[1].Bearer.Should().Be("access-2");
        intentos[1].Cuerpo.Should().Be(intentos[0].Cuerpo).And.Contain("HEX_NOCTURNA");
        _servidor.A("/api/auth/refresh").Should().HaveCount(1);
    }

    [Fact]
    public async Task Si_tras_el_401_el_refresh_tampoco_vale_devuelve_el_401_y_termina_la_sesion()
    {
        await ConSesionAsync(TimeSpan.FromMinutes(10));
        _servidor.Responder = (req, _) => req.RequestUri!.AbsolutePath == "/api/auth/refresh"
            ? ServidorFalso.SobreDeError(HttpStatusCode.Unauthorized, "Identity.RefreshToken.Reused")
            : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        string? terminada = null;
        _sesion.SesionTerminada += c => terminada = c;

        var resp = await _http.GetAsync("/api/auth/me");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        terminada.Should().Be("Identity.RefreshToken.Reused");
        _servidor.A("/api/auth/me").Should().HaveCount(1, "sin token nuevo no hay reintento");
    }

    [Fact]
    public async Task Con_authorization_explicita_no_renueva_ni_reintenta()
    {
        // Son los desafíos (mfa/verify, select-tenant): viajan con un token temporal
        // que no es la sesión y que no se puede renovar.
        await ConSesionAsync(TimeSpan.FromSeconds(20));
        _servidor.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/verify");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token-de-desafio");

        var resp = await _http.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var vista = _servidor.Peticiones.Should().ContainSingle().Subject;
        vista.Bearer.Should().Be("token-de-desafio");
    }

    [Fact]
    public async Task Sin_sesion_la_peticion_sale_anonima()
    {
        _servidor.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        await _http.GetAsync("/api/health");

        _servidor.Peticiones.Should().ContainSingle().Which.Bearer.Should().BeNull();
        _servidor.A("/api/auth/refresh").Should().BeEmpty();
    }

    [Fact]
    public async Task Un_cuerpo_mayor_al_tope_sale_sin_reintento()
    {
        await ConSesionAsync(TimeSpan.FromMinutes(10));
        _servidor.Responder = (req, _) => req.RequestUri!.AbsolutePath == "/api/auth/refresh"
            ? Renovacion("access-2", "refresh-2")
            : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var grande = new ByteArrayContent(new byte[RenovacionDeSesionHandler.MaximoReintentable + 1]);

        var resp = await _http.PostAsync("/api/attachments", grande);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _servidor.A("/api/attachments").Should().HaveCount(1);
        _servidor.A("/api/auth/refresh").Should().BeEmpty();
    }

    [Fact]
    public async Task La_marca_SinSesion_deja_pasar_la_peticion_tal_cual()
    {
        await ConSesionAsync(TimeSpan.FromSeconds(20));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Options.Set(RenovadorDeSesion.SinSesion, true);

        await _http.SendAsync(req);

        var vista = _servidor.Peticiones.Should().ContainSingle().Subject;
        vista.Bearer.Should().BeNull();
        vista.SinSesion.Should().BeTrue();
    }
}
