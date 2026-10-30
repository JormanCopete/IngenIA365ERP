using FluentAssertions;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, T0105 (FR-041, FR-047, FR-048, FR-192): los medios cubren lo aplicado más las vueltas (<c>MeansTotalMismatch</c>), las vueltas sólo salen del
/// efectivo (<c>ChangeOnlyInCash</c>), un bono no se repite (<c>VoucherAlreadyUsed</c>); la fecha valor anterior respeta
/// <c>Caja.FechaValorAnterior.MaxDias</c> (<c>BackdatedNotAllowed</c>) y exige motivo, soporte y aprobador presente (<c>BackdatedNeedsApproval</c>, 428); la
/// firmeza que devuelve el módulo queda en la aplicación. Escrita en la fase 6; se corre al final.
/// </summary>
public class MediosYFechasDelReciboTests
{
    private readonly CajaConContratos _k = new();

    private async Task<Guid> ConItemsAsync(decimal valor = 100_000, DateOnly? fecha = null, string? motivo = null, Guid? soporte = null)
    {
        await _k.AbrirAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId, ValueDate: fecha, BackdateReason: motivo,
            BackdateSupportAttachmentPublicId: soporte), default)).Value;
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, valor)]), default);
        return op.PublicId;
    }

    [Fact]
    public async Task Los_medios_que_no_alcanzan_responden_MeansTotalMismatch()
    {
        var op = await ConItemsAsync();
        var r = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(90_000)]), default);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoMediosNoCuadran);
    }

    [Fact]
    public async Task Las_vueltas_solo_salen_del_efectivo()
    {
        var op = await ConItemsAsync();
        var r = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Transferencia(150_000)]), default);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoVueltasSoloEnEfectivo);

        var conVueltas = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(120_000)]), default);
        conVueltas.Value.Change.Should().Be(20_000);
    }

    [Fact]
    public async Task Un_bono_ya_usado_no_se_repite()
    {
        var op = await ConItemsAsync();
        var bono = _k.C.E.Medio("BONO", PaymentMeansClass.Voucher, 0);
        _k.C.E.Ofrecer(_k.C.Punto, bono);
        // La sesión selló los medios al abrir: el bono no está sellado y responde MeansNotOffered; se registra un recibo anterior con el bono para el UK.
        _k.C.E.Db.ReceiptMeans.Add(new ReceiptMeans { ReceiptId = 1, PaymentMeansId = _k.C.Efectivo.Id, VoucherNumber = "B-77", Amount = 1 });
        await _k.C.E.Db.SaveChangesAsync();

        var r = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(100_000) with { VoucherNumber = "B-77" }]), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoBonoUsado);
    }

    [Fact]
    public async Task La_fecha_valor_anterior_sin_dias_admitidos_no_se_confirma()
    {
        var op = await ConItemsAsync(fecha: EscenarioDeRecaudo.Hoy.AddDays(-1), motivo: "Se fue la luz", soporte: Guid.NewGuid());
        await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(100_000)]), default);

        var r = await _k.Confirmacion().ConfirmarAsync(op, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoFechaAnteriorNoAdmitida);
    }

    [Fact]
    public async Task La_fecha_valor_anterior_exige_motivo_soporte_y_aprobador_presente()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaFechaValorAnteriorMaxDias, "3");
        var sinMotivo = await ConItemsAsync(fecha: EscenarioDeRecaudo.Hoy.AddDays(-1));
        await _k.Medios().Handle(new UpdateCashOperationMeansCommand(sinMotivo, [_k.Efectivo(100_000)]), default);
        (await _k.Confirmacion().ConfirmarAsync(sinMotivo, Guid.NewGuid(), null, default)).Error.Code.Should().Be(ErroresDeRecaudo.CodigoFechaAnteriorConAprobacion);
    }

    [Fact]
    public async Task La_firmeza_que_devuelve_el_modulo_queda_en_la_aplicacion()
    {
        var op = await ConItemsAsync();
        await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(100_000)]), default);

        var r = await _k.Confirmacion().ConfirmarAsync(op, Guid.NewGuid(), null, default);

        r.Value.Applications.Single().FinalityUntil.Should().Be(EscenarioDeRecaudo.Hoy.AddDays(3));
        (await _k.C.E.Db.ReceiptApplications.SingleAsync()).FinalityUntil.Should().Be(EscenarioDeRecaudo.Hoy.AddDays(3));
    }
}
