using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage.Configuration;
using IngenIA365ERP.Storage.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// Feature 011: el host de siempre (<see cref="CentralIdentityApiFixture"/>) pero con los adjuntos en un
/// almacén compatible con S3 de verdad —MinIO— y <c>Provider = S3</c>, como en los ambientes. Sirve para
/// recorrer por HTTP la subida y la descarga directas, con la política firmada cumpliéndose de verdad.
///
/// <para>
/// El almacén se reemplaza por uno que usa el cliente de MinIO: el de producción resuelve las
/// credenciales por la cadena estándar del SDK, que aquí no existe. Sin cifrado del lado del servidor,
/// porque MinIO exige un KMS para aceptarlo.
/// </para>
/// </summary>
public sealed class ApiConAlmacenS3Fixture : CentralIdentityApiFixture
{
    public MinioFixture Minio { get; } = new();

    protected override Task AntesDeArrancarAsync() => Minio.InitializeAsync();

    protected override void ConfigurarHost(IWebHostBuilder builder)
    {
        builder.UseSetting("AttachmentStorage:Provider", AttachmentStorageSettings.ProveedorS3);
        builder.UseSetting("AttachmentStorage:S3:BucketName", MinioFixture.Bucket);
        builder.UseSetting("AttachmentStorage:S3:Prefix", "e2e");
        builder.UseSetting("AttachmentStorage:S3:ServerSideEncryption", string.Empty);
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IBlobStore>(sp =>
            new S3BlobStore(Minio.CrearCliente(), sp.GetRequiredService<IOptions<AttachmentStorageSettings>>(),
                sp.GetRequiredService<ILogger<S3BlobStore>>(), propio: true))));
    }

    protected override Task DespuesDeCerrarAsync() => Minio.DisposeAsync();
}

/// <summary>Un host con los adjuntos en MinIO para las pruebas de subida y descarga directas.</summary>
[CollectionDefinition(Nombre)]
public sealed class AdjuntosSobreS3Collection : ICollectionFixture<ApiConAlmacenS3Fixture>
{
    public const string Nombre = "Adjuntos sobre S3";
}
