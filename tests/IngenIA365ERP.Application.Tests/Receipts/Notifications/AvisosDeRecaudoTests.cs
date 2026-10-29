using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Receipts.Notifications;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Receipts;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Notifications;

/// <summary>
/// Feature 015, R1, T0361 (FR-020, FR-045, FR-177): el recibo y el egreso salen por correo sólo con la autorización del canal (<c>IAutorizacionDeDatos</c>, finalidad
/// <c>ContactChannel</c>, canal <c>Email</c>); sin autorización o sin correo no sale nada y se dice por qué; el cuerpo no lleva saldos ni datos de otros titulares (el
/// detalle va en el PDF, que ya enmascara para el pagador tercero); SMS y mensajería siguen simulados. Prueba escrita, sin correr.
/// </summary>
public sealed class AvisosDeRecaudoTests
{
    private readonly IAutorizacionDeDatos _autorizaciones = Substitute.For<IAutorizacionDeDatos>();
    private readonly IEmailSender _correo = Substitute.For<IEmailSender>();
    private readonly Guid _persona = Guid.NewGuid();

    private void Autoriza(bool si) =>
        _autorizaciones.VigenteAsync(_persona, DataAuthorizationPurpose.ContactChannel, AvisosDeRecaudo.CanalDeCorreo, Arg.Any<CancellationToken>())
            .Returns(new AutorizacionVigente(si ? "Accepted" : "Declined", si, Guid.NewGuid(), 1, DateTime.UtcNow, null));

    private Task<AvisoDeRecaudoDto> EnviarAsync(ReceiptNoticeChannel canal = ReceiptNoticeChannel.Email, string? correo = "ana@coop.test") =>
        AvisosDeRecaudo.EnviarAsync(_autorizaciones, _correo, _persona, correo, canal, null, "Recibo RC-1",
            AvisosDeRecaudo.Cuerpo("Coop", "recibo de caja", "RC-1", new DateOnly(2026, 12, 1), 80_000m, "ABC123"),
            () => new EmailAttachment("recibo.pdf", "application/pdf", [1, 2, 3]), default);

    [Fact]
    public async Task Con_la_autorizacion_del_canal_sale_por_correo_con_el_pdf()
    {
        Autoriza(true);

        var r = await EnviarAsync();

        r.Outcome.Should().Be(ResultadosDeAviso.Enviado);
        r.Destination.Should().NotBe("ana@coop.test", "el destino se devuelve enmascarado");
        await _correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == "ana@coop.test" && m.Attachments!.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_autorizacion_o_sin_correo_no_sale_nada()
    {
        Autoriza(false);
        (await EnviarAsync()).Outcome.Should().Be(ResultadosDeAviso.SinAutorizacion);

        Autoriza(true);
        (await EnviarAsync(correo: null)).Outcome.Should().Be(ResultadosDeAviso.SinDestino);
        await _correo.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SMS_y_mensajeria_siguen_simulados()
    {
        Autoriza(true);
        (await EnviarAsync(ReceiptNoticeChannel.Sms)).Outcome.Should().Be(ResultadosDeAviso.Simulado);
        (await EnviarAsync(ReceiptNoticeChannel.WhatsApp)).Outcome.Should().Be(ResultadosDeAviso.Simulado);
        await _correo.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void El_cuerpo_no_lleva_saldos_ni_otros_titulares()
    {
        var cuerpo = AvisosDeRecaudo.Cuerpo("Coop", "recibo de caja", "RC-1", new DateOnly(2026, 12, 1), 80_000m, "ABC123");
        cuerpo.Should().Contain("RC-1").And.Contain("ABC123").And.NotContainAny("saldo", "mora", "Saldo");
    }
}
