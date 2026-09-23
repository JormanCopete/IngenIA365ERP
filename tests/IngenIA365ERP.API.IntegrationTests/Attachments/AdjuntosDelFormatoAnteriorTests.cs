using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Feature 011 (FR-040, contracts/api.md §6): los adjuntos que ya existen —cifrados por la aplicación,
/// en DEV y QA— se siguen leyendo a través de la API, sin migrarlos. Se siembran por debajo con
/// <see cref="SembradorDeAdjuntosAnteriores"/> porque ninguna ruta los produce ya; todo lo demás pasa
/// por HTTP con el token del administrador de la cooperativa contable.
/// </summary>
[Collection(ContabilidadCollection.Nombre)]
public class AdjuntosDelFormatoAnteriorTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_soporte_de_un_comprobante_se_baja_descifrado_y_con_su_huella()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var contenido = "%PDF-1.7\nFactura 1234 de la Ferretería Núñez"u8.ToArray();
        var soporte = await SembradorDeAdjuntosAnteriores.SembrarAsync(
            fx, ctx.TenantPublicId, "AccountingDocument", ctx.ComprobantePrincipal, contenido, "Factura 1234 – Núñez.pdf");

        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/api/attachments/{soporte.PublicId}");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.TokenAdmin);
        using var respuesta = await http.SendAsync(peticion);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        (await respuesta.Content.ReadAsByteArrayAsync()).Should().Equal(contenido);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        respuesta.Headers.GetValues("X-Attachment-Sha256").Single().Should().Be(soporte.Sha256Hex);
    }

    [Fact]
    public async Task La_lista_de_soportes_del_comprobante_lo_trae()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var soporte = await SembradorDeAdjuntosAnteriores.SembrarAsync(
            fx, ctx.TenantPublicId, "AccountingDocument", ctx.ComprobanteNorte, "%PDF-1.4 recibo"u8.ToArray(), "recibo.pdf");

        var lista = await GetAsync(http, ctx.TokenAdmin,
            $"/api/attachments/by-owner?ownerEntityType=AccountingDocument&ownerEntityPublicId={ctx.ComprobanteNorte}");

        lista.EnumerateArray().Select(a => a.GetProperty("publicId").GetGuid()).Should().Contain(soporte.PublicId);
    }

    [Fact]
    public async Task Un_adjunto_de_otra_cooperativa_no_existe_aqui()
    {
        var ctx = await PrepararAsync(fx);
        var otra = await CooperativaAisladaAsync(fx, "adjuntos", 2026);
        using var http = fx.CreateClient();
        var ajeno = await SembradorDeAdjuntosAnteriores.SembrarAsync(
            fx, otra.TenantPublicId, "AccountingDocument", Guid.NewGuid(), "%PDF-1.4 ajeno"u8.ToArray());

        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/api/attachments/{ajeno.PublicId}");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.TokenAdmin);
        using var respuesta = await http.SendAsync(peticion);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound, "una base por cooperativa: el de la otra no está en ésta");
    }
}
