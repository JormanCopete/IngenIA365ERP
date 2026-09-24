using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Payroll.Pila;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Payroll.Pila;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Pila;

/// <summary>
/// Feature 011 (US4, T061): la planilla se baja con un enlace firmado que conserva lo que exigía la
/// descarga de siempre —reconocer el descuadre (FR-027)— y el <c>charset</c> del layout, que el operador
/// exige. Una planilla del formato anterior responde <c>direct: false</c>.
/// </summary>
public class EnlaceDePilaTests
{
    private sealed class Escenario
    {
        public PilaDePrueba F { get; } = new();
        public IBlobStore Almacen { get; } = Substitute.For<IBlobStore>();

        public Escenario() =>
            Almacen.FirmarDescargaAsync(Arg.Any<BlobReference>(), Arg.Any<DescargaFirmada>(), Arg.Any<CancellationToken>())
                .Returns(ci => new EnlaceDeDescarga("https://almacen.test/pila?firma", ci.Arg<DescargaFirmada>().VenceEn));

        public EmitirEnlaceDePilaCommandHandler Handler() =>
            new(F.D.Db, Almacen, F.D.Clock, Options.Create(new LimitesDeAdjuntos()));

        /// <summary>Genera la planilla y deja su adjunto como lo guardaría UploadAttachmentCommand.</summary>
        public async Task<Domain.Entities.Payroll.Transactions.PilaGeneration> GenerarAsync(FormatoDeAdjunto formato = FormatoDeAdjunto.Direct, bool descuadrar = false)
        {
            if (descuadrar)
            {
                var arl = await F.D.Db.PayrollRunLines.SingleAsync(l => l.ConceptCode == "ARL" && l.Amount == 10_500m);
                arl.Amount = 10_000m;
                await F.D.Db.SaveChangesAsync();
            }
            var r = await F.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(r.Error.Message);
            var g = await F.D.Db.PilaGenerations.SingleAsync(x => x.PublicId == r.Value.GenerationPublicId);
            F.D.Db.Attachments.Add(new Attachment
            {
                PublicId = g.FileAttachmentPublicId!.Value, TenantId = 9, OwnerEntityType = "PilaGeneration", OwnerEntityPublicId = g.PublicId,
                FileName = g.FileName!, ContentType = "text/plain", SizeBytes = 100, Sha256Hex = g.FileSha256!,
                StoragePath = "9/2026/12/pila.bin", EncryptedDek = string.Empty, Format = formato, Status = EstadoDeAdjunto.Available,
            });
            await F.D.Db.SaveChangesAsync();
            return g;
        }
    }

    [Fact]
    public async Task El_enlace_lleva_el_nombre_de_la_planilla_y_el_charset_del_layout()
    {
        var e = new Escenario();
        var g = await e.GenerarAsync();

        var r = await e.Handler().Handle(new EmitirEnlaceDePilaCommand(g.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Direct.Should().BeTrue();
        var codificacion = PilaLayoutCatalog.ByCode(g.LayoutVersion)!.Encoding;
        await e.Almacen.Received(1).FirmarDescargaAsync(Arg.Is<BlobReference>(b => b.Uri == "9/2026/12/pila.bin"),
            Arg.Is<DescargaFirmada>(d => d.NombreDeArchivo == g.FileName && d.ContentType == $"text/plain; charset={codificacion}"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_descuadre_sigue_exigiendo_reconocerlo()
    {
        var e = new Escenario();
        var g = await e.GenerarAsync(descuadrar: true);

        var sinReconocer = await e.Handler().Handle(new EmitirEnlaceDePilaCommand(g.PublicId), CancellationToken.None);
        var reconocido = await e.Handler().Handle(new EmitirEnlaceDePilaCommand(g.PublicId, AcknowledgeDifference: true), CancellationToken.None);

        sinReconocer.Error.Code.Should().Be("Payroll.Pila.Unreconciled");
        reconocido.Value.Direct.Should().BeTrue();
    }

    [Fact]
    public async Task Una_planilla_del_formato_anterior_se_baja_por_la_API()
    {
        var e = new Escenario();
        var g = await e.GenerarAsync(FormatoDeAdjunto.AppEncrypted);

        var r = await e.Handler().Handle(new EmitirEnlaceDePilaCommand(g.PublicId), CancellationToken.None);

        r.Value.Should().Be(EnlaceDeDescargaDto.PorLaApi);
        await e.Almacen.DidNotReceive().FirmarDescargaAsync(Arg.Any<BlobReference>(), Arg.Any<DescargaFirmada>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_la_huella_guardada_no_es_la_de_la_generacion_no_se_firma()
    {
        var e = new Escenario();
        var g = await e.GenerarAsync();
        var adjunto = await e.F.D.Db.Attachments.SingleAsync(a => a.PublicId == g.FileAttachmentPublicId);
        adjunto.Sha256Hex = new string('0', 64);
        await e.F.D.Db.SaveChangesAsync();

        var r = await e.Handler().Handle(new EmitirEnlaceDePilaCommand(g.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Pila.FileTampered");
    }
}
