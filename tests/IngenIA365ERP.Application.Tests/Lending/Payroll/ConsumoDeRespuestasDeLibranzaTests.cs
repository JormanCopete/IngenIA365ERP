using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Files;
using IngenIA365ERP.Application.Lending.Payroll.Results;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 013, K2, T0810 (mensajes.md §12.5, §12.6; FR-114, FR-115): lo que Recaudo devuelve del archivo de descuentos.
/// <list type="bullet">
/// <item><c>EnvioDeDescuentosConfirmado</c>: <c>Generated</c> → <c>Sent</c> con fecha, radicado y mensaje; ya <c>Sent</c>, <c>Processed</c> sin efecto;
/// <c>Superseded</c>/<c>Voided</c> → <c>Rejected</c> <c>Lending.PayrollFile.Superseded</c>; huella distinta → <c>.HashMismatch</c>.</item>
/// <item><c>ResultadoDeDescuentos</c>, todo o nada: descontado → resultado con la ventana de giro y el umbral sellados; no aplicado → no aplicado de
/// origen <c>PaymasterResult</c> con el motivo traducido por los códigos, seguimiento automático, aviso <c>NOAPLICPAG</c> y alerta por archivo; parcial →
/// las dos cosas; <c>Generated</c> pasa a <c>Sent</c>; <c>expectedAmount</c> distinto → <c>.LineMismatch</c> con las líneas; un resultado posterior
/// reemplaza; el recibo es único por mensaje.</item>
/// </list>
/// </summary>
public sealed class ConsumoDeRespuestasDeLibranzaTests : IAsyncLifetime
{
    private static readonly DateOnly Descuento = new(2027, 1, 30);

    private LibranzaDePrueba _l = null!;
    private PayrollDeductionFileDto _archivo = null!;

    public async Task InitializeAsync()
    {
        _l = await LibranzaDePrueba.CrearAsync();
        _archivo = await _l.ArchivoAsync();
    }

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private Task<PayrollDeductionFile> ArchivoAsync(Guid? id = null) =>
        _l.Db.PayrollDeductionFiles.AsNoTracking().SingleAsync(f => f.PublicId == (id ?? _archivo.PublicId));

    /// <summary>Cambia una propiedad de sólo inicio en la base (simula lo que llegó distinto de lo que Cartera guardó).</summary>
    private async Task CambiarAsync<T>(T entidad, string propiedad, object valor) where T : class
    {
        _l.Db.Attach(entidad);
        _l.Db.Entry(entidad).Property(propiedad).CurrentValue = valor;
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
    }

