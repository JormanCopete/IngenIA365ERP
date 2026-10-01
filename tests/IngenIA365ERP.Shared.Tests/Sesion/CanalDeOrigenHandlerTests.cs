using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Sesion;

/// <summary>
/// T36 / T043: cada anfitrión dice por qué canal entró la petición con <c>X-Canal</c> (<c>web</c>
/// o <c>app</c>) para que la auditoría lo registre (<c>IOrigenDeLaPeticion</c>). El handler no
/// toca <c>Authorization</c>: eso es sólo de <c>RenovacionDeSesionHandler</c>
/// (<c>ElTokenDeSesionLoPoneElHandler</c>).
/// </summary>
public class CanalDeOrigenHandlerTests
{
    private sealed class Eco : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Ultima = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static async Task<HttpRequestMessage> EnviarAsync(CanalDeOrigenHandler handler, HttpRequestMessage? peticion = null)
    {
        var eco = new Eco();
        handler.InnerHandler = eco;
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://erp.pruebas") };
        await http.SendAsync(peticion ?? new HttpRequestMessage(HttpMethod.Get, "/api/core/people"));
        return eco.Ultima!;
    }

    [Theory]
    [InlineData(CanalDeOrigenHandler.Web)]
    [InlineData(CanalDeOrigenHandler.App)]
    public async Task Pone_el_canal_del_anfitrion(string canal)
    {
        var enviada = await EnviarAsync(new CanalDeOrigenHandler(canal));

        enviada.Headers.GetValues(CanalDeOrigenHandler.Cabecera).Should().ContainSingle().Which.Should().Be(canal);
    }

    [Fact]
    public async Task Reemplaza_un_canal_puesto_a_mano_y_no_toca_la_autorizacion()
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/documents");
        peticion.Headers.Add(CanalDeOrigenHandler.Cabecera, "pos");

        var enviada = await EnviarAsync(new CanalDeOrigenHandler(CanalDeOrigenHandler.App), peticion);

        enviada.Headers.GetValues(CanalDeOrigenHandler.Cabecera).Should().ContainSingle().Which.Should().Be("app");
        enviada.Headers.Authorization.Should().BeNull();
    }

    [Theory]
    [InlineData("pos")]
    [InlineData("proceso")]
    [InlineData("")]
    public void Solo_admite_web_o_app(string canal)
    {
        FluentActions.Invoking(() => new CanalDeOrigenHandler(canal)).Should().Throw<ArgumentException>();
    }
}
