using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Agreements;
using IngenIA365ERP.Application.Tests.Lending.Libranza;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// Feature 013, K2, T0811 (interfaces.md §3.7, enmienda A9): el giro de una pagaduría entra por el contrato de obligaciones como un pago
/// <c>ExternalPaymasterDeduction</c> que cita la línea del archivo de descuentos que quedó pendiente de giro.
/// <list type="bullet">
/// <item>con un pendiente de esa pagaduría, la referencia es obligatoria (<c>Lending.Payment.PaymasterReferenceRequired</c>) y tiene que ser de un
/// pendiente (<c>.PaymasterReferenceInvalid</c>);</item>
/// <item>con referencia, la fecha valor es la del descuento aunque pase <c>Pagos.DiasMaximosFechaValor</c>, la pagaduría es el pagador y el tercero de
/// <c>PagoAplicado</c>, la operación guarda el resultado y el pendiente se levanta;</item>
/// <item>sin pendiente el pago va sin referencia; reversarlo vuelve a dejar el pendiente.</item>
/// </list>
/// </summary>
public sealed class PagoDePagaduriaPorElContratoTests : IAsyncLifetime
{
    private static readonly DateOnly Descuento = new(2027, 1, 30);

    private LibranzaDePrueba _l = null!;
    private Guid _linea;
    private decimal _valor;

    public async Task InitializeAsync()
    {
        _l = await LibranzaDePrueba.CrearAsync();
        var archivo = await _l.ArchivoAsync();
        var linea = await _l.LineaAsync(archivo.PublicId, _l.Credito);
        (_linea, _valor) = (linea.PublicId, linea.TotalAmount);
        await _l.ConfirmarYConsumirAsync(archivo.PublicId);
        await _l.ResultadoYConsumoAsync(archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "00", _valor, Descuento)));
        // Once días después del descuento: fuera de los cinco que admite la fecha valor de un pago registrado.
        await _l.P.CausarHastaAsync(Descuento.AddDays(11));
        _l.G.ActuarComo(_l.D.Cajera);
    }

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private PagoAAplicar Giro(Guid? linea, string? clave = null) =>
        _l.P.Pago(ModalidadDePago.FreeAmount, _valor, _l.G.HoyLocal, clave, medio: ClaseDeMedioDeRecaudo.ExternalPaymasterDeduction, pagador: _l.Pagaduria.PublicId)
            with { DeductionFileLinePublicId = linea };

    private async Task<int> PendientesAsync() =>
        (await _l.Pendientes().Handle(new ListPendingPayrollTransfersQuery(_l.Convenio.PublicId), default)).Value.Count;

    [Fact]
    public async Task Con_un_pendiente_la_referencia_es_obligatoria_y_tiene_que_ser_de_un_pendiente()
    {
        (await PendientesAsync()).Should().Be(1);

        var sin = await _l.P.AplicarAsync(Giro(null));
        sin.Code.Should().Be(ErroresDeLibranza.CodigoReferenciaRequerida, sin.Message);

        var ajena = await _l.P.AplicarAsync(Giro(Guid.NewGuid()));
        ajena.Code.Should().Be(ErroresDeLibranza.CodigoReferenciaInvalida, ajena.Message);

        // Otro pagador (la propia deudora en efectivo) no necesita referencia: el pendiente es de la pagaduría.
        var efectivo = await _l.P.AplicarAsync(_l.P.Pago(ModalidadDePago.FreeAmount, 10_000m));
        efectivo.Result.Should().Be(ResultadoDelContrato.Applied, $"{efectivo.Code}: {efectivo.Message}");
    }

    [Fact]
    public async Task Con_referencia_la_fecha_valor_es_la_del_descuento_la_pagaduria_paga_y_el_pendiente_se_levanta()
    {
        var aplicado = await _l.P.AplicadoAsync(Giro(_linea));

        var op = await _l.P.OperacionAsync(aplicado.OperationPublicId);
        op.ValueDate.Should().Be(Descuento, "la fecha valor es la del descuento aunque pase Pagos.DiasMaximosFechaValor (A9)");
        op.PayerPersonId.Should().Be(_l.Pagaduria.Id);
        var resultado = await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync();
        op.PayrollDeductionResultId.Should().Be(resultado.Id);
        op.PaymasterPendingLineReference.Should().NotBeNullOrEmpty();
        (await PendientesAsync()).Should().Be(0);

        var mensaje = await _l.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == PagoAplicadoV1.Type && m.OriginPublicId == aplicado.OperationPublicId);
        mensaje.PayloadJson.Should().Contain(_linea.ToString()).And.Contain(_l.Pagaduria.PublicId.ToString(), "la pagaduría es el tercero del pago");
        await _l.P.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Reversar_el_giro_vuelve_a_dejar_el_pendiente()
    {
        var pago = Giro(_linea);
        await _l.P.AplicadoAsync(pago);
        (await PendientesAsync()).Should().Be(0);

        var r = await _l.P.Contrato().ReversarAsync(new SolicitudDeReverso(PagosDePrueba.Prueba, pago.IdempotencyKey, "Giro devuelto por el banco"), default);
        _l.Db.ChangeTracker.Clear();

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        (await PendientesAsync()).Should().Be(1);
        await _l.P.ElLibroCuadraAsync();
        // Y el giro siguiente vuelve a citarlo.
        (await _l.P.AplicarAsync(Giro(null))).Code.Should().Be(ErroresDeLibranza.CodigoReferenciaRequerida);
    }

    [Fact]
    public async Task Sin_pendiente_el_pago_de_la_pagaduria_va_sin_referencia_y_no_levanta_nada()
    {
        await _l.P.AplicadoAsync(Giro(_linea));

        var otro = await _l.P.AplicadoAsync(Giro(null) with { Amount = 50_000m });

        var op = await _l.P.OperacionAsync(otro.OperationPublicId);
        (op.PayrollDeductionResultId, op.PaymasterPendingLineReference).Should().Be(((int?)null, (string?)null));
        op.ValueDate.Should().Be(_l.G.HoyLocal);
    }
}
