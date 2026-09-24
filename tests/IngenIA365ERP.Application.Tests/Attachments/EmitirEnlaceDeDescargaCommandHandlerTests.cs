using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.EmitirEnlaceDeDescarga;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Feature 011 (US2, T053): el enlace de descarga se firma sólo para lo disponible, vence a los segundos
/// que diga la configuración, lleva el nombre y el tipo del archivo, y queda en la auditoría. Lo del
/// formato anterior se baja por la API.
/// </summary>
public class EmitirEnlaceDeDescargaCommandHandlerTests
{
    private static EmitirEnlaceDeDescargaCommandHandler Handler(EscenarioDeAdjuntos e, Common.TestApplicationDbContext db) =>
        new(db, e.Almacen, e.Permisos, e.Usuario, e.Reloj, e.Limites);

    private static async Task<Result<EnlaceDeDescargaDto>> Emitir(EscenarioDeAdjuntos e, Guid id)
    {
        using var db = e.Base.Db();
        return await Handler(e, db).Handle(new EmitirEnlaceDeDescargaCommand(id), CancellationToken.None);
    }

    [Fact]
    public async Task Un_adjunto_disponible_recibe_un_enlace_directo_con_su_nombre_su_tipo_y_60_segundos()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf(), estado: EstadoDeAdjunto.Available);

        var r = await Emitir(e, a.PublicId);

        r.Value.Direct.Should().BeTrue();
        r.Value.Url.Should().Contain(a.StoragePath);
        r.Value.ExpiresAt.Should().Be(new DateTimeOffset(EscenarioDeAdjuntos.Ahora.AddSeconds(60)));
        await e.Almacen.Received(1).FirmarDescargaAsync(Arg.Is<BlobReference>(b => b.Uri == a.StoragePath),
            new DescargaFirmada("factura.pdf", "application/pdf", new DateTimeOffset(EscenarioDeAdjuntos.Ahora.AddSeconds(60))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_vida_del_enlace_sale_de_la_configuracion()
    {
        var e = new EscenarioDeAdjuntos();
        e.Limites.Value.DescargaSegundos = 15;
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf(), estado: EstadoDeAdjunto.Available);

        (await Emitir(e, a.PublicId)).Value.ExpiresAt.Should().Be(new DateTimeOffset(EscenarioDeAdjuntos.Ahora.AddSeconds(15)));
    }

    [Fact]
    public async Task Uno_del_formato_anterior_se_baja_por_la_API_y_no_se_firma_nada()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf(), estado: EstadoDeAdjunto.Available);
        using (var db = e.Base.Db())
        {
            db.Attachments.Single(x => x.Id == a.Id).Format = FormatoDeAdjunto.AppEncrypted;
            db.SaveChanges();
        }

        var r = await Emitir(e, a.PublicId);

        r.Value.Should().Be(EnlaceDeDescargaDto.PorLaApi);
        await e.Almacen.DidNotReceive().FirmarDescargaAsync(Arg.Any<BlobReference>(), Arg.Any<DescargaFirmada>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EstadoDeAdjunto.Uploading)]
    [InlineData(EstadoDeAdjunto.Rejected)]
    [InlineData(EstadoDeAdjunto.Incomplete)]
    public async Task Lo_que_no_esta_disponible_no_recibe_enlace(EstadoDeAdjunto estado)
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf(), estado: estado);

        var r = await Emitir(e, a.PublicId);

        r.Error.Code.Should().Be(AttachmentErrorCodes.NotAvailable);
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { status = estado });
    }

    [Fact]
    public async Task Sin_permiso_de_consultar_comprobantes_responde_como_si_no_existiera()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf(), estado: EstadoDeAdjunto.Available);
        e.Permisos.HasPermissionAsync("Accounting.Vouchers.View", Arg.Any<CancellationToken>()).Returns(false);

        (await Emitir(e, a.PublicId)).Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task Cada_enlace_queda_en_la_auditoria()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf(), estado: EstadoDeAdjunto.Available);
        var auditoria = Substitute.For<IAuditService>();
        AuditLogCommand? registrado = null;
        auditoria.LogAsync(Arg.Do<AuditLogCommand>(c => registrado = c), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var comando = new EmitirEnlaceDeDescargaCommand(a.PublicId);

        using var db = e.Base.Db();
        await new AuditBehavior<EmitirEnlaceDeDescargaCommand, Result<EnlaceDeDescargaDto>>(
                auditoria, e.Usuario, NullLogger<AuditBehavior<EmitirEnlaceDeDescargaCommand, Result<EnlaceDeDescargaDto>>>.Instance)
            .Handle(comando, _ => Handler(e, db).Handle(comando, CancellationToken.None), CancellationToken.None);

        registrado.Should().NotBeNull("FR-046: quién pidió bajar qué y cuándo");
        registrado!.NewValues.Should().BeSameAs(comando);
    }
}
