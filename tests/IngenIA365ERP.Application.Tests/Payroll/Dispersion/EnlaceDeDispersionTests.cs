using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Payroll.Dispersion;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Dispersion;

/// <summary>
/// Feature 011 (US4, T061): el archivo de dispersión se baja con un enlace firmado con el nombre, el tipo
/// y la codificación del formato del banco —lo mismo que servía <c>GET /{id}/file</c>—. Uno del formato
/// anterior responde <c>direct: false</c>.
/// </summary>
public class EnlaceDeDispersionTests
{
    private sealed class Escenario
    {
        public DispersionDePrueba P { get; } = new();
        public IBlobStore Almacen { get; } = Substitute.For<IBlobStore>();

        public Escenario() =>
            Almacen.FirmarDescargaAsync(Arg.Any<BlobReference>(), Arg.Any<DescargaFirmada>(), Arg.Any<CancellationToken>())
                .Returns(ci => new EnlaceDeDescarga("https://almacen.test/dispersion?firma", ci.Arg<DescargaFirmada>().VenceEn));

        public EmitirEnlaceDeDispersionCommandHandler Handler() =>
            new(P.D.Db, Almacen, P.D.Clock, Options.Create(new LimitesDeAdjuntos()));

        public async Task<Domain.Entities.Payroll.Transactions.BankDisbursementFile> GenerarAsync(FormatoDeAdjunto formato = FormatoDeAdjunto.Direct)
        {
            var r = await P.Generador().Handle(P.ComandoDePrima(), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            var archivo = await P.D.Db.BankDisbursementFiles.Include(a => a.Format).SingleAsync();
            P.D.Db.Attachments.Add(new Attachment
            {
                PublicId = archivo.FileAttachmentPublicId!.Value, TenantId = 9, OwnerEntityType = "BankDisbursementFile", OwnerEntityPublicId = archivo.PublicId,
                FileName = archivo.FileName, ContentType = "text/plain", SizeBytes = 100, Sha256Hex = archivo.FileSha256,
                StoragePath = "9/2026/12/dispersion.bin", EncryptedDek = string.Empty, Format = formato, Status = EstadoDeAdjunto.Available,
            });
            await P.D.Db.SaveChangesAsync();
            return archivo;
        }
    }

    [Fact]
    public async Task El_enlace_sale_con_el_nombre_el_tipo_y_la_codificacion_del_formato()
    {
        var e = new Escenario();
        var archivo = await e.GenerarAsync();

        var r = await e.Handler().Handle(new EmitirEnlaceDeDispersionCommand(archivo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Direct.Should().BeTrue();
        var tipo = EmitirEnlaceDeDispersionCommandHandler.TipoConCodificacion(archivo.Format!.ContentType, archivo.Format.Encoding);
        tipo.Should().Contain("charset=");
        await e.Almacen.Received(1).FirmarDescargaAsync(Arg.Is<BlobReference>(b => b.Uri == "9/2026/12/dispersion.bin"),
            Arg.Is<DescargaFirmada>(d => d.NombreDeArchivo == "DEMO20261215.txt" && d.ContentType == tipo),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Uno_del_formato_anterior_se_baja_por_la_API()
    {
        var e = new Escenario();
        var archivo = await e.GenerarAsync(FormatoDeAdjunto.AppEncrypted);

        (await e.Handler().Handle(new EmitirEnlaceDeDispersionCommand(archivo.PublicId), CancellationToken.None)).Value
            .Should().Be(EnlaceDeDescargaDto.PorLaApi);
    }

    [Fact]
    public async Task Un_archivo_que_no_existe_responde_como_siempre()
    {
        var e = new Escenario();

        (await e.Handler().Handle(new EmitirEnlaceDeDispersionCommand(Guid.NewGuid()), CancellationToken.None)).Error.Code
            .Should().Be("Payroll.Disbursement.FileNotFound");
    }

    [Theory]
    [InlineData("text/plain", "iso-8859-1", "text/plain; charset=iso-8859-1")]
    [InlineData("text/csv; charset=utf-8", "us-ascii", "text/csv; charset=utf-8")]
    public void El_tipo_lleva_la_codificacion_salvo_que_el_formato_ya_la_diga(string tipo, string codificacion, string esperado) =>
        EmitirEnlaceDeDispersionCommandHandler.TipoConCodificacion(tipo, codificacion).Should().Be(esperado);
}
