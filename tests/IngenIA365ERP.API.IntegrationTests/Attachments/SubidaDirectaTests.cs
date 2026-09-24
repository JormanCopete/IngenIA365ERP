using System.Net;
using FluentAssertions;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Feature 011 (US1, T038): la subida directa recorrida por HTTP, con el almacén de verdad (MinIO) y
/// <c>Provider = S3</c>. El archivo va del «navegador» al almacén; la API sólo autoriza y confirma.
/// </summary>
[Collection(AdjuntosSobreS3Collection.Nombre)]
public class SubidaDirectaTests(ApiConAlmacenS3Fixture fx)
{
    [Fact]
    public async Task Pedir_subir_al_almacen_y_confirmar_deja_el_soporte_disponible()
    {
        var coop = await AdjuntosE2E.CooperativaAsync(fx, "adjuntoss3");
        using var api = fx.CreateClient();

        var subida = await AdjuntosE2E.SubirAsync(api, coop.Token, coop.Borrador, AdjuntosE2E.Pdf(64 * 1024));

        subida.EstadoDelAlmacen.Should().Be(HttpStatusCode.NoContent, "el archivo fue al almacén, no a la API");
        subida.Confirmacion!.Value.GetProperty("status").GetInt32().Should().Be(2, "Available");
        var lista = await GetAsync(api, coop.Token, $"/api/attachments/by-owner?ownerEntityType=AccountingDocument&ownerEntityPublicId={coop.Borrador}");
        var soporte = lista.EnumerateArray().Single(a => a.GetProperty("publicId").GetGuid() == subida.Adjunto);
        soporte.GetProperty("status").GetInt32().Should().Be(2);
        soporte.GetProperty("format").GetInt32().Should().Be(2, "Direct");
        soporte.GetProperty("fileName").GetString().Should().Be("Factura 1234 – Núñez.pdf");
        soporte.GetProperty("canDelete").GetBoolean().Should().BeTrue("el comprobante sigue en borrador");
    }

    [Fact]
    public async Task Un_ejecutable_renombrado_a_pdf_llega_al_almacen_pero_se_rechaza_al_confirmar()
    {
        var coop = await AdjuntosE2E.CooperativaAsync(fx, "adjuntoss3");
        using var api = fx.CreateClient();
        var exe = new byte[16 * 1024];
        "MZ"u8.CopyTo(exe);

        var subida = await AdjuntosE2E.SubirAsync(api, coop.Token, coop.Borrador, exe, "factura.pdf");

        subida.EstadoDelAlmacen.Should().Be(HttpStatusCode.NoContent, "la firma autoriza tamaño, tipo declarado y huella, no el contenido");
        subida.Confirmacion!.Value.GetProperty("status").GetInt32().Should().Be(3, "Rejected");
        subida.Confirmacion.Value.GetProperty("rejectionReason").GetString().Should().Contain("ejecutable");
        using var enlace = await EnviarAsync(api, coop.Token, HttpMethod.Post, $"/api/attachments/{subida.Adjunto}/download-link", null);
        enlace.StatusCode.Should().Be(HttpStatusCode.Conflict, "el ERP no firma descargas de algo que no aceptó");
    }

    [Fact]
    public async Task No_se_suben_archivos_a_lo_que_genera_un_modulo()
    {
        var coop = await AdjuntosE2E.CooperativaAsync(fx, "adjuntoss3");
        using var api = fx.CreateClient();

        using var r = await AdjuntosE2E.PedirSubidaAsync(api, coop.Token, Guid.NewGuid(), AdjuntosE2E.Pdf(1024), ownerEntityType: "PilaGeneration");

        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LeerAsync(r)).GetProperty("code").GetString().Should().Be("Attachments.OwnerNotAllowed");
    }

    [Fact]
    public async Task El_comprobante_de_otra_cooperativa_no_existe_aqui()
    {
        var mia = await AdjuntosE2E.CooperativaAsync(fx, "adjuntoss3");
        var otra = await AdjuntosE2E.CooperativaAsync(fx, "adjuntoss3otra");
        using var api = fx.CreateClient();

        using var r = await AdjuntosE2E.PedirSubidaAsync(api, otra.Token, mia.Borrador, AdjuntosE2E.Pdf(1024));

        r.StatusCode.Should().Be(HttpStatusCode.NotFound, "una base por cooperativa: el borrador de la otra no está en ésta");
    }

    [Fact]
    public async Task Pasarse_del_tamano_responde_400_con_el_maximo()
    {
        var coop = await AdjuntosE2E.CooperativaAsync(fx, "adjuntoss3");
        using var api = fx.CreateClient();

        using var r = await EnviarAsync(api, coop.Token, HttpMethod.Post, "/api/attachments/uploads", new
        {
            ownerEntityType = "AccountingDocument", ownerEntityPublicId = coop.Borrador, fileName = "enorme.pdf", contentType = "application/pdf",
            sizeBytes = 26_214_401L, sha256Base64 = "n4bQgYhMfWWaL+qgxVrQFaO/TxsrC4Is0V1sFbDwCgg=",
        });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var sobre = await LeerAsync(r);
        sobre.GetProperty("code").GetString().Should().Be("Validation.Attachments.FileTooLarge");
        sobre.GetProperty("data").GetProperty("maxBytes").GetInt64().Should().Be(26_214_400L);
    }
}