    private async Task<TestPayrollMessageDto> EmitirResultadoAsync(byte[] respuesta)
    {
        _l.G.ActuarComo(_l.D.Cajera);
        var r = await _l.EmitirResultado().Handle(new EmitTestPayrollDeductionResultCommand(_archivo.PublicId, "respuesta.csv", respuesta) { OperationKey = Guid.NewGuid() },
            default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _l.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private async Task<ResultadoDeConsumo> ConsumirResultadoAsync(Guid mensaje)
    {
        _l.G.ActuarComoProceso();
        var c = await _l.ConsumirResultado().Handle(new ConsumePayrollDeductionResultCommand([mensaje]), default);
        _l.Db.ChangeTracker.Clear();
        c.IsSuccess.Should().BeTrue(c.IsFailure ? $"{c.Error.Code}: {c.Error.Message}" : string.Empty);
        return c.Value;
    }

    private async Task<decimal> EsperadoAsync() => (await _l.LineaAsync(_archivo.PublicId, _l.Credito)).TotalAmount;

    // --------------------------------------------------------------------------------------------------------------- envío confirmado --

    [Fact]
    public async Task El_envio_confirmado_deja_el_archivo_Sent_y_repetirlo_no_tiene_efecto()
    {
        var r = await _l.ConfirmarEnvio().Handle(new ConfirmTestPayrollFileSentCommand(_archivo.PublicId, "Portal", "RAD-2027-01") { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        var c = await _l.ConsumirEnvio().Handle(new ConsumePayrollFileSentCommand([r.Value.MessagePublicId]), default);
        _l.Db.ChangeTracker.Clear();

        c.Value.Should().BeOfType<ResultadoDeConsumo.Processed>();
        var f = await ArchivoAsync();
        (f.Status, f.PaymasterReference, f.SentConfirmationMessagePublicId).Should().Be((PayrollFileStatus.Sent, "RAD-2027-01", (Guid?)r.Value.MessagePublicId));
        f.SentAt.Should().NotBeNull();
        var recibo = await _l.Db.LendingInboundMessages.AsNoTracking().SingleAsync(x => x.MessagePublicId == r.Value.MessagePublicId);
        recibo.PayrollDeductionFileId.Should().Be(f.Id);

        (await _l.ConsumirEnvio().Handle(new ConsumePayrollFileSentCommand([r.Value.MessagePublicId]), default)).Value
            .Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>("el recibo es único por mensaje");
        var otra = await _l.ConfirmarYConsumirAsync(_archivo.PublicId, "RAD-OTRO");
        otra.Should().BeOfType<ResultadoDeConsumo.Processed>("ya enviado: se procesa sin efecto");
        (await ArchivoAsync()).PaymasterReference.Should().Be("RAD-2027-01");
    }

    [Fact]
    public async Task Un_envio_de_un_archivo_reemplazado_o_con_otra_huella_se_rechaza()
    {
        var r = await _l.ConfirmarEnvio().Handle(new ConfirmTestPayrollFileSentCommand(_archivo.PublicId) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        await CambiarAsync(await ArchivoAsync(), nameof(PayrollDeductionFile.FileSha256), new string('0', 64));

        var huella = (await _l.ConsumirEnvio().Handle(new ConsumePayrollFileSentCommand([r.Value.MessagePublicId]), default)).Value;
        _l.Db.ChangeTracker.Clear();

        huella.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ErroresDeLibranza.CodigoHuellaDistinta);
        (await ArchivoAsync()).Status.Should().Be(PayrollFileStatus.Generated);

        var nuevo = await _l.ArchivoAsync();
        var r2 = await _l.ConfirmarEnvio().Handle(new ConfirmTestPayrollFileSentCommand(nuevo.PublicId) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        var r3 = await _l.ConfirmarEnvio().Handle(new ConfirmTestPayrollFileSentCommand(nuevo.PublicId) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        _l.G.ActuarComo(_l.D.Cajera);
        (await _l.Anular().Handle(new VoidPayrollDeductionFileCommand(nuevo.PublicId, "Se anula para la prueba") { OperationKey = Guid.NewGuid() }, default))
            .IsSuccess.Should().BeTrue();
        _l.Db.ChangeTracker.Clear();

        // El primer archivo quedó Superseded al regenerar; el segundo, Voided.
        (await _l.ConsumirEnvio().Handle(new ConsumePayrollFileSentCommand([r.Value.MessagePublicId]), default)).Value
            .Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ErroresDeLibranza.CodigoArchivoReemplazado, "un rechazo se puede reprocesar");
        _l.Db.ChangeTracker.Clear();
        (await _l.ConsumirEnvio().Handle(new ConsumePayrollFileSentCommand([r2.Value.MessagePublicId]), default)).Value
            .Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ErroresDeLibranza.CodigoArchivoReemplazado);
        r3.IsSuccess.Should().BeTrue();
        (await _l.Db.LendingInboundMessageEvaluations.AsNoTracking().CountAsync(e => e.FailureCode == ErroresDeLibranza.CodigoArchivoReemplazado)).Should().Be(2);
    }

    // ---------------------------------------------------------------------------------------------------------- resultado de descuentos --

    [Fact]
    public async Task Lo_descontado_queda_pendiente_de_giro_con_la_ventana_y_el_umbral_sellados_y_el_archivo_pasa_a_Sent()
    {
        var esperado = await EsperadoAsync();

        var c = await _l.ResultadoYConsumoAsync(_archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "00", esperado, Descuento)));

        c.Should().BeOfType<ResultadoDeConsumo.Processed>();
        var r = await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync();
        (r.Outcome, r.DeductedAmount, r.NotDeductedAmount, r.DeductionDate, r.PaymasterResultCode, r.NonDeductionReason)
            .Should().Be((PayrollResultOutcome.Deducted, esperado, 0m, (DateOnly?)Descuento, "00", (NonDeductionReason?)null));
        var version = await _l.Db.PayrollAgreementVersions.AsNoTracking().SingleAsync(v => v.PayrollAgreementId == _l.Convenio.Id);
        var parametros = (await _l.Convenios().ParametrosAsync(version, LibranzaDePrueba.Hoy, default)).Value;
        (r.TransferDueDate, r.PaymasterArrearsThresholdDays)
            .Should().Be((PayrollDeductionResult.VencimientoDelGiro(Descuento, parametros.VentanaDeGiroDias), (int?)parametros.UmbralDeMoraDias));
        r.SupersedesResultId.Should().BeNull();
        var f = await ArchivoAsync();
        f.Status.Should().Be(PayrollFileStatus.Sent, "el resultado prueba el envío");
        f.FirstResultAt.Should().NotBeNull();
        (await _l.Db.InstallmentNonDeductions.AnyAsync()).Should().BeFalse();
        _l.Alertas.Should().NotContain(a => a.TypeCode == TiposDeAlerta.NoAplicadoDePagaduria);
    }

    [Fact]
    public async Task Lo_no_aplicado_deja_su_no_aplicado_con_el_motivo_traducido_el_seguimiento_el_aviso_y_la_alerta()
    {
        await _l.ResultadoYConsumoAsync(_archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "07", 0m, null)));

        var r = await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync();
        (r.Outcome, r.DeductedAmount, r.NonDeductionReason, r.TransferDueDate).Should().Be((PayrollResultOutcome.NotDeducted, 0m, (NonDeductionReason?)NonDeductionReason.Leave,
            (DateOnly?)null));
        var n = await _l.Db.InstallmentNonDeductions.AsNoTracking().SingleAsync();
        (n.Origin, n.Reason, n.ReasonText, n.NotDeductedAmount, n.PayrollDeductionResultId, n.InstallmentNumber, n.CollectorCode)
            .Should().Be((NonDeductionOrigin.PaymasterResult, NonDeductionReason.Leave, "Licencia no remunerada", r.NotDeductedAmount, (int?)r.Id, 1,
                "REC"), "el recaudo es el módulo de origen del mensaje");
        var seguimiento = await _l.Db.NonDeductionFollowUps.AsNoTracking().Where(s => s.InstallmentNonDeductionId == n.Id).ToListAsync();
        seguimiento.Should().Contain(s => s.Action == NonDeductionFollowUpAction.MovedToCashPayment && s.IsAutomatic, "la regla del convenio");
        var aviso = await _l.Db.DebtorMessages.AsNoTracking().SingleAsync(m => m.InstallmentNonDeductionId == n.Id);
        (aviso.Purpose, aviso.OriginKind, aviso.OriginPublicId).Should().Be((DebtorMessagePurpose.Informative, SeguimientoDeNoAplicados.OrigenDelMensaje,
            (Guid?)n.PublicId));
        aviso.RenderedBody.Should().Contain("licencia");
        var alerta = _l.Alertas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.NoAplicadoDePagaduria).Subject;
        alerta.DedupKey.Should().Be($"{TiposDeAlerta.NoAplicadoDePagaduria}:{_archivo.PublicId:D}", "una alerta por archivo");
    }

    [Fact]
    public async Task Una_parcial_deja_las_dos_cosas_y_un_resultado_posterior_reemplaza_al_anterior()
    {
        var esperado = await EsperadoAsync();
        await _l.ResultadoYConsumoAsync(_archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "07", 100_000m, Descuento)));

        var parcial = await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync();
        (parcial.Outcome, parcial.DeductedAmount, parcial.NotDeductedAmount).Should().Be((PayrollResultOutcome.PartiallyDeducted, 100_000m, esperado - 100_000m));
        parcial.TransferDueDate.Should().NotBeNull("lo descontado queda pendiente de giro");
        (await _l.Db.InstallmentNonDeductions.AsNoTracking().SingleAsync()).NotDeductedAmount.Should().Be(esperado - 100_000m);

        await _l.ResultadoYConsumoAsync(_archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "00", esperado, Descuento)));

        var ultimo = await _l.Db.PayrollDeductionResults.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync();
        (ultimo.Outcome, ultimo.SupersedesResultId).Should().Be((PayrollResultOutcome.Deducted, (int?)parcial.Id));
    }

    [Fact]
    public async Task Una_linea_que_no_espera_lo_enviado_rechaza_todo_el_resultado()
    {
        var esperado = await EsperadoAsync();
        var m = await EmitirResultadoAsync(LibranzaDePrueba.Respuesta((_l.Credito.Number, "00", esperado, Descuento)));
        await CambiarAsync(await _l.LineaAsync(_archivo.PublicId, _l.Credito), nameof(PayrollDeductionFileLine.TotalAmount), esperado + 1m);

        var c = await ConsumirResultadoAsync(m.MessagePublicId);

        var rechazo = c.Should().BeOfType<ResultadoDeConsumo.Rejected>().Subject;
        rechazo.Code.Should().Be(ErroresDeLibranza.CodigoLineasQueNoCasan);
        rechazo.DataJson.Should().Contain("ExpectedAmount");
        (await _l.Db.PayrollDeductionResults.AnyAsync()).Should().BeFalse("todo o nada");
        (await ArchivoAsync()).Status.Should().Be(PayrollFileStatus.Generated);
        var recibo = await _l.Db.LendingInboundMessages.AsNoTracking().SingleAsync(x => x.MessagePublicId == m.MessagePublicId);
        recibo.Status.Should().Be(InboundMessageStatus.Rejected);

        // Corregido el dato, el mismo mensaje se reprocesa sobre el mismo recibo; después, ya procesado.
        await CambiarAsync(await _l.LineaAsync(_archivo.PublicId, _l.Credito), nameof(PayrollDeductionFileLine.TotalAmount), esperado);
        (await ConsumirResultadoAsync(m.MessagePublicId)).Should().BeOfType<ResultadoDeConsumo.Processed>();
        (await ConsumirResultadoAsync(m.MessagePublicId)).Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        (await _l.Db.LendingInboundMessages.CountAsync(x => x.MessagePublicId == m.MessagePublicId)).Should().Be(1);
        (await _l.Db.PayrollDeductionResults.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_repartidor_de_Recaudo_manda_cada_tipo_a_su_consumidor()
    {
        var sender = Substitute.For<MediatR.ISender>();
        var envio = await _l.ConfirmarEnvio().Handle(new ConfirmTestPayrollFileSentCommand(_archivo.PublicId) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        var resultado = await EmitirResultadoAsync(LibranzaDePrueba.Respuesta((_l.Credito.Number, "07", 0m, null)));
        var h = new ConsumeReceiptsMessageCommandHandler(_l.Db, sender);

        await h.Handle(new ConsumeReceiptsMessageCommand([envio.Value.MessagePublicId]), default);
        await h.Handle(new ConsumeReceiptsMessageCommand([resultado.MessagePublicId]), default);

        await sender.Received(1).Send(Arg.Is<ConsumePayrollFileSentCommand>(x => x.MessagePublicIds[0] == envio.Value.MessagePublicId),
            Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Is<ConsumePayrollDeductionResultCommand>(x => x.MessagePublicIds[0] == resultado.MessagePublicId),
            Arg.Any<CancellationToken>());
        (await h.Handle(new ConsumeReceiptsMessageCommand([Guid.NewGuid()]), default)).IsFailure.Should().BeTrue();
    }
}
