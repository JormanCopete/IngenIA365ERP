using FluentAssertions;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Agreements;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Convenios;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Agreements;

/// <summary>
/// Feature 021, US6 (T114; research R4; FR-050, FR-051): el archivo de la pasarela reconoce los pagos del canal por su referencia. Lo que ya se aplicó en línea
/// sale aplicado con su recibo; un aprobado que nunca se avisó se aplica una vez, a nombre del analista que carga, con los conceptos que dice el canal; el
/// orden entre aviso y archivo no importa porque los dos usan la misma llave del recibo.
/// </summary>
public sealed class ConciliacionDelCanalTests
{
    private readonly ConveniosDePrueba _p = new();
    private readonly CollectionAgreement _pasarela;
    private readonly ChannelPaymentDestination _destino;

    public ConciliacionDelCanalTests()
    {
        var e = _p.B.K.C.E;
        var formato = new BankFileFormat();
        BankFileFormatDefinition.Parse("""
            { "code": "PASARELA-PRUEBA", "name": "Reporte de la pasarela de prueba", "scope": "CollectionAgreementFile", "validFrom": "2026-01-01",
              "kind": "Delimited", "delimiter": ";", "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF", "uppercase": false, "stripAccents": false,
              "amountFormat": "Point2", "fileName": "PASARELA.csv", "contentType": "text/csv",
              "records": {
                "header": { "enabled": false, "fields": [] },
                "detail": { "enabled": true, "fields": [
                  { "order": 1, "name": "Fecha", "source": "MovementDate", "format": "yyyy-MM-dd", "required": true },
                  { "order": 2, "name": "Valor", "source": "Amount", "required": true },
                  { "order": 3, "name": "Referencia", "source": "MovementReference" },
                  { "order": 4, "name": "Secuencia", "source": "BankSequence" },
                  { "order": 5, "name": "Estado", "source": "Outcome" } ] },
                "trailer": { "enabled": false, "fields": [] } } }
            """)!.AplicarA(formato, bankId: null);
        e.Db.BankFileFormats.Add(formato);
        e.Db.SaveChanges();
        _pasarela = new CollectionAgreement
        {
            Code = "AVAL", Name = "Pasarela", Kind = CollectionAgreementKind.Gateway, OwnBankAccountPublicId = _p.B.Cuenta.PublicId, BranchId = e.OficinaA.Id,
        };
        e.Db.CollectionAgreements.Add(_pasarela);
        e.Db.SaveChanges();
        e.Db.CollectionAgreementVersions.Add(new CollectionAgreementVersion
        {
            CollectionAgreementId = _pasarela.Id, ValidFrom = EscenarioDeRecaudo.Hoy.AddDays(-30), ReferenceMode = PaymentReferenceMode.ByProduct,
            ReferenceLength = 0, CheckDigit = CheckDigitAlgorithm.Modulo10, BankFileFormatId = formato.Id,
        });
        _destino = new ChannelPaymentDestination { Name = "Avalpay", CollectionAgreementId = _pasarela.Id };
        e.Db.ChannelPaymentDestinations.Add(_destino);
        e.Db.SaveChanges();
        _p.Canal.Configurado.Returns(true);
    }

    private static string Referencia(int secuencia)
    {
        var cuerpo = $"COO261201{secuencia.ToString("X8", System.Globalization.CultureInfo.InvariantCulture)}";
        return cuerpo + ReferenciaDelCanal.DigitoDeControl(cuerpo);
    }

    private ItemDelAvisoDto Cuota() => new(ClaveDeConcepto.DeCredito(_p.B.K.CreditoA), "Due", 332_500m, null);

    private static readonly AprobacionDeLaPasarelaDto Aprobacion = new(733112, 1598634446, "3324451", "1597317690", "_PSE_", "PSE", "Banco de pruebas");

    private Task<Result<CollectionFileLoadDto>> Cargar(string referencia, decimal valor = 332_500m, string secuencia = "S1") =>
        _p.Cargar().Handle(new LoadCollectionFileCommand(_pasarela.PublicId, $"{secuencia}.csv",
            ConveniosDePrueba.Archivo($"{EscenarioDeRecaudo.Hoy:yyyy-MM-dd};{valor:0.00};{referencia};{secuencia};APROBADA")) { OperationKey = Guid.NewGuid() }, default);

