using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Results;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Results;

/// <summary>
/// Feature 015, T1, T0419 (FR-020, FR-117, FR-156): girar una orden deja su comprobante de pago numerado (uno por orden) con código de verificación; el documento enmascara
/// el documento del beneficiario y las cuentas; se envía al beneficiario por correo sólo con la autorización del canal; SMS y mensajería siguen simulados. Prueba escrita,
/// sin correr.
/// </summary>
public class ComprobanteDePagoTests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly IAutorizacionDeDatos _autorizaciones = Substitute.For<IAutorizacionDeDatos>();
    private readonly IEmailSender _correo = Substitute.For<IEmailSender>();
    private readonly IComprobanteDePagoEnPdf _pdf = Substitute.For<IComprobanteDePagoEnPdf>();

    private SendPaymentVoucherCommandHandler Enviar() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _autorizaciones, _pdf, _correo,
        new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj);

    private async Task<Guid> GiradaAsync()
    {
        var orden = _t.Orden(750_000m, PaymentOrderState.Sent);
        var r = await _t.Resultados.RegistrarAsync(orden, new ResultadoARegistrar(DisbursementOutcome.Disbursed, EscenarioDeRecaudo.Hoy, "REF-1", null, null,
            PaymentResponseSource.Manual, Guid.NewGuid()), new QuienRegistra(_t.E.Cajera.Id, Guid.NewGuid(), "Cajera"), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        await _t.E.Db.SaveChangesAsync();
        return (await _t.E.Db.PaymentVouchers.SingleAsync()).PublicId;
    }

    [Fact]
    public async Task Girar_deja_un_comprobante_numerado_con_codigo_y_uno_solo_por_orden()
    {
        await GiradaAsync();

        var v = await _t.E.Db.PaymentVouchers.SingleAsync();
        v.Number.Should().StartWith(ComprobantesDePago.Codigo);
        v.VerificationCode.Should().HaveLength(16);
        v.NetAmount.Should().Be(750_000m);
        v.DisbursementResultId.Should().BePositive();
    }

    [Fact]
    public async Task El_documento_enmascara_el_documento_del_beneficiario()
    {
        var id = await GiradaAsync();
        var modelo = await new GetPaymentVoucherDocumentQueryHandler(_t.E.Db, _t.Alcance).Handle(new GetPaymentVoucherDocumentQuery(id), default);

        modelo.Value.BeneficiaryTaxId.Should().NotBe("2001");
        modelo.Value.AmountInWords.Should().Contain("setecientos cincuenta mil");
    }

    [Fact]
    public async Task Sin_autorizacion_del_canal_no_sale_y_con_ella_sale_por_correo()
    {
        var id = await GiradaAsync();
        var sin = await Enviar().Handle(new SendPaymentVoucherCommand(id, MessageChannel.Email, "berta@ejemplo.co") { OperationKey = Guid.NewGuid() }, default);
        sin.Value.Outcome.Should().Be(ComprobantesDePago.SinAutorizacion);
        await _correo.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());

        _autorizaciones.VigenteAsync(_t.Beneficiaria.PublicId, DataAuthorizationPurpose.ContactChannel, "Email", Arg.Any<CancellationToken>())
            .Returns(new AutorizacionVigente("Accepted", true, Guid.NewGuid(), 1, DateTime.UtcNow.AddYears(-1), "Email"));
        var con = await Enviar().Handle(new SendPaymentVoucherCommand(id, MessageChannel.Email, "berta@ejemplo.co") { OperationKey = Guid.NewGuid() }, default);

        con.Value.Outcome.Should().Be(ComprobantesDePago.Enviado);
        await _correo.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        (await _t.E.Db.PaymentVouchers.SingleAsync()).SendCount.Should().Be(1);
    }

    [Fact]
    public async Task Sms_y_mensajeria_siguen_simulados()
    {
        var id = await GiradaAsync();
        var r = await Enviar().Handle(new SendPaymentVoucherCommand(id, MessageChannel.Sms) { OperationKey = Guid.NewGuid() }, default);
        r.Value.Outcome.Should().Be(ComprobantesDePago.Simulado);
    }
}
