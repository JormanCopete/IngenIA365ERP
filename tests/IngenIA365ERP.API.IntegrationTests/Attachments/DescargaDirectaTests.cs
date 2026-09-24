using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Feature 011 (US2, T054): la descarga directa recorrida por HTTP sobre MinIO. El enlace baja el archivo
/// idéntico, con su nombre con tildes; lo del formato anterior se sigue bajando por la API, y lo directo
/// ya no.
///
/// <para>
/// Que el enlace vence lo prueba <see cref="AlmacenPrefirmadoTests"/> contra el mismo MinIO con 1 s, y
/// que el comando lo firma a los segundos de la configuración, <c>EmitirEnlaceDeDescargaCommandHandlerTests</c>:
/// aquí no se puede bajar a 2 s porque <c>DescargaSegundos</c> admite de 10 a 300 (FR-021) y el host no
/// arranca con menos.
/// </para>
/// </summary>
[Collection(AdjuntosSobreS3Collection.Nombre)]
public class DescargaDirectaTests(ApiConAlmacenS3Fixture fx)
{
    [Fact]
    public async Task El_enlace_baja_el_archivo_identico_con_su_nombre()
    {
        var coop = await AdjuntosE2E.CooperativaAsync(fx, "adjuntoss3");
        using var api = fx.CreateClient();
        var contenido = AdjuntosE2E.Pdf(48 * 1024);
        var subida = await AdjuntosE2E.SubirAsync(api, coop.Token, coop.Borrador, contenido, "Recibo n.º 7 – Peñalosa.pdf");

        using var pedido = await EnviarAsync(api, coop.Token, HttpMethod.Post, $"/api/attachments/{subida.Adjunto}/download-link", null);
        pedido.StatusCode.Should().Be(HttpStatusCode.OK, await pedido.Content.ReadAsStringAsync());
        var enlace = await LeerAsync(pedido);
        enlace.GetProperty("direct").GetBoolean().Should().BeTrue();

        using var descarga = await AdjuntosE2E.Navegador.GetAsync(enlace.GetProperty("url").GetString());
        descarga.StatusCode.Should().Be(HttpStatusCode.OK);
        (await descarga.Content.ReadAsByteArrayAsync()).Should().Equal(contenido);
        descarga.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("Recibo n.º 7 – Peñalosa.pdf");
        descarga.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task Lo_directo_no_se_baja_a_traves_de_la_API()
    {
        var coop = await AdjuntosE2E.CooperativaAsync(fx, "adjuntoss3");
        using var api = fx.CreateClient();
        var subida = await AdjuntosE2E.SubirAsync(api, coop.Token, coop.Borrador, AdjuntosE2E.Pdf(2048));

        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/api/attachments/{subida.Adjunto}");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coop.Token);
        using var r = await api.SendAsync(peticion);

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LeerAsync(r)).GetProperty("code").GetString().Should().Be("Attachments.UseDownloadLink");
    }

    [Fact]
    public async Task Lo_del_formato_anterior_se_sigue_bajando_por_la_API()
    {
        var coop = await AdjuntosE2E.CooperativaAsync(fx, "adjuntoss3");
        using var api = fx.CreateClient();
        var contenido = "%PDF-1.4 soporte de antes"u8.ToArray();
        var anterior = await SembradorDeAdjuntosAnteriores.SembrarAsync(fx, coop.TenantPublicId, "AccountingDocument", coop.Borrador, contenido);

        using var pedido = await EnviarAsync(api, coop.Token, HttpMethod.Post, $"/api/attachments/{anterior.PublicId}/download-link", null);
        (await LeerAsync(pedido)).GetProperty("direct").GetBoolean().Should().BeFalse("hay que descifrarlo: se baja por la API");

        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/api/attachments/{anterior.PublicId}");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coop.Token);
        using var r = await api.SendAsync(peticion);
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await r.Content.ReadAsByteArrayAsync()).Should().Equal(contenido);
    }
}
