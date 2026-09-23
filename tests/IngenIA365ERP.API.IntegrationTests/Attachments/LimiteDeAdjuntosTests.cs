using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.Endpoints.Attachments;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Feature 011 (R13, contracts/api.md): lo que excede el limitador de adjuntos recibe 429 con el sobre de
/// siempre y el código <c>Attachments.Busy</c>. Sin contenedores: se prueba la respuesta y la
/// configuración, no la saturación.
/// </summary>
public class LimiteDeAdjuntosTests
{
    [Fact]
    public async Task El_rechazo_es_un_429_con_el_sobre_y_un_reintento_sugerido()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "traza-1" };
        http.Response.Body = new MemoryStream();

        await LimiteDeAdjuntos.ResponderOcupadoAsync(http, CancellationToken.None);

        http.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        http.Response.Headers.RetryAfter.ToString().Should().Be("5");
        http.Response.Body.Position = 0;
        var sobre = await JsonDocument.ParseAsync(http.Response.Body);
        sobre.RootElement.GetProperty("code").GetString().Should().Be("Attachments.Busy");
        sobre.RootElement.GetProperty("message").GetString().Should().Be(LimiteDeAdjuntos.Mensaje);
        sobre.RootElement.GetProperty("traceId").GetString().Should().Be("traza-1");
    }

    [Theory]
    [InlineData("0", "64")]
    [InlineData("32", "-1")]
    public void Una_configuracion_imposible_no_arranca(string permisos, string cola)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AttachmentStorage:Concurrencia:Permisos"] = permisos,
            ["AttachmentStorage:Concurrencia:Cola"] = cola,
        }).Build();

        FluentActions.Invoking(() => new ServiceCollection().AddLimiteDeAdjuntos(configuracion))
            .Should().Throw<InvalidOperationException>().WithMessage("*Concurrencia*");
    }
}
