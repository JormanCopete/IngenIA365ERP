using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Testcontainers.Minio;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Un almacén compatible con S3 de verdad (MinIO) para probar lo que un <see cref="IAmazonS3"/> falso
/// no puede decir: que la política de un POST prefirmado ata tamaño, tipo, clave y huella; que un GET
/// prefirmado vence; y que borrar deja una marca mientras la versión sobrevive (feature 011, R17).
///
/// <para>
/// El bucket nace <b>versionado</b>, como el real: sin eso, la papelera de 90 días no existiría y las
/// pruebas de borrado pasarían por las razones equivocadas. Exige Docker Desktop encendido, como el
/// resto de las pruebas de integración.
/// </para>
/// </summary>
public sealed class MinioFixture : IAsyncLifetime
{
    public const string Bucket = "ingenia365-erp-attachments";

    /// <summary>
    /// De quay.io y no la que trae el paquete por defecto (<c>minio/minio</c>): MinIO dejó de publicar en
    /// Docker Hub y el 2026-09-23 esa imagen ya no se podía bajar («repository does not exist»).
    /// </summary>
    public const string Imagen = "quay.io/minio/minio:latest";

    private readonly MinioContainer _minio = new MinioBuilder(Imagen).Build();

    /// <summary>Cliente administrativo del contenedor: puede listar versiones, a diferencia del ERP.</summary>
    public IAmazonS3 Cliente { get; private set; } = null!;

    /// <summary>La URL base del contenedor, para armar <c>AttachmentStorage:S3:ServiceUrl</c>.</summary>
    public string Endpoint => _minio.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _minio.StartAsync();
        Cliente = CrearCliente();
        await Cliente.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });
        await Cliente.PutBucketVersioningAsync(new PutBucketVersioningRequest
        {
            BucketName = Bucket,
            VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled },
        });
    }

    /// <summary>Un cliente nuevo contra el contenedor, igual al que arma <c>S3BlobStore</c> con <c>ServiceUrl</c>.</summary>
    public IAmazonS3 CrearCliente() => new AmazonS3Client(
        new BasicAWSCredentials(_minio.GetAccessKey(), _minio.GetSecretKey()),
        new AmazonS3Config { ServiceURL = Endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });

    public async Task DisposeAsync()
    {
        Cliente?.Dispose();
        await _minio.DisposeAsync();
    }
}

/// <summary>Un solo contenedor para todas las pruebas que necesitan un almacén real.</summary>
[CollectionDefinition(Nombre)]
public sealed class ColeccionAlmacenMinio : ICollectionFixture<MinioFixture>
{
    public const string Nombre = "Almacén MinIO";
}
