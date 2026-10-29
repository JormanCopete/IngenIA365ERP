using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Results;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Results;

/// <summary>
/// Feature 015, US11, T0152 (FR-118, FR-212): la respuesta del banco se lee con <c>FlatFileReader</c> en <c>TreasuryPaymentResponse = 9</c> (el genérico
/// <c>RESP-PAGOS-GENERICO</c>), se guarda tal como llegó antes de su fila y cada línea se coteja contra la huella de la línea del archivo generado (documento, cuenta y
/// valor): la que casa se informa; la que discrepa alerta <c>Tesoreria.DiscrepanciaEnRespuesta</c> y queda sin informar hasta su revisión. Prueba escrita, sin correr.
/// </summary>
public class RespuestaDelBancoTests
{
    private readonly EscenarioDeResultados _r = new();
    private readonly ISender _sender = Substitute.For<ISender>();

    private EscenarioDeTesoreria T => _r.T;

    public RespuestaDelBancoTests() =>
        _sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(Guid.NewGuid()));

    private LoadPaymentResponseCommandHandler Cargar() => new(T.E.Db, T.Alcance, T.E.Actor, T.Cerrojo, T.Segregacion, T.Aprobacion, T.Resultados, T.E.Alertas,
        _r.Auditoria, _sender, T.E.Reloj);

    private ReviewPaymentResponseLineCommandHandler Revisar() => new(T.E.Db, T.Alcance, T.E.Actor, T.Cerrojo, T.Segregacion, T.Aprobacion, T.Resultados,
        _r.Auditoria, T.E.Reloj);

    [Fact]
    public async Task Las_lineas_que_casan_se_informan_y_la_que_discrepa_espera_su_revision()
    {
        var a = T.Orden(500_000m);
        var b = T.Orden(300_000m);
        var c = T.Orden(200_000m);
        var (_, archivo) = _r.LoteEnviado(a, b, c);
        var contenido = EscenarioDeResultados.Respuesta(
            "1;2001;123456789;500000.00;Pagado;;2026-11-21",
            "2;2001;123456789;300000.00;Rechazado;Cuenta inexistente;2026-11-21",
            "3;2001;999999999;200000.00;Pagado;;2026-11-21");

        var r = await Cargar().Handle(new LoadPaymentResponseCommand(archivo.PublicId, "RESPUESTA.csv", contenido) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.LineCount.Should().Be(3);
        r.Value.InformedCount.Should().Be(2);
        r.Value.DiscrepancyCount.Should().Be(1);
        r.Value.Lines.Single(l => l.LineNumber == 3).DiscrepancyDetail.Should().Be("cuenta");
        a.Status.Should().Be(PaymentOrderState.Disbursed);
        b.Status.Should().Be(PaymentOrderState.Rejected);
        c.Status.Should().Be(PaymentOrderState.Sent, "la línea alterada no se informa hasta revisarla (US11-7)");
        T.E.Db.IntegrationMessages.Count(m => m.Type == ResultadoDelGiroV1.Type).Should().Be(2);
        await T.E.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(x => x.TypeCode == TiposDeAlerta.TesoreriaDiscrepanciaEnRespuesta), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<UploadAttachmentCommand>(x => x.OwnerEntityType == "TreasuryPaymentResponse"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revisar_la_linea_la_informa_o_la_descarta()
    {
        var orden = T.Orden(200_000m);
        var (_, archivo) = _r.LoteEnviado(orden);
        var cargada = await Cargar().Handle(new LoadPaymentResponseCommand(archivo.PublicId, "RESPUESTA.csv",
            EscenarioDeResultados.Respuesta("1;2001;123456789;199000.00;Pagado;;2026-11-21")) { OperationKey = Guid.NewGuid() }, default);
        var linea = cargada.Value.Lines.Single();
        linea.Discrepancy.Should().BeTrue();
        linea.DiscrepancyDetail.Should().Be("valor");

        var revisada = await Revisar().Handle(new ReviewPaymentResponseLineCommand(archivo.PublicId, cargada.Value.PublicId, linea.PublicId, true,
            "El banco confirmó por teléfono el valor del archivo") { OperationKey = Guid.NewGuid() }, default);

        revisada.IsSuccess.Should().BeTrue(revisada.IsFailure ? revisada.Error.Message : null);
        revisada.Value.Lines.Single().Informed.Should().BeTrue();
        orden.Status.Should().Be(PaymentOrderState.Disbursed);
        T.E.Db.DisbursementResults.Single().Amount.Should().Be(200_000m, "se informa el neto de la orden, nunca el valor de la línea alterada");

        (await Revisar().Handle(new ReviewPaymentResponseLineCommand(archivo.PublicId, cargada.Value.PublicId, linea.PublicId, true, "otra vez")
            { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be("Treasury.Response.AlreadyReviewed");
    }

    [Fact]
    public async Task Una_respuesta_de_un_archivo_no_enviado_no_se_carga()
    {
        var orden = T.Orden();
        var (_, archivo) = _r.LoteEnviado(orden);
        T.E.Db.Entry(archivo).Property(x => x.SentAt).CurrentValue = null;
        T.E.Db.SaveChanges();

        var r = await Cargar().Handle(new LoadPaymentResponseCommand(archivo.PublicId, "R.csv", EscenarioDeResultados.Respuesta("1;2001;123456789;1000000.00;Pagado;;2026-11-21"))
            { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Treasury.File.NotSent");
    }

    [Fact]
    public async Task Un_resultado_que_no_es_pagado_ni_rechazado_no_se_carga()
    {
        var orden = T.Orden();
        var (_, archivo) = _r.LoteEnviado(orden);

        var r = await Cargar().Handle(new LoadPaymentResponseCommand(archivo.PublicId, "R.csv", EscenarioDeResultados.Respuesta("1;2001;123456789;1000000.00;Quizas;;2026-11-21"))
            { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Treasury.Response.UnknownOutcome");
        T.E.Db.PaymentResponses.Should().BeEmpty();
    }

    [Fact]
    public void El_cotejo_usa_la_huella_de_la_linea()
    {
        var linea = new Domain.Entities.Treasury.Payments.PaymentFileLine
        {
            BeneficiaryTaxId = "2001", BankCode = "1007", AccountNumber = "123", Amount = 10m,
            LineSha256 = Application.Treasury.PaymentFiles.LineasDelArchivo.HuellaDeLinea("2001", "1007", "123", 10m),
        };
        LoadPaymentResponseCommandHandler.Discrepa(linea, new() { BeneficiaryTaxId = "2001", AccountNumber = "123", Amount = 10m }).Should().BeNull();
        LoadPaymentResponseCommandHandler.Discrepa(linea, new() { BeneficiaryTaxId = "2002", AccountNumber = "123", Amount = 10m }).Should().Be("documento");
        LoadPaymentResponseCommandHandler.Discrepa(null, new() { BeneficiaryTaxId = "2001" }).Should().Be("sin línea en el archivo generado");
    }
}
