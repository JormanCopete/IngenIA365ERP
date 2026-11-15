using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Importadores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Implantacion;

/// <summary>
/// Lo que protege a los usuarios de una carga grande (2026-11-06), sin contenedores: el rechazo del limitador y el tope del libro
/// descomprimido, que se comprueba antes de abrirlo.
/// </summary>
public class LimiteDeCargasTests
{
    [Fact]
    public async Task El_rechazo_es_un_429_con_el_sobre_y_un_reintento_sugerido()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "traza-1" };
        http.Response.Body = new MemoryStream();

        await LimiteDeCargas.ResponderOcupadoAsync(http, CancellationToken.None);

        http.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        http.Response.Headers.RetryAfter.ToString().Should().Be("15");
        http.Response.Body.Position = 0;
        var sobre = await JsonDocument.ParseAsync(http.Response.Body);
        sobre.RootElement.GetProperty("code").GetString().Should().Be("Import.Busy");
        sobre.RootElement.GetProperty("message").GetString().Should().Be(LimiteDeCargas.Mensaje);
        sobre.RootElement.GetProperty("traceId").GetString().Should().Be("traza-1");
    }

    [Fact]
    public void El_rechazo_distingue_la_politica_de_cargas_de_la_de_adjuntos()
    {
        static HttpContext Con(string politica)
        {
            var http = new DefaultHttpContext();
            http.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new EnableRateLimitingAttribute(politica)), "ruta"));
            return http;
        }

        LimiteDeCargas.EsSuya(Con(LimiteDeCargas.Politica)).Should().BeTrue();
        LimiteDeCargas.EsSuya(Con("adjuntos")).Should().BeFalse();
        LimiteDeCargas.EsSuya(new DefaultHttpContext()).Should().BeFalse();
    }

    [Theory]
    [InlineData("0", "4")]
    [InlineData("2", "-1")]
    public void Una_configuracion_imposible_no_arranca(string permisos, string cola)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cargas:Concurrencia:Permisos"] = permisos,
            ["Cargas:Concurrencia:Cola"] = cola,
        }).Build();

        FluentActions.Invoking(() => new ServiceCollection().AddLimiteDeCargas(configuracion))
            .Should().Throw<InvalidOperationException>().WithMessage("*Concurrencia*");
    }

    /// <summary>Un zip pequeño que se abre enorme: los 16 MB del archivo no limitan lo que ClosedXML carga en memoria.</summary>
    private static byte[] LibroQueSeInfla()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entrada = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize).Open();
            var ceros = new byte[1024 * 1024];
            for (var i = 0; i <= ClosedXmlTabularFileReader.MaximoDescomprimido / ceros.Length; i++) entrada.Write(ceros);
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task Un_libro_que_se_infla_al_abrirlo_se_rechaza_sin_cargarlo()
    {
        var lector = new ClosedXmlTabularFileReader();
        var libro = LibroQueSeInfla();
        libro.Length.Should().BeLessThan(2 * 1024 * 1024);

        var hoja = await lector.LeerHojaAsync(libro, "bomba.xlsx", null);
        var hojas = await lector.ListarHojasAsync(libro, "bomba.xlsx");

        hoja.Error.Code.Should().Be("Archivo.Ilegible");
        hoja.Error.Message.Should().Contain("descomprimido");
        hojas.Error.Message.Should().Contain("descomprimido");
    }

    [Fact]
    public async Task Lo_que_no_es_un_zip_sigue_con_su_mensaje_de_siempre()
    {
        var r = await new ClosedXmlTabularFileReader().LeerHojaAsync("esto no es un libro"u8.ToArray(), "x.xlsx", null);

        r.Error.Code.Should().Be("Archivo.Ilegible");
        r.Error.Message.Should().NotContain("descomprimido");
    }
}