    private RegisterChannelPaymentCommand Aviso(string referencia) =>
        new(_p.B.K.Asociado.PublicId, referencia, _destino.PublicId, 332_500m, "COP", new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.FromHours(-5)), Aprobacion,
            [Cuota()], null) { OperationKey = RegisterChannelPaymentCommand.ClaveDeOperacion(referencia) };

    [Fact]
    public void La_referencia_del_canal_tiene_su_digito()
    {
        var r = Referencia(10);
        ReferenciaDelCanal.Es(r).Should().BeTrue();
        ReferenciaDelCanal.Es(r, "COO").Should().BeTrue();
        ReferenciaDelCanal.Es(r, "ABC").Should().BeFalse();
        ReferenciaDelCanal.Es(r[..^1] + (r[^1] == '0' ? '1' : '0')).Should().BeFalse();
        ReferenciaDelCanal.Es(ConveniosDePrueba.Referencia(ConveniosDePrueba.NumeroA)).Should().BeFalse();
    }

    [Fact]
    public async Task Lo_ya_aplicado_en_linea_sale_aplicado_con_su_recibo_sin_aplicar_otra_vez()
    {
        var referencia = Referencia(1);
        var enLinea = await new RegisterChannelPaymentCommandHandler(_p.B.K.C.E.Db, _p.AplicacionDelCanal).Handle(Aviso(referencia), default);
        enLinea.Value.Outcome.Should().Be("Applied");

        var r = await Cargar(referencia);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.File!.Applied.Should().Be(1);
        var linea = await _p.B.K.C.E.Db.GatewayFileLines.SingleAsync();
        (await _p.B.K.C.E.Db.IssuedReceipts.SingleAsync(x => x.Id == linea.ReceiptId)).PublicId.Should().Be(enLinea.Value.ReceiptPublicId!.Value);
        (await _p.B.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(1);
        await _p.B.K.Obligaciones.Received(1).AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>());
        await _p.Canal.DidNotReceive().BuscarIntentosAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_aprobado_sin_aviso_se_aplica_una_vez_a_nombre_del_analista_y_el_aviso_tardio_no_duplica()
    {
        var referencia = Referencia(2);
        _p.Canal.BuscarIntentosAsync(_p.CooperativaPublicId, null, null, referencia, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<IntentoDelCanalDto>>([new IntentoDelCanalDto(referencia, _p.B.K.Asociado.PublicId, "approved", 332_500m,
                DateTime.UtcNow, DateTime.UtcNow, "3324451", "PSE", "failed_retrying", null, _destino.PublicId, [Cuota()], Aprobacion,
                new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.FromHours(-5)), Guid.NewGuid())]));

        var r = await Cargar(referencia);

        r.Value.File!.Applied.Should().Be(1);
        var recibo = await _p.B.K.C.E.Db.IssuedReceipts.SingleAsync();
        recibo.Channel.Should().Be(ReceiptChannel.Online);
        recibo.CashierUserId.Should().Be(_p.B.K.C.E.Cajera.Id, "lo aplica quien carga el archivo (FR-174 sin excepción)");
        var aviso = await _p.B.K.C.E.Db.ChannelPaymentNotices.SingleAsync();
        aviso.ReceiptId.Should().Be(recibo.Id);
        aviso.OutcomeReason.Should().Contain("conciliación");

        // El aviso del canal llega después: la misma respuesta, sin otro recibo.
        var tardio = await new RegisterChannelPaymentCommandHandler(_p.B.K.C.E.Db, _p.AplicacionDelCanal).Handle(Aviso(referencia), default);
        tardio.Value.ReceiptPublicId.Should().Be(recibo.PublicId);
        (await _p.B.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(1);
        await _p.B.K.Obligaciones.Received(1).AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_referencia_del_canal_sin_pago_que_cuadre_queda_como_partida()
    {
        var referencia = Referencia(3);
        _p.Canal.BuscarIntentosAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<IntentoDelCanalDto>>([]));

        var r = await Cargar(referencia, 100_000m);

        r.Value.File!.Unidentified.Should().Be(1);
        (await _p.B.K.C.E.Db.GatewayFileLines.SingleAsync()).Code.Should().Be(MotivosDeLineaDeConvenio.CanalSinAviso);
        (await _p.B.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_pago_que_la_pasarela_reverso_no_se_aplica_y_queda_como_partida()
    {
        // Avisado y reversado por la pasarela (sin anulación con saldo a favor).
        var avisado = Referencia(5);
        await new RegisterChannelPaymentCommandHandler(_p.B.K.C.E.Db, _p.AplicacionDelCanal).Handle(Aviso(avisado), default);
        var aviso = await _p.B.K.C.E.Db.ChannelPaymentNotices.SingleAsync();
        aviso.ReversedAt = DateTime.UtcNow;
        await _p.B.K.C.E.Db.SaveChangesAsync();
        (await Cargar(avisado)).Value.File!.Unidentified.Should().Be(1);

        // Nunca avisado, pero el canal dice que la pasarela lo reversó.
        var sinAviso = Referencia(6);
        _p.Canal.BuscarIntentosAsync(_p.CooperativaPublicId, null, null, sinAviso, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<IntentoDelCanalDto>>([new IntentoDelCanalDto(sinAviso, _p.B.K.Asociado.PublicId, "approved", 332_500m,
                DateTime.UtcNow, DateTime.UtcNow, "3324451", "PSE", "not_sent", null, _destino.PublicId, [Cuota()], Aprobacion, null, Guid.NewGuid(),
                DateTime.UtcNow, "gateway")]));
        (await Cargar(sinAviso, secuencia: "S2")).Value.File!.Unidentified.Should().Be(1);

        (await _p.B.K.C.E.Db.GatewayFileLines.Select(l => l.Code).ToListAsync()).Should().AllBe(MotivosDeLineaDeConvenio.CanalReversado);
        (await _p.B.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(1, "sólo el del aviso original");
    }

    [Fact]
    public async Task La_misma_referencia_en_otro_archivo_no_se_cuenta_dos_veces()
    {
        var referencia = Referencia(4);
        await new RegisterChannelPaymentCommandHandler(_p.B.K.C.E.Db, _p.AplicacionDelCanal).Handle(Aviso(referencia), default);
        (await Cargar(referencia, secuencia: "S1")).Value.File!.Applied.Should().Be(1);

        var otra = await Cargar(referencia, secuencia: "S9");

        otra.Value.File!.Applied.Should().Be(0);
        otra.Value.File.Omitted.Should().Be(1);
    }
}
