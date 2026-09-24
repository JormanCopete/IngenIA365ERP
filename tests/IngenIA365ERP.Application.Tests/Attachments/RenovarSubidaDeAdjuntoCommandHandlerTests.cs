using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.RenovarSubida;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Core;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Feature 011 (US1, T037): una subida que no llegó a tiempo se reintenta sobre la misma fila y la misma
/// clave. Cualquier otro estado responde 409 <c>Attachments.NotAvailable</c>.
/// </summary>
public class RenovarSubidaDeAdjuntoCommandHandlerTests
{
    private static async Task<Result<AutorizacionDeSubidaDto>> Renovar(EscenarioDeAdjuntos e, Guid id)
    {
        using var db = e.Base.Db();
        return await new RenovarSubidaDeAdjuntoCommandHandler(db, e.Almacen, e.Permisos, e.Usuario, e.Reloj, e.Limites)
            .Handle(new RenovarSubidaDeAdjuntoCommand(id), CancellationToken.None);
    }

    [Fact]
    public async Task Una_incompleta_vuelve_a_subiendo_con_la_misma_clave_y_otro_vencimiento()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf(), estado: EstadoDeAdjunto.Incomplete, venceEn: TimeSpan.FromMinutes(-10));

        var r = await Renovar(e, a.PublicId);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.AttachmentPublicId.Should().Be(a.PublicId, "la misma fila");
        var firmada = e.Firmadas.Should().ContainSingle().Subject;
        firmada.Referencia!.Uri.Should().Be(a.StoragePath, "la misma clave: la huella firmada es la misma");
        firmada.Sha256Base64.Should().Be(AdjuntosDirectos.HexABase64(a.Sha256Hex));
        var fila = e.Fila(a.PublicId);
        fila.Status.Should().Be(EstadoDeAdjunto.Uploading);
        fila.UploadExpiresAt.Should().Be(EscenarioDeAdjuntos.Ahora.AddMinutes(5));
        fila.StoragePath.Should().Be(a.StoragePath);
    }

    [Fact]
    public async Task Una_subiendo_vencida_y_sin_objeto_tambien_se_renueva()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf());
        e.Adelantar(TimeSpan.FromMinutes(6));

        (await Renovar(e, a.PublicId)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Si_el_archivo_ya_llego_no_se_renueva_hay_que_confirmarlo()
    {
        var e = new EscenarioDeAdjuntos();
        var pdf = EscenarioDeAdjuntos.Pdf();
        var a = e.Subiendo(pdf);
        e.EnElAlmacen(a, pdf);
        e.Adelantar(TimeSpan.FromMinutes(6));

        var r = await Renovar(e, a.PublicId);

        r.Error.Code.Should().Be(AttachmentErrorCodes.NotAvailable);
        r.Error.Message.Should().Contain("confirmarlo");
    }

    [Theory]
    [InlineData(EstadoDeAdjunto.Uploading)]
    [InlineData(EstadoDeAdjunto.Available)]
    [InlineData(EstadoDeAdjunto.Rejected)]
    public async Task En_cualquier_otro_estado_responde_409_con_el_estado(EstadoDeAdjunto estado)
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf(), estado: estado);

        var r = await Renovar(e, a.PublicId);

        r.Error.Code.Should().Be(AttachmentErrorCodes.NotAvailable);
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { status = estado });
        e.Firmadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_permiso_de_escribir_comprobantes_responde_como_si_no_existiera()
    {
        var e = new EscenarioDeAdjuntos();
        var a = e.Subiendo(EscenarioDeAdjuntos.Pdf(), estado: EstadoDeAdjunto.Incomplete);
        e.Permisos.HasPermissionAsync("Accounting.Vouchers.Create", Arg.Any<CancellationToken>()).Returns(false);

        (await Renovar(e, a.PublicId)).Error.Code.Should().Be("Generic.NotFound");
    }
}
