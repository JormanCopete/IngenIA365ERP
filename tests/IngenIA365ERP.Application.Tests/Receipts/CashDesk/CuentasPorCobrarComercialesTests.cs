using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, R1, T0394 (FR-049, FR-179): las cuentas por cobrar comerciales.
/// <list type="bullet">
/// <item>la consulta de pendientes por persona y factura es de Contabilidad (<c>IContabilidadParaRecaudo.CuentasPorCobrarComercialesAsync</c>), neta de lo que ya cobró
/// Recaudo en recibos vigentes;</item>
/// <item>la aplicación <c>CommercialReceivable</c> lleva el tercero y la factura de cruce en <c>ReciboEmitido</c> con el rol <c>CuentasPorCobrarComerciales</c>;</item>
/// <item>no se cobra más que el saldo, ni una factura sin saldo, ni como concepto vario;</item>
/// <item>lo cobrado queda en <c>RCD_CommercialReceivableApplications</c>: antes de activar Cartera, el recibo es la referencia que Cartera incorpora y el informe de
/// aplicaciones lo entrega.</item>
/// </list>
/// Prueba escrita, sin correr.
/// </summary>
public sealed class CuentasPorCobrarComercialesTests
{
    private readonly CajaConContratos _k = new();
    private readonly IContabilidadParaRecaudo _contabilidad = Substitute.For<IContabilidadParaRecaudo>();
    private readonly Guid _venta = Guid.NewGuid();

    public CuentasPorCobrarComercialesTests()
    {
        _contabilidad.CuentasPorCobrarComercialesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([new CuentaPorCobrarComercialDto(_venta, "FV-100", EscenarioDeRecaudo.Hoy.AddDays(-20), 150_000m)]);
        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<CombinacionDeHecho>>(), Arg.Any<CancellationToken>()).Returns(new EvaluacionContableDto(true, "Bloquear", []));
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null));
    }

    private UpdateCashOperationItemsCommandHandler Items() => new(_k.C.E.Db, _k.C.E.Alcance, _k.Obligaciones, _k.Cuentas, contabilidad: _contabilidad);

    private ConfirmacionDeRecibo Confirmacion() => new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.E.Reloj, _k.C.Cerrojo, _k.C.Numerador, _k.C.E.Parametros, _k.C.Presente,
        _contabilidad, _k.Obligaciones, _k.Cuentas, _k.Listas, _k.C.Emision, _k.C.E.Alertas);

    private static ItemPedidoDto Factura(decimal valor, string factura = "FV-100") => new(ApplicationTarget.CommercialReceivable, valor, CrossDocument: factura);

    private async Task<Guid> OperacionAsync()
    {
        await _k.AbrirAsync();
        return (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value.PublicId;
    }

    [Fact]
    public async Task No_se_cobra_mas_que_el_saldo_ni_una_factura_sin_saldo()
    {
        var op = await OperacionAsync();

        (await Items().Handle(new UpdateCashOperationItemsCommand(op, [Factura(200_000m)]), default)).Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        (await Items().Handle(new UpdateCashOperationItemsCommand(op, [Factura(10_000m, "FV-999")]), default)).Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        (await Items().Handle(new UpdateCashOperationItemsCommand(op, [Factura(10_000m, "")]), default)).Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        (await Items().Handle(new UpdateCashOperationItemsCommand(op, [Factura(150_000m)]), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Se_cobra_con_una_cuota_de_credito_con_tercero_y_factura_de_cruce_y_el_saldo_baja()
    {
        var op = await OperacionAsync();
        (await Items().Handle(new UpdateCashOperationItemsCommand(op, [_k.Cuota(_k.CreditoA, 100_000m), Factura(60_000m)]), default)).IsSuccess.Should().BeTrue();
        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(160_000m)]), default)).IsSuccess.Should().BeTrue();

        var r = await Confirmacion().ConfirmarAsync(op, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var detalle = await _k.C.E.Db.CommercialReceivableApplications.SingleAsync();
        detalle.InvoiceNumber.Should().Be("FV-100");
        detalle.ConsultedBalance.Should().Be(150_000m);
        var mensaje = await _k.C.E.Db.IntegrationMessages.SingleAsync(m => m.Type == ReciboEmitidoV1.Type);
        mensaje.PayloadJson.Should().Contain(RolesDeRecaudo.CuentasPorCobrarComerciales).And.Contain("FV-100");

        var pendiente = await CuentasPorCobrarDeRecaudo.PendientesAsync(_k.C.E.Db, _contabilidad, _k.Asociado.PublicId, default);
        pendiente.Single().Pending.Should().Be(90_000m, "Recaudo descuenta lo que ya cobró aunque el lote no se haya contabilizado");
    }

    [Fact]
    public async Task El_informe_de_aplicaciones_entrega_lo_cobrado()
    {
        var op = await OperacionAsync();
        await Items().Handle(new UpdateCashOperationItemsCommand(op, [Factura(50_000m)]), default);
        await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(50_000m)]), default);
        (await Confirmacion().ConfirmarAsync(op, Guid.NewGuid(), null, default)).IsSuccess.Should().BeTrue();

        var informe = await new ListCommercialReceivableApplicationsQueryHandler(_k.C.E.Db, _k.C.E.Alcance).Handle(new ListCommercialReceivableApplicationsQuery(), default);

        informe.Value.Should().ContainSingle(a => a.InvoiceNumber == "FV-100" && a.Amount == 50_000m && !a.ReceiptVoided);
    }
}
