using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Sesion;

/// <summary>
/// Los catálogos se guardan unos minutos en el navegador (2026-11-24): la segunda pantalla que pide bancos no espera
/// a la API, pero nunca ve lo de otra cooperativa ni lo de otra persona, una escritura descarta lo guardado y lo que
/// no es catálogo o no fue 200 pasa siempre. (nuevo)
/// </summary>
public class CatalogosEnCacheHandlerTests
{
    private DateTimeOffset _ahora = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private readonly ApiQueCuenta _api = new();
    private readonly HttpMessageInvoker _cliente;

    public CatalogosEnCacheHandlerTests()
    {
        var cache = new CacheDeCatalogos(() => _ahora);
        _cliente = new HttpMessageInvoker(new CatalogosEnCacheHandler(cache) { InnerHandler = _api });
    }

    private static string Token(string persona, string cooperativa)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{\"alg\":\"none\"}")}.{B64($"{{\"sub\":\"{persona}\",\"active_tenant_id\":\"{cooperativa}\"}}")}.firma";
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, string? token)
    {
        var peticion = new HttpRequestMessage(metodo, new Uri("https://api.test" + ruta));
        if (token is not null) peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _cliente.SendAsync(peticion, CancellationToken.None);
    }

    [Fact]
    public async Task La_segunda_peticion_del_mismo_catalogo_no_llega_a_la_api()
    {
        var token = Token("ana", "coop-1");

        var primera = await EnviarAsync(HttpMethod.Get, "/api/core/banks?PageSize=500", token);
        var segunda = await EnviarAsync(HttpMethod.Get, "/api/core/banks?PageSize=500", token);

        _api.Llamadas.Should().Be(1);
        (await segunda.Content.ReadAsStringAsync()).Should().Be(await primera.Content.ReadAsStringAsync());
        segunda.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Otra_cooperativa_u_otra_persona_no_ven_lo_guardado()
    {
        await EnviarAsync(HttpMethod.Get, "/api/core/banks", Token("ana", "coop-1"));
        await EnviarAsync(HttpMethod.Get, "/api/core/banks", Token("ana", "coop-2"));
        await EnviarAsync(HttpMethod.Get, "/api/core/banks", Token("luis", "coop-1"));

        _api.Llamadas.Should().Be(3);
    }

    [Fact]
    public async Task Una_escritura_sobre_el_catalogo_descarta_lo_guardado()
    {
        var token = Token("ana", "coop-1");
        await EnviarAsync(HttpMethod.Get, "/api/core/banks", token);

        await EnviarAsync(HttpMethod.Post, "/api/core/banks", token);
        await EnviarAsync(HttpMethod.Get, "/api/core/banks", token);

        _api.Llamadas.Should().Be(3);
    }

    [Fact]
    public async Task Lo_guardado_vence_a_los_cinco_minutos()
    {
        var token = Token("ana", "coop-1");
        await EnviarAsync(HttpMethod.Get, "/api/core/cities?PageSize=2000", token);

        _ahora += CacheDeCatalogos.Vigencia + TimeSpan.FromSeconds(1);
        await EnviarAsync(HttpMethod.Get, "/api/core/cities?PageSize=2000", token);

        _api.Llamadas.Should().Be(2);
    }

    [Theory]
    [InlineData("/api/core/people?PageSize=500")]
    [InlineData("/api/core/banksx")]
    [InlineData("/api/lending/loans")]
    public async Task Lo_que_no_es_catalogo_pasa_siempre(string ruta)
    {
        var token = Token("ana", "coop-1");
        await EnviarAsync(HttpMethod.Get, ruta, token);
        await EnviarAsync(HttpMethod.Get, ruta, token);

        _api.Llamadas.Should().Be(2);
    }

    [Fact]
    public async Task Sin_token_o_sin_respuesta_200_no_se_guarda()
    {
        await EnviarAsync(HttpMethod.Get, "/api/core/banks", token: null);
        await EnviarAsync(HttpMethod.Get, "/api/core/banks", token: null);

        _api.Estado = HttpStatusCode.InternalServerError;
        var token = Token("ana", "coop-1");
        var fallida = await EnviarAsync(HttpMethod.Get, "/api/core/branches", token);
        _api.Estado = HttpStatusCode.OK;
        await EnviarAsync(HttpMethod.Get, "/api/core/branches", token);

        fallida.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        _api.Llamadas.Should().Be(4);
    }

    [Fact]
    public async Task Dos_pantallas_a_la_vez_comparten_una_sola_llamada()
    {
        var token = Token("ana", "coop-1");
        _api.Espera = new TaskCompletionSource();

        var a = EnviarAsync(HttpMethod.Get, "/api/core/cost-centers", token);
        var b = EnviarAsync(HttpMethod.Get, "/api/core/cost-centers", token);
        _api.Espera.SetResult();
        var respuestas = await Task.WhenAll(a, b);

        _api.Llamadas.Should().Be(1);
        respuestas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    private sealed class ApiQueCuenta : HttpMessageHandler
    {
        private int _llamadas;
        public int Llamadas => _llamadas;
        public HttpStatusCode Estado { get; set; } = HttpStatusCode.OK;
        public TaskCompletionSource? Espera { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _llamadas);
            if (Espera is not null) await Espera.Task;
            return new HttpResponseMessage(Estado)
            {
                Content = new StringContent($"{{\"llamada\":{n}}}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
