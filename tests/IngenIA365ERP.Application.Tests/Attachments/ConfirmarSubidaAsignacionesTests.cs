using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.ConfirmarSubida;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Storage.Configuration;
using IngenIA365ERP.Storage.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Las pruebas que miden memoria corren solas: <c>GC.GetTotalAllocatedBytes</c> cuenta lo que asignan
/// todos los hilos del proceso, y otra prueba corriendo en paralelo ensuciaría la medición.
/// </summary>
[CollectionDefinition(Nombre, DisableParallelization = true)]
public sealed class MedicionDeMemoriaCollection
{
    public const string Nombre = "Medicion de memoria";
}

/// <summary>
/// Feature 011 (SC-001, research R17, T077): lo único de un archivo que pasa por el servidor son los
/// primeros 8 KiB que revisa la confirmación. Validar un objeto de 1 MB y uno de 25 MB tiene que asignar
/// lo mismo, con tolerancia; si alguien cambia el almacén para leer el objeto entero —o quita el rango de
/// la lectura y el servidor manda todo—, el de 25 MB asigna 24 MB más y esta prueba lo ve.
///
/// <para>
/// Corre el manejador de verdad sobre el <see cref="S3BlobStore"/> de verdad, con un S3 falso que
/// <b>ignora el rango pedido</b> y manda el objeto entero: así lo que se mide es que el almacén lea sólo
/// lo que necesita, no que el falso sea amable.
/// </para>
/// </summary>
[Collection(MedicionDeMemoriaCollection.Nombre)]
public class ConfirmarSubidaAsignacionesTests
{
    private const int UnMega = 1024 * 1024;
    private const long Tolerancia = UnMega;

    private sealed class S3QueMandaTodo
    {
        public Dictionary<string, (byte[] Contenido, string Huella)> Objetos { get; } = [];
        public List<ByteRange?> Rangos { get; } = [];
        public IAmazonS3 Cliente { get; } = Substitute.For<IAmazonS3>();

        public S3QueMandaTodo()
        {
            Cliente.GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var o = Objetos[ci.Arg<GetObjectMetadataRequest>().Key];
                    return Task.FromResult(new GetObjectMetadataResponse { ContentLength = o.Contenido.Length, ChecksumSHA256 = o.Huella });
                });
            Cliente.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var p = ci.Arg<GetObjectRequest>();
                    Rangos.Add(p.ByteRange);
                    // El objeto entero, sin copiarlo: un MemoryStream sobre el arreglo no asigna su tamaño.
                    return Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(Objetos[p.Key].Contenido, writable: false) });
                });
        }
    }

    private static S3BlobStore Almacen(S3QueMandaTodo s3) => new(
        s3.Cliente,
        Options.Create(new AttachmentStorageSettings
        {
            Provider = AttachmentStorageSettings.ProveedorS3,
            S3 = new S3StorageSettings { BucketName = "ingenia365-erp-attachments", Prefix = "dev" },
        }),
        NullLogger<S3BlobStore>.Instance);

    /// <summary>Lo que asigna confirmar un PDF de <paramref name="tamano"/> bytes.</summary>
    private static async Task<long> AsignadoAlConfirmar(EscenarioDeAdjuntos e, S3QueMandaTodo s3, S3BlobStore almacen, int tamano)
    {
        var pdf = EscenarioDeAdjuntos.Pdf(tamano);
        var fila = e.Subiendo(pdf);
        var clave = "dev/" + fila.StoragePath;
        s3.Objetos[clave] = (pdf, EscenarioDeAdjuntos.Huella(pdf));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var antes = GC.GetTotalAllocatedBytes(precise: true);
        EstadoDeAdjunto estado;
        using (var db = e.Base.Db())
        {
            var handler = new ConfirmarSubidaDeAdjuntoCommandHandler(
                db, almacen, e.Permisos, e.Usuario, e.Reloj, NullLogger<ConfirmarSubidaDeAdjuntoCommandHandler>.Instance);
            var r = await handler.Handle(new ConfirmarSubidaDeAdjuntoCommand(fila.PublicId), CancellationToken.None);
            estado = r.Value.Status;
        }
        var asignado = GC.GetTotalAllocatedBytes(precise: true) - antes;

        s3.Objetos.Remove(clave);
        estado.Should().Be(EstadoDeAdjunto.Available, "la medición sólo vale si la confirmación llegó hasta el final");
        return asignado;
    }

    [Fact]
    public async Task Validar_un_objeto_de_1_MB_y_uno_de_25_MB_asigna_lo_mismo()
    {
        var e = new EscenarioDeAdjuntos();
        var s3 = new S3QueMandaTodo();
        var almacen = Almacen(s3);

        // Calentar: JIT, el modelo de EF y las cachés del SDK no son parte de lo que se mide.
        await AsignadoAlConfirmar(e, s3, almacen, 64 * 1024);
        await AsignadoAlConfirmar(e, s3, almacen, 64 * 1024);

        // El mínimo de tres, para que un asignador de fondo no decida la prueba.
        var chico = long.MaxValue;
        var grande = long.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            chico = Math.Min(chico, await AsignadoAlConfirmar(e, s3, almacen, UnMega));
            grande = Math.Min(grande, await AsignadoAlConfirmar(e, s3, almacen, 25 * UnMega));
        }

        (grande - chico).Should().BeLessThan(Tolerancia,
            $"validar 25 MB asignó {grande:N0} bytes y validar 1 MB {chico:N0}: la confirmación no puede depender del tamaño del archivo");
        grande.Should().BeLessThan(5 * UnMega, $"validar asignó {grande:N0} bytes: lee más que los primeros 8 KiB");
        s3.Rangos.Should().OnlyContain(r => r != null && r.Start == 0 && r.End == 8 * 1024 - 1,
            "el almacén pide sólo los primeros 8 KiB; lo demás no tiene por qué cruzar la red");
    }

    /// <summary>
    /// Que el instrumento ve lo que tiene que ver: copiar entero el objeto de 25 MB sí asigna 25 MB. Sin
    /// esto, la prueba de arriba pasaría también si la medición no midiera nada.
    /// </summary>
    [Fact]
    public void La_medicion_ve_un_archivo_de_25_MB_cuando_se_lee_entero()
    {
        var pdf = EscenarioDeAdjuntos.Pdf(25 * UnMega);

        var antes = GC.GetTotalAllocatedBytes(precise: true);
        using var copia = new MemoryStream();
        new MemoryStream(pdf, writable: false).CopyTo(copia);
        var asignado = GC.GetTotalAllocatedBytes(precise: true) - antes;

        asignado.Should().BeGreaterThan(25L * UnMega);
    }
}
