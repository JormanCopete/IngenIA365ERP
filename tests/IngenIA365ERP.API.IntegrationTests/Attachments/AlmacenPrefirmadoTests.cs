using System.Net;
using System.Security.Cryptography;
using Amazon.S3.Model;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage.Configuration;
using IngenIA365ERP.Storage.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Feature 011 (T021): lo que un <c>IAmazonS3</c> falso no puede decir, contra un almacén compatible con
/// S3 de verdad. Que la autorización de subida sirve para <b>ese</b> archivo y ningún otro, que el enlace
/// de descarga trae el nombre con tildes y vence, y que borrar deja una marca mientras la versión
/// sobrevive —la papelera de 90 días—.
///
/// <para>
/// Sin cifrado del lado del servidor: MinIO exige un KMS configurado para aceptar SSE-S3, y lo que aquí se
/// prueba es la política, no el cifrado en reposo (ése lo fija el bucket real, research R3).
/// </para>
/// </summary>
[Collection(ColeccionAlmacenMinio.Nombre)]
public class AlmacenPrefirmadoTests(MinioFixture minio)
{
    private static readonly HttpClient Navegador = new();

    private S3BlobStore Almacen() => new(minio.CrearCliente(),
        Options.Create(new AttachmentStorageSettings
        {
            Provider = AttachmentStorageSettings.ProveedorS3,
            S3 = new S3StorageSettings { BucketName = MinioFixture.Bucket, Prefix = "dev", ServerSideEncryption = "" },
        }),
        NullLogger<S3BlobStore>.Instance, propio: true);

    private static string Sha256Base64(byte[] contenido) => Convert.ToBase64String(SHA256.HashData(contenido));

    private static async Task<AutorizacionDeSubida> AutorizarAsync(S3BlobStore almacen, byte[] contenido, string tipo = "application/pdf") =>
        await almacen.FirmarSubidaAsync(new SolicitudDeSubida(
            new BlobMetadata("3", "AccountingDocument", Guid.NewGuid(), "Factura 1234 – Núñez.pdf", tipo, contenido.LongLength,
                Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant()),
            Sha256Base64(contenido), DateTimeOffset.UtcNow.AddMinutes(5)), CancellationToken.None);

    /// <summary>
    /// Lo que hace el navegador (research R14): todos los campos en el orden recibido y el archivo al
    /// final. <paramref name="cambiar"/> permite alterar un campo, como lo haría quien intenta reusar la
    /// autorización para otra cosa.
    /// </summary>
    private static async Task<HttpResponseMessage> SubirAsync(AutorizacionDeSubida a, byte[] archivo, Action<Dictionary<string, string>>? cambiar = null)
    {
        var campos = a.Campos.ToDictionary(c => c.Key, c => c.Value);
        cambiar?.Invoke(campos);
        using var formulario = new MultipartFormDataContent();
        foreach (var (nombre, valor) in campos)
            formulario.Add(new StringContent(valor), nombre);
        formulario.Add(new ByteArrayContent(archivo), a.CampoDelArchivo, "archivo.pdf");
        return await Navegador.PostAsync(a.Url, formulario);
    }

    private static byte[] Pdf(int tamano)
    {
        var contenido = RandomNumberGenerator.GetBytes(tamano);
        "%PDF-1.7\n"u8.CopyTo(contenido);
        return contenido;
    }

    [Fact]
    public async Task Con_la_autorizacion_el_archivo_llega_y_confirmar_lo_ve_sin_bajarlo()
    {
        using var almacen = Almacen();
        var contenido = Pdf(4096);
        var autorizacion = await AutorizarAsync(almacen, contenido);

        using var respuesta = await SubirAsync(autorizacion, contenido);

        respuesta.StatusCode.Should().Be(HttpStatusCode.NoContent, await respuesta.Content.ReadAsStringAsync());
        (await almacen.ConsultarAsync(autorizacion.Referencia, CancellationToken.None))!.Tamano.Should().Be(4096);
        (await almacen.LeerInicioAsync(autorizacion.Referencia, 8, CancellationToken.None)).Should().Equal("%PDF-1.7"u8.ToArray());
    }

