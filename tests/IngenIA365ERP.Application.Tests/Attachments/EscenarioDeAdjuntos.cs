using System.Security.Cryptography;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Feature 011: lo que comparten las pruebas de los comandos de subida y descarga directas. Un almacén
/// sustituto que firma y responde lo que cada prueba le diga, un reloj que se puede adelantar y un
/// comprobante en borrador al que se le suben soportes.
/// </summary>
internal sealed class EscenarioDeAdjuntos
{
    public static readonly DateTime Ahora = new(2026, 9, 23, 15, 0, 0, DateTimeKind.Utc);

    public BaseQueFallaAlGuardar Base { get; } = new();
    public IBlobStore Almacen { get; } = Substitute.For<IBlobStore>();
    public IPermissionChecker Permisos { get; } = Substitute.For<IPermissionChecker>();
    public ICurrentUserService Usuario { get; } = Substitute.For<ICurrentUserService>();
    public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();
    public IOptions<LimitesDeAdjuntos> Limites { get; } = Options.Create(new LimitesDeAdjuntos());
    public List<SolicitudDeSubida> Firmadas { get; } = [];
    public Guid Borrador { get; }

    public EscenarioDeAdjuntos()
    {
        Usuario.TenantId.Returns("1");
        Usuario.UserName.Returns("auxiliar@demo");
        Permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Reloj.UtcNow.Returns(Ahora);
        Almacen.FirmarSubidaAsync(Arg.Any<SolicitudDeSubida>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeSubida>();
            Firmadas.Add(s);
            var clave = s.Referencia?.Uri ?? $"1/2026/09/{Guid.NewGuid():N}.bin";
            return new AutorizacionDeSubida(new BlobReference(clave), "https://almacen.test/bucket",
                [new("key", "dev/" + clave), new("Content-Type", s.Metadata.ContentType), new("x-amz-checksum-sha256", s.Sha256Base64)],
                "file", s.VenceEn);
        });
        Almacen.FirmarDescargaAsync(Arg.Any<BlobReference>(), Arg.Any<DescargaFirmada>(), Arg.Any<CancellationToken>())
            .Returns(ci => new EnlaceDeDescarga($"https://almacen.test/bucket/{ci.Arg<BlobReference>().Uri}?firma", ci.Arg<DescargaFirmada>().VenceEn));

        using var db = Base.Db();
        var doc = new AccountingDocument
        {
            VoucherTypeId = 1, Date = new DateOnly(2026, 9, 1), Description = "Compra de papelería",
            OriginModule = "Accounting", RegisteredBy = "auxiliar@demo", Status = DocumentStatus.Draft,
        };
        db.AccountingDocuments.Add(doc);
        db.SaveChanges();
        Borrador = doc.PublicId;
    }

    public void Adelantar(TimeSpan cuanto) => Reloj.UtcNow.Returns(Ahora + cuanto);

    public static byte[] Pdf(int tamano = 4096)
    {
        var contenido = new byte[tamano];
        "%PDF-1.7\n"u8.CopyTo(contenido);
        return contenido;
    }

    public static string Huella(byte[] contenido) => Convert.ToBase64String(SHA256.HashData(contenido));

    /// <summary>Una fila como la deja pedir la subida: <c>Uploading</c>, directo, con su vencimiento.</summary>
    public Attachment Subiendo(byte[] contenido, string tipo = "application/pdf", TimeSpan? venceEn = null, EstadoDeAdjunto estado = EstadoDeAdjunto.Uploading)
    {
        using var db = Base.Db();
        var a = new Attachment
        {
            TenantId = 1, OwnerEntityType = AdjuntosDeModulo.Comprobante, OwnerEntityPublicId = Borrador,
            FileName = "factura.pdf", ContentType = tipo, SizeBytes = contenido.Length,
            Sha256Hex = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant(),
            StoragePath = $"1/2026/09/{Guid.NewGuid():N}.bin", EncryptedDek = string.Empty,
            Format = FormatoDeAdjunto.Direct, Status = estado,
            UploadExpiresAt = Ahora + (venceEn ?? TimeSpan.FromMinutes(5)), CreatedBy = "auxiliar@demo",
        };
        db.Attachments.Add(a);
        db.SaveChanges();
        return a;
    }

    /// <summary>Que el almacén tenga el objeto: tamaño, huella y principio.</summary>
    public void EnElAlmacen(Attachment a, byte[] contenido, string? huella = null)
    {
        Almacen.ConsultarAsync(Arg.Is<BlobReference>(b => b.Uri == a.StoragePath), Arg.Any<CancellationToken>())
            .Returns(new EstadoDelObjeto(contenido.Length, huella ?? Huella(contenido)));
        Almacen.LeerInicioAsync(Arg.Is<BlobReference>(b => b.Uri == a.StoragePath), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => contenido.Take(ci.ArgAt<int>(1)).ToArray());
    }

    public Attachment Fila(Guid publicId)
    {
        using var db = Base.Db();
        return db.Attachments.Single(a => a.PublicId == publicId);
    }
}
