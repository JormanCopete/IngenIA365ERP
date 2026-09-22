using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage;
using IngenIA365ERP.Storage.Configuration;
using IngenIA365ERP.Storage.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Adjuntos en S3 (2026-09-22). No pega contra AWS: usa un <see cref="IAmazonS3"/> falso con un
/// diccionario, que es lo que hace falta para fijar el contrato —qué clave se escribe, qué guarda
/// la base, qué pasa cuando el objeto no está— sin credenciales ni red en el CI. Lo que sólo AWS
/// puede decir (que la credencial tenga permisos) lo dice el health check en el ambiente.
/// </summary>
public class S3BlobStoreTests
{
    private const string Bucket = "ingenia365-erp-attachments";

    /// <summary>Un S3 de mentira: guarda los objetos en memoria por clave y responde 404 como el real.</summary>
    private sealed class S3Falso
    {
        public Dictionary<string, byte[]> Objetos { get; } = [];
        public List<string> Borrados { get; } = [];
        public IAmazonS3 Cliente { get; }

        public S3Falso()
        {
            Cliente = Substitute.For<IAmazonS3>();
            Cliente.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var p = ci.Arg<PutObjectRequest>();
                    using var ms = new MemoryStream();
                    p.InputStream.CopyTo(ms);
                    Objetos[p.Key] = ms.ToArray();
                    return Task.FromResult(new PutObjectResponse());
                });
            Cliente.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var clave = ci.ArgAt<string>(1);
                    if (!Objetos.TryGetValue(clave, out var bytes))
                        throw new AmazonS3Exception("No such key") { StatusCode = HttpStatusCode.NotFound };
                    return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(bytes) });
                });
            Cliente.DeleteObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var clave = ci.ArgAt<string>(1);
                    Objetos.Remove(clave);
                    Borrados.Add(clave);
                    return Task.FromResult(new DeleteObjectResponse());
                });
        }
    }

    private static (S3BlobStore Store, S3Falso S3) Crear(string? prefijo = null)
    {
        var s3 = new S3Falso();
        var opciones = Options.Create(new AttachmentStorageSettings
        {
            Provider = AttachmentStorageSettings.ProveedorS3,
            S3 = new S3StorageSettings { BucketName = Bucket, Prefix = prefijo, ServerSideEncryption = "AES256" },
        });
        return (new S3BlobStore(s3.Cliente, opciones, NullLogger<S3BlobStore>.Instance), s3);
    }

    private static BlobMetadata Metadatos(string tenant = "coop-uno") =>
        new(tenant, "AccountingDocument", Guid.NewGuid(), "soporte de la factura.pdf", "application/pdf", 12, "abc123");

    [Fact]
    public async Task Guarda_bajo_cooperativa_y_anio_mes_y_devuelve_lo_mismo_que_guardo()
    {
        var (store, s3) = Crear();
        var contenido = "bytes-ya-cifrados"u8.ToArray();

        var referencia = await store.PutAsync(new MemoryStream(contenido), Metadatos(), CancellationToken.None);

        referencia.Uri.Should().MatchRegex($@"^coop-uno/{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}/[0-9a-f]{{32}}\.bin$");
        s3.Objetos.Should().ContainSingle().Which.Key.Should().Be(referencia.Uri, "sin prefijo, la clave es la referencia");

        await using var leido = await store.GetAsync(referencia, CancellationToken.None);
        using var ms = new MemoryStream();
        await leido.CopyToAsync(ms);
        ms.ToArray().Should().Equal(contenido);
    }

    [Fact]
    public async Task El_prefijo_del_ambiente_no_entra_en_lo_que_se_guarda_en_la_base()
    {
        var (store, s3) = Crear("pdn");

        var referencia = await store.PutAsync(new MemoryStream([1, 2, 3]), Metadatos(), CancellationToken.None);

        referencia.Uri.Should().StartWith("coop-uno/", "la base guarda la referencia sin prefijo: mover el prefijo no invalida lo escrito");
        s3.Objetos.Keys.Should().ContainSingle().Which.Should().Be($"pdn/{referencia.Uri}");
        // Y se lee de vuelta con el mismo prefijo.
        (await store.GetAsync(referencia, CancellationToken.None)).Length.Should().Be(3);
    }

    [Fact]
    public async Task El_objeto_va_como_octet_stream_cifrado_por_el_servidor_y_con_el_nombre_original_codificado()
    {
        var (store, s3) = Crear();
        PutObjectRequest? enviado = null;
        s3.Cliente.PutObjectAsync(Arg.Do<PutObjectRequest>(p => enviado = p), Arg.Any<CancellationToken>());

        await store.PutAsync(new MemoryStream([9]), Metadatos(), CancellationToken.None);

        enviado.Should().NotBeNull();
        enviado!.BucketName.Should().Be(Bucket);
        enviado.ContentType.Should().Be("application/octet-stream", "lo que se sube son bytes cifrados, no un PDF");
        enviado.ServerSideEncryptionMethod.Should().Be(ServerSideEncryptionMethod.AES256);
        enviado.Metadata["x-amz-meta-nombre"].Should().Be(Uri.EscapeDataString("soporte de la factura.pdf"));
        enviado.Metadata["x-amz-meta-sha256"].Should().Be("abc123");
    }

    [Fact]
    public async Task Un_blob_que_no_esta_es_FileNotFound_y_borrar_lo_que_ya_no_esta_no_falla()
    {
        var (store, _) = Crear();
        var inexistente = new BlobReference("coop-uno/2026/09/deadbeef.bin");

        var acto = () => store.GetAsync(inexistente, CancellationToken.None);

        await acto.Should().ThrowAsync<FileNotFoundException>("es lo que el contrato de IBlobStore promete");
        await store.DeleteAsync(inexistente, CancellationToken.None);
    }

    [Fact]
    public async Task Borrar_quita_el_objeto_y_la_prueba_de_salud_escribe_y_limpia()
    {
        var (store, s3) = Crear("qa");
        var referencia = await store.PutAsync(new MemoryStream([1]), Metadatos(), CancellationToken.None);

        await store.DeleteAsync(referencia, CancellationToken.None);
        s3.Objetos.Should().BeEmpty();

        var donde = await store.ProbarAsync(CancellationToken.None);

        donde.Should().Be($"s3://{Bucket}/qa");
        s3.Objetos.Should().BeEmpty("la prueba de salud no deja basura");
        s3.Borrados.Should().Contain(k => k.StartsWith("qa/.healthcheck/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("coop/2026/09/a.bin", null, "coop/2026/09/a.bin")]
    [InlineData("coop/2026/09/a.bin", "pdn", "pdn/coop/2026/09/a.bin")]
    [InlineData("/coop/a.bin", "/pdn/", "pdn/coop/a.bin")]
    [InlineData("coop\\2026\\a.bin", "", "coop/2026/a.bin")]
    public void La_clave_se_arma_igual_venga_como_venga_la_referencia(string referencia, string? prefijo, string esperada) =>
        S3BlobStore.ClaveDe(referencia, prefijo).Should().Be(esperada);

    [Fact]
    public void Una_referencia_que_intenta_salirse_del_prefijo_se_rechaza() =>
        FluentActions.Invoking(() => S3BlobStore.ClaveDe("coop/../../otro/a.bin", "pdn"))
            .Should().Throw<InvalidOperationException>();

    [Fact]
    public void Sin_bucket_el_arranque_falla_en_vez_de_escribir_en_ninguna_parte()
    {
        var opciones = Options.Create(new AttachmentStorageSettings { Provider = AttachmentStorageSettings.ProveedorS3 });
        FluentActions.Invoking(() => new S3BlobStore(Substitute.For<IAmazonS3>(), opciones, NullLogger<S3BlobStore>.Instance))
            .Should().Throw<InvalidOperationException>().WithMessage("*BucketName*");
    }

    [Theory]
    [InlineData("Local", typeof(LocalEncryptedFileStore))]
    [InlineData("S3", typeof(S3BlobStore))]
    public void El_proveedor_configurado_decide_el_almacen(string proveedor, Type esperado)
    {
        var servicios = Servicios(proveedor);

        servicios.GetRequiredService<IBlobStore>().Should().BeOfType(esperado);
    }

    [Fact]
    public void Un_proveedor_desconocido_tumba_el_arranque_en_vez_de_caer_al_disco_sin_avisar() =>
        FluentActions.Invoking(() => Servicios("Nube"))
            .Should().Throw<InvalidOperationException>().WithMessage("*Provider*");

    private static ServiceProvider Servicios(string proveedor)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AttachmentStorage:Provider"] = proveedor,
            ["AttachmentStorage:LocalRootPath"] = Path.Combine(Path.GetTempPath(), "ingenia-di-" + Guid.NewGuid().ToString("N")),
            ["AttachmentStorage:S3:BucketName"] = Bucket,
            ["AttachmentStorage:S3:Region"] = "us-east-1",
        }).Build();
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddDataProtection();
        servicios.AddStorageServices(configuracion);
        return servicios.BuildServiceProvider();
    }
}
