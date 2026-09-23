using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Amazon;
using Amazon.Runtime;
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
///
/// <para>
/// Feature 011: la forma de las autorizaciones firmadas se prueba con un <see cref="AmazonS3Client"/>
/// <b>real</b> y credenciales ficticias: firmar es un cálculo local, no toca la red, y así lo que se
/// afirma es la política que de verdad saldría hacia S3, no lo que el código le pidió a un sustituto.
/// Que S3 la haga cumplir lo prueba <see cref="AlmacenPrefirmadoTests"/> contra MinIO.
/// </para>
/// </summary>
public class S3BlobStoreTests
{
    private const string Bucket = "ingenia365-erp-attachments";

    /// <summary>Un S3 de mentira: guarda los objetos en memoria por clave y responde 404 como el real.</summary>
    private sealed class S3Falso
    {
        public Dictionary<string, byte[]> Objetos { get; } = [];
        public Dictionary<string, string> Huellas { get; } = [];
        public List<string> Borrados { get; } = [];
        public List<GetObjectMetadataRequest> Cabeceras { get; } = [];
        public IAmazonS3 Cliente { get; }

        /// <param name="sinListar">
        /// Como la credencial del ERP (research R4): sin <c>s3:ListBucket</c>, S3 responde 403, no 404,
        /// ante una clave que no existe.
        /// </param>
        public S3Falso(bool sinListar = false)
        {
            var noEsta = sinListar ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound;
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
                        throw new AmazonS3Exception("No such key") { StatusCode = noEsta };
                    return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(bytes) });
                });
            Cliente.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var p = ci.Arg<GetObjectRequest>();
                    if (!Objetos.TryGetValue(p.Key, out var bytes))
                        throw new AmazonS3Exception("No such key") { StatusCode = noEsta };
                    var desde = (int)(p.ByteRange?.Start ?? 0);
                    var hasta = (int)Math.Min(p.ByteRange?.End ?? bytes.Length - 1, bytes.Length - 1);
                    return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(bytes[desde..(hasta + 1)]) });
                });
            Cliente.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var p = ci.Arg<GetObjectMetadataRequest>();
                    Cabeceras.Add(p);
                    if (!Objetos.TryGetValue(p.Key, out var bytes))
                        throw new AmazonS3Exception("Not Found") { StatusCode = noEsta };
                    var respuesta = new GetObjectMetadataResponse { ContentLength = bytes.Length };
                    if (p.ChecksumMode == ChecksumMode.ENABLED && Huellas.TryGetValue(p.Key, out var huella))
                        respuesta.ChecksumSHA256 = huella;
                    return Task.FromResult(respuesta);
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

    private static (S3BlobStore Store, S3Falso S3) Crear(string? prefijo = null, bool sinListar = false)
    {
        var s3 = new S3Falso(sinListar);
        return (new S3BlobStore(s3.Cliente, Opciones(prefijo), NullLogger<S3BlobStore>.Instance), s3);
    }

    private static IOptions<AttachmentStorageSettings> Opciones(string? prefijo) => Options.Create(new AttachmentStorageSettings
    {
        Provider = AttachmentStorageSettings.ProveedorS3,
        S3 = new S3StorageSettings { BucketName = Bucket, Prefix = prefijo, ServerSideEncryption = "AES256" },
    });

    /// <summary>Un cliente del SDK de verdad, con credenciales que no existen: sirve para firmar, no para hablar con S3.</summary>
    private static S3BlobStore ConFirmaReal(string? prefijo = "pdn") =>
        new(new AmazonS3Client(new BasicAWSCredentials("clave-de-prueba", "secreto-de-prueba"), RegionEndpoint.USEast1),
            Opciones(prefijo), NullLogger<S3BlobStore>.Instance, propio: true);

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

    // ------------------------------------------------------------------ feature 011 --

    [Fact]
    public async Task Sin_permiso_de_listar_el_403_de_una_clave_que_no_esta_es_no_esta()
    {
        var (store, _) = Crear(sinListar: true);
        var inexistente = new BlobReference("coop-uno/2026/09/deadbeef.bin");

        await FluentActions.Invoking(() => store.GetAsync(inexistente, CancellationToken.None))
            .Should().ThrowAsync<FileNotFoundException>("research R4: sin s3:ListBucket, S3 responde 403 y no 404");
        await FluentActions.Invoking(() => store.LeerInicioAsync(inexistente, 8, CancellationToken.None))
            .Should().ThrowAsync<FileNotFoundException>();
        (await store.ConsultarAsync(inexistente, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Consultar_trae_el_tamano_y_la_huella_que_S3_verifico()
    {
        var (store, s3) = Crear("qa");
        var referencia = new BlobReference("coop-uno/2026/09/abc.bin");
        s3.Objetos["qa/coop-uno/2026/09/abc.bin"] = new byte[1234];
        s3.Huellas["qa/coop-uno/2026/09/abc.bin"] = "n4bQgYhMfWWaL+qgxVrQFaO/TxsrC4Is0V1sFbDwCgg=";

        var estado = await store.ConsultarAsync(referencia, CancellationToken.None);

        estado.Should().Be(new EstadoDelObjeto(1234, "n4bQgYhMfWWaL+qgxVrQFaO/TxsrC4Is0V1sFbDwCgg="));
        s3.Cabeceras.Should().ContainSingle().Which.ChecksumMode.Should().Be(ChecksumMode.ENABLED,
            "sin pedirla, S3 no devuelve la huella y confirmar no podría compararla");
    }

    [Fact]
    public async Task Leer_el_inicio_trae_solo_los_primeros_bytes_y_menos_si_el_archivo_es_corto()
    {
        var (store, s3) = Crear();
        var contenido = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        s3.Objetos["coop-uno/2026/09/largo.bin"] = contenido;
        s3.Objetos["coop-uno/2026/09/corto.bin"] = [1, 2, 3];

        (await store.LeerInicioAsync(new BlobReference("coop-uno/2026/09/largo.bin"), 8, CancellationToken.None))
            .Should().Equal(contenido[..8], "la firma del tipo se valida con los primeros bytes, sin bajar el archivo");
        (await store.LeerInicioAsync(new BlobReference("coop-uno/2026/09/corto.bin"), 8, CancellationToken.None))
            .Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task La_autorizacion_de_subida_fija_clave_tamano_tipo_y_huella_en_la_politica()
    {
        using var store = ConFirmaReal();
        var metadatos = new BlobMetadata("3", "AccountingDocument", Guid.NewGuid(), "Factura 1234 – Núñez.pdf", "application/pdf", 20_481_234, "9f86d081");
        var vence = DateTimeOffset.UtcNow.AddMinutes(5);

        var autorizacion = await store.FirmarSubidaAsync(new SolicitudDeSubida(metadatos, "n4bQgYhMfWWaL+qgxVrQFaO/TxsrC4Is0V1sFbDwCgg=", vence), CancellationToken.None);

        autorizacion.Referencia.Uri.Should().MatchRegex(@"^3/\d{4}/\d{2}/[0-9a-f]{32}\.bin$", "la base guarda la referencia sin el prefijo");
        autorizacion.CampoDelArchivo.Should().Be("file");
        autorizacion.Url.Should().Contain(Bucket);
        var campos = autorizacion.Campos.ToDictionary(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);
        campos["key"].Should().Be($"pdn/{autorizacion.Referencia.Uri}");

        var politica = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(campos["policy"]))).RootElement;
        politica.GetProperty("expiration").GetDateTimeOffset().Should().BeCloseTo(vence, TimeSpan.FromSeconds(1));
        var condiciones = politica.GetProperty("conditions").EnumerateArray().ToList();
        condiciones.Should().Contain(c => c.ValueKind == JsonValueKind.Array
            && c[0].GetString() == "content-length-range" && c[1].GetInt64() == 20_481_234 && c[2].GetInt64() == 20_481_234,
            "un byte de más o de menos y S3 rechaza la subida");
        Exige(condiciones, "key", $"pdn/{autorizacion.Referencia.Uri}");
        Exige(condiciones, "bucket", Bucket);
        Exige(condiciones, "Content-Type", "application/pdf");
        Exige(condiciones, "x-amz-checksum-sha256", "n4bQgYhMfWWaL+qgxVrQFaO/TxsrC4Is0V1sFbDwCgg=");
        Exige(condiciones, "x-amz-checksum-algorithm", "SHA256");
        Exige(condiciones, "x-amz-server-side-encryption", "AES256");
    }

    [Fact]
    public async Task El_enlace_de_descarga_fuerza_guardar_con_el_nombre_original_y_vence()
    {
        using var store = ConFirmaReal();
        var vence = DateTimeOffset.UtcNow.AddSeconds(60);

        var enlace = await store.FirmarDescargaAsync(new BlobReference("3/2026/09/abc.bin"),
            new DescargaFirmada("Factura 1234 – Núñez.pdf", "application/pdf", vence), CancellationToken.None);

        var url = new Uri(enlace.Url);
        url.AbsolutePath.Should().EndWith("/pdn/3/2026/09/abc.bin");
        var consulta = HttpUtility.ParseQueryString(url.Query);
        consulta["response-content-disposition"].Should().Be(S3BlobStore.ComoAdjunto("Factura 1234 – Núñez.pdf"));
        consulta["response-content-type"].Should().Be("application/pdf");
        consulta["response-cache-control"].Should().Be("no-store");
        int.Parse(consulta["X-Amz-Expires"]!).Should().BeInRange(58, 60);
        enlace.VenceEn.Should().Be(vence);
    }

    [Fact]
    public void El_nombre_viaja_en_UTF8_y_con_una_version_ASCII_sin_comillas_que_rompan_la_cabecera()
    {
        S3BlobStore.ComoAdjunto("Factura 1234 – Núñez.pdf")
            .Should().Be("attachment; filename=\"Factura 1234 _ N__ez.pdf\"; filename*=UTF-8''Factura%201234%20%E2%80%93%20N%C3%BA%C3%B1ez.pdf");
        S3BlobStore.ComoAdjunto("el \"informe\" (final)'s.pdf")
            .Should().StartWith("attachment; filename=\"el _informe_ (final)'s.pdf\"; filename*=UTF-8''")
            .And.EndWith("%28final%29%27s.pdf", "en RFC 5987 el apóstrofo separa el idioma del valor: el del nombre va codificado");
    }

    /// <summary>
    /// Una condición exacta de la política, en cualquiera de las dos formas que admite S3:
    /// <c>{"campo": "valor"}</c> o <c>["eq", "$campo", "valor"]</c>.
    /// </summary>
    private static void Exige(List<JsonElement> condiciones, string campo, string valor) =>
        condiciones.Should().Contain(c =>
                (c.ValueKind == JsonValueKind.Object && c.EnumerateObject().Any(p =>
                    string.Equals(p.Name, campo, StringComparison.OrdinalIgnoreCase) && p.Value.GetString() == valor))
                || (c.ValueKind == JsonValueKind.Array && c.GetArrayLength() == 3 && c[0].GetString() == "eq"
                    && string.Equals(c[1].GetString(), "$" + campo, StringComparison.OrdinalIgnoreCase) && c[2].GetString() == valor),
            $"la política tiene que fijar {campo} = {valor}");

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
