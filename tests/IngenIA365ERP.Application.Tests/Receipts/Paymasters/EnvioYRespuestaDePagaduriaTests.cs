using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Paymasters;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Receipts.Paymasters;

/// <summary>
/// Feature 015, US7, T0202 (FR-076, FR-077, FR-080, FR-201): confirmar el envío emite <c>EnvioDeDescuentosConfirmado</c> v1 con canal, radicado y huella (repetir no
/// cambia nada); la respuesta capturada casa por crédito (v1) o por empleado y código (v2) y la fila sin equivalencia queda «no aplicada»; devolverla emite
/// <c>ResultadoDeDescuentos</c> v1 o v2 según el archivo, y sin la versión adoptada responde <c>Receipts.Paymaster.VersionNotAdopted</c>; la evaluación de Cartera deja
/// la respuesta aceptada (y el resultado en las líneas) o rechazada con su código, que se corrige con otra versión. Prueba escrita, sin correr.
/// </summary>
public class EnvioYRespuestaDePagaduriaTests
{
    private readonly PagaduriasDePrueba _p = new();

    private async Task<PayrollDeductionFile> EnviadoAsync(object contenido, int version)
    {
        var f = await _p.RecibirAsync(contenido, version);
        var r = await _p.ConfirmarEnvio().Handle(new ConfirmPayrollFileSentCommand(f.PublicId, PayrollFileChannel.Email, null, "RAD-1") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        return f;
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<PaymasterResponseDto>> CapturarAsync(PayrollDeductionFile f, params FilaDeRespuestaCapturada[] filas) =>
        _p.CargarRespuesta().Handle(new LoadPaymasterResponseCommand(f.PublicId, Captured: filas) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task Confirmar_el_envio_emite_el_mensaje_con_la_huella_y_repetir_no_cambia_nada()
    {
        var f = await EnviadoAsync(_p.V1(2), 1);

        var mensaje = await _p.B.K.C.E.Db.IntegrationMessages.SingleAsync(m => m.Type == EnvioDeDescuentosConfirmadoV1.Type);
        mensaje.PayloadJson.Should().Contain("abc123").And.Contain("RAD-1");
        (await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFiles.SingleAsync(x => x.Id == f.Id)).Status.Should().Be(PayrollDeductionFileState.Sent);

        var otra = await _p.ConfirmarEnvio().Handle(new ConfirmPayrollFileSentCommand(f.PublicId, PayrollFileChannel.Portal) { OperationKey = Guid.NewGuid() }, default);
        otra.IsSuccess.Should().BeTrue();
        (await _p.B.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == EnvioDeDescuentosConfirmadoV1.Type)).Should().Be(1);
        (await _p.B.K.C.E.Db.PayrollFileSendings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task La_respuesta_se_carga_sobre_un_archivo_enviado()
    {
        var f = await _p.RecibirAsync(_p.V1(1), 1);

        var r = await CapturarAsync(f, new FilaDeRespuestaCapturada(100_000m, "CR-A"));

        r.Error.Code.Should().Be(ErroresDeRecaudo.ArchivoSinEnviar().Code);
    }

    [Fact]
    public async Task La_fila_sin_equivalencia_queda_no_aplicada_y_la_linea_no_informada_tambien()
    {
        var f = await EnviadoAsync(_p.V1(2), 1);

        var r = await CapturarAsync(f, new FilaDeRespuestaCapturada(100_000m, "CR-A"), new FilaDeRespuestaCapturada(40_000m, "CR-ZZ"));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(PaymasterResponseState.Previewed);
        r.Value.Unmatched.Should().Be(1);
        r.Value.Lines.Should().Contain(l => l.ProductNumber == "CR-ZZ" && l.Unmatched && l.Outcome == PaymasterDeductionOutcome.NotApplied);
        r.Value.Lines.Should().Contain(l => l.ProductNumber == "CR-A" && l.Outcome == PaymasterDeductionOutcome.Deducted);
        r.Value.Lines.Should().Contain(l => l.ProductNumber == "CR-B" && l.Deducted == 0m && l.Outcome == PaymasterDeductionOutcome.NotApplied);
    }

    [Fact]
    public async Task La_vista_previa_agrupa_por_codigo_de_la_pagaduria_en_la_v2()
    {
        var f = await EnviadoAsync(_p.V2(), 2);
        var r = await CapturarAsync(f, new FilaDeRespuestaCapturada(100_000m, null, "E-1", "120"), new FilaDeRespuestaCapturada(20_000m, null, "E-1", "130"));

        var vista = await new PreviewPaymasterResponseQueryHandler(_p.B.K.C.E.Db, _p.B.K.C.E.Alcance)
            .Handle(new PreviewPaymasterResponseQuery(f.PublicId, r.Value.PublicId), default);

        vista.Value.GroupBy.Should().Be("PaymasterConcept");
        vista.Value.Rows.Select(x => x.Key).Should().Equal("120", "130");
        vista.Value.Rows.Single(x => x.Key == "130").Deducted.Should().Be(20_000m);
    }

    [Fact]
    public async Task Devolver_emite_la_v1_o_la_v2_segun_el_archivo()
    {
        var v1 = await EnviadoAsync(_p.V1(1), 1);
        var r1 = await CapturarAsync(v1, new FilaDeRespuestaCapturada(100_000m, "CR-A"));
        var e1 = await _p.Devolver().Handle(new EmitPaymasterResultCommand(v1.PublicId, r1.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        var v2 = await EnviadoAsync(_p.V2(), 2);
        var r2 = await CapturarAsync(v2, new FilaDeRespuestaCapturada(100_000m, null, "E-1", "120"));
        var e2 = await _p.Devolver().Handle(new EmitPaymasterResultCommand(v2.PublicId, r2.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);

        e1.Value.Status.Should().Be(PaymasterResponseState.Emitted);
        e1.Value.EmittedMessageVersion.Should().Be(1);
        e2.Value.EmittedMessageVersion.Should().Be(2);
        var mensajes = await _p.B.K.C.E.Db.IntegrationMessages.Where(m => m.Type == ResultadoDeDescuentosV1.Type).OrderBy(m => m.Id).ToListAsync();
        mensajes.Select(m => m.Version).Should().Equal(1, 2);
        mensajes[1].PayloadJson.Should().Contain("conceptLines");
    }

    [Fact]
    public async Task Sin_la_version_adoptada_no_se_devuelve()
    {
        var p = new PagaduriasDePrueba(1);
        var f = await p.RecibirAsync(p.V2(), 2);
        await p.ConfirmarEnvio().Handle(new ConfirmPayrollFileSentCommand(f.PublicId, PayrollFileChannel.Email) { OperationKey = Guid.NewGuid() }, default);
        var r = await p.CargarRespuesta().Handle(new LoadPaymasterResponseCommand(f.PublicId, Captured: [new FilaDeRespuestaCapturada(1m, null, "E-1", "120")])
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        var e = await p.Devolver().Handle(new EmitPaymasterResultCommand(f.PublicId, r.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);

        e.Error.Code.Should().Be(ErroresDeRecaudo.CodigoVersionNoAdoptada);
    }

    [Fact]
    public async Task La_evaluacion_de_Cartera_acepta_y_escribe_el_resultado_o_rechaza_con_su_codigo()
    {
        var f = await EnviadoAsync(_p.V1(2), 1);
        var rechazada = await CapturarAsync(f, new FilaDeRespuestaCapturada(100_000m, "CR-A"), new FilaDeRespuestaCapturada(30_000m, "CR-B"));
        await _p.Devolver().Handle(new EmitPaymasterResultCommand(f.PublicId, rechazada.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        await EntregaACarteraAsync(rechazada.Value.PublicId, DeliveryStatus.Rejected, "Lending.PayrollFile.LineMismatch");

        var r1 = await _p.Evaluacion().Handle(new RefreshPaymasterResponseStatusCommand(f.PublicId, rechazada.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        r1.Value.Status.Should().Be(PaymasterResponseState.RejectedByLending);
        r1.Value.RejectionCode.Should().Be("Lending.PayrollFile.LineMismatch");

        var corregida = await CapturarAsync(f, new FilaDeRespuestaCapturada(100_000m, "CR-A"), new FilaDeRespuestaCapturada(30_000m, "CR-B"));
        corregida.Value.Version.Should().Be(2);
        await _p.Devolver().Handle(new EmitPaymasterResultCommand(f.PublicId, corregida.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        await EntregaACarteraAsync(corregida.Value.PublicId, DeliveryStatus.Processed, null);
        var r2 = await _p.Evaluacion().Handle(new RefreshPaymasterResponseStatusCommand(f.PublicId, corregida.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r2.Value.Status.Should().Be(PaymasterResponseState.AcceptedByLending);
        (await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFiles.SingleAsync(x => x.Id == f.Id)).Status.Should().Be(PayrollDeductionFileState.Responded);
        var lineas = await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFileLines.Where(l => l.PayrollDeductionFileId == f.Id).OrderBy(l => l.Id).ToListAsync();
        lineas.Select(l => l.DeductedAmount).Should().Equal(100_000m, 30_000m);
        lineas.Select(l => l.DeductionOutcome).Should().Equal(PaymasterDeductionOutcome.Deducted, PaymasterDeductionOutcome.PartiallyDeducted);
    }

    /// <summary>Deja la entrega a Cartera del resultado emitido de la respuesta como si Cartera la hubiera evaluado.</summary>
    private async Task EntregaACarteraAsync(Guid respuesta, DeliveryStatus estado, string? codigo)
    {
        var db = _p.B.K.C.E.Db;
        var r = await db.PaymasterResponses.SingleAsync(x => x.PublicId == respuesta);
        var mensaje = await db.IntegrationMessages.SingleAsync(m => m.PublicId == r.EmittedMessagePublicId);
        var entrega = await db.IntegrationMessageDeliveries.SingleAsync(d => d.MessageId == mensaje.Id && d.Destination == IntegrationDestinations.Lending);
        entrega.Status = estado;
        entrega.LastErrorCode = codigo;
        await db.SaveChangesAsync();
    }
}