    [Fact]
    public async Task Un_byte_de_mas_no_entra()
    {
        using var almacen = Almacen();
        var autorizado = Pdf(1000);
        var autorizacion = await AutorizarAsync(almacen, autorizado);

        using var respuesta = await SubirAsync(autorizacion, [.. autorizado, 0x00]);

        respuesta.IsSuccessStatusCode.Should().BeFalse("la política fija el tamaño exacto (content-length-range [n, n])");
        (await almacen.ConsultarAsync(autorizacion.Referencia, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Declarar_otro_tipo_no_entra()
    {
        using var almacen = Almacen();
        var contenido = Pdf(1000);
        var autorizacion = await AutorizarAsync(almacen, contenido);

        using var respuesta = await SubirAsync(autorizacion, contenido, c => c["Content-Type"] = "text/html");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden, "un PDF autorizado no se puede servir después como página");
        (await almacen.ConsultarAsync(autorizacion.Referencia, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task La_autorizacion_no_sirve_para_otra_clave()
    {
        using var almacen = Almacen();
        var contenido = Pdf(1000);
        var autorizacion = await AutorizarAsync(almacen, contenido);
        var otra = $"dev/3/2026/09/{Guid.NewGuid():N}.bin";

        using var respuesta = await SubirAsync(autorizacion, contenido, c => c["key"] = otra);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden, "con la clave fija, nadie escribe donde el servidor no lo decidió");
        (await almacen.ConsultarAsync(new BlobReference(otra["dev/".Length..]), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task El_enlace_de_descarga_trae_el_nombre_con_tildes_y_no_se_guarda_en_cache()
    {
        using var almacen = Almacen();
        var contenido = Pdf(2048);
        var autorizacion = await AutorizarAsync(almacen, contenido);
        (await SubirAsync(autorizacion, contenido)).EnsureSuccessStatusCode();

        var enlace = await almacen.FirmarDescargaAsync(autorizacion.Referencia,
            new DescargaFirmada("Factura 1234 – Núñez.pdf", "application/pdf", DateTimeOffset.UtcNow.AddSeconds(60)), CancellationToken.None);
        using var respuesta = await Navegador.GetAsync(enlace.Url);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await respuesta.Content.ReadAsByteArrayAsync()).Should().Equal(contenido);
        respuesta.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment", "el navegador lo guarda, no lo abre en la página");
        respuesta.Content.Headers.ContentDisposition.FileNameStar.Should().Be("Factura 1234 – Núñez.pdf");
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        respuesta.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task El_enlace_de_descarga_vence()
    {
        using var almacen = Almacen();
        var contenido = Pdf(512);
        var autorizacion = await AutorizarAsync(almacen, contenido);
        (await SubirAsync(autorizacion, contenido)).EnsureSuccessStatusCode();

        var enlace = await almacen.FirmarDescargaAsync(autorizacion.Referencia,
            new DescargaFirmada("recibo.pdf", "application/pdf", DateTimeOffset.UtcNow.AddSeconds(1)), CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        using var respuesta = await Navegador.GetAsync(enlace.Url);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden, "un enlace vencido no sirve aunque alguien lo haya copiado");
    }

    [Fact]
    public async Task Borrar_deja_una_marca_y_la_version_sobrevive_en_la_papelera()
    {
        using var almacen = Almacen();
        var contenido = Pdf(256);
        var autorizacion = await AutorizarAsync(almacen, contenido);
        (await SubirAsync(autorizacion, contenido)).EnsureSuccessStatusCode();
        var clave = $"dev/{autorizacion.Referencia.Uri}";

        await almacen.DeleteAsync(autorizacion.Referencia, CancellationToken.None);

        await FluentActions.Invoking(() => almacen.GetAsync(autorizacion.Referencia, CancellationToken.None))
            .Should().ThrowAsync<FileNotFoundException>("para el ERP el archivo ya no está");
        var versiones = await minio.Cliente.ListVersionsAsync(new ListVersionsRequest { BucketName = MinioFixture.Bucket, Prefix = clave });
        versiones.Versions.Should().Contain(v => v.Key == clave && v.IsDeleteMarker == true && v.IsLatest == true, "borrar deja una marca");
        var sobreviviente = versiones.Versions.Should().ContainSingle(v => v.Key == clave && v.IsDeleteMarker != true).Subject;

        using var recuperada = await minio.Cliente.GetObjectAsync(new GetObjectRequest
        {
            BucketName = MinioFixture.Bucket, Key = clave, VersionId = sobreviviente.VersionId,
        });
        using var copia = new MemoryStream();
        await recuperada.ResponseStream.CopyToAsync(copia);
        copia.ToArray().Should().Equal(contenido, "desde la papelera se recupera tal cual (research R17)");
    }
}
