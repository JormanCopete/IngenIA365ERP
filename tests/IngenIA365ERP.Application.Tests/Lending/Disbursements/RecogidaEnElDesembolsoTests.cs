using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 013, US3, T0419 (FR-046; data-model §7.3; T21, T91): cada crédito recogido se paga por <see cref="IContratoDeObligaciones.AplicarAsync"/>
/// dentro de la transacción del desembolso, con el recaudador <c>CAR</c> de clase <c>OwnLoanPayoff</c>, la clave
/// <c>CAR:{operaciónDeDesembolso:N}:{obligationPublicId:N}</c>, el medio <c>InternalOffset</c> y la modalidad <c>FullPayoff</c> o <c>FreeAmount</c>;
/// el neto de la orden es el bruto menos lo recogido y un neto que no queda positivo se rechaza; un recogido en mora es reestructuración
/// (<c>Lending.Payoff.RequiresRestructuring</c>, también en la vista previa); la recogida que la solicitud no autoriza no se admite; si el contrato
/// no aplica un ítem, el desembolso no existe.
/// <para>
/// El efecto en el recogido —su operación <c>Payment</c> con <c>RelatedOperationId</c>, <c>FinalityDate = ValueDate</c>, el <c>PaidOff</c> y su
/// <c>PagoAplicado</c>— es del contrato real (fase 9, <c>AplicacionDePagos</c>): aquí el contrato es un doble y se prueba lo que el desembolso le pide
/// y lo que hace con la respuesta.
/// </para>
/// </summary>
public sealed class RecogidaEnElDesembolsoTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private Loan _viejo = null!;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _viejo = _d.O.Credito(_d.O.Ana, 1_000_000m, "CR-VIEJO");
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private Task<CreditApplicationDto> AprobadaConRecogidaAsync(params OwnLoanPayoffInput[] recogidas) =>
        _d.AprobadaAsync(_d.O.Solicitud() with { OwnLoanPayoffs = recogidas.Length == 0 ? [new OwnLoanPayoffInput(_viejo.PublicId)] : recogidas });

    [Fact]
    public async Task La_recogida_total_va_por_el_contrato_con_la_clave_CAR_y_el_cruce_interno()
    {
        var s = await AprobadaConRecogidaAsync();

        var r = await _d.DesembolsadoAsync(_d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(_viejo.PublicId)] });

        var lote = _d.Aplicadas.Should().ContainSingle().Subject;
        (lote.Collector.Code, lote.Collector.Class).Should().Be(("CAR", ClaseDeRecaudador.OwnLoanPayoff));
        var pago = lote.Payments.Should().ContainSingle().Subject;
        pago.IdempotencyKey.Should().Be($"CAR:{r.OperationPublicId:N}:{_viejo.PublicId:N}");
        (pago.MeansClass, pago.Modality, pago.Amount, pago.ValueDate).Should()
            .Be((ClaseDeMedioDeRecaudo.InternalOffset, ModalidadDePago.FullPayoff, 1_000_000m, DesembolsoDePrueba.Hoy));
        pago.PayerPublicId.Should().Be(_d.O.Ana.PublicId);

        r.Payoffs.Should().ContainSingle(p => p.LoanPublicId == _viejo.PublicId && p.Applied == 1_000_000m);
        r.NetToDisburse.Should().Be(2_000_000m);
        var orden = await _d.Db.DisbursementOrders.AsNoTracking().SingleAsync();
        (orden.GrossAmount, orden.PayoffAmount, orden.NetAmount).Should().Be((3_000_000m, 1_000_000m, 2_000_000m));
    }

    [Fact]
    public async Task La_recogida_parcial_va_como_FreeAmount_por_el_valor_pedido()
    {
        var s = await AprobadaConRecogidaAsync(new OwnLoanPayoffInput(_viejo.PublicId, "Partial", 400_000m));

        await _d.DesembolsadoAsync(_d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(_viejo.PublicId, "Partial", 400_000m)] });

        var pago = _d.Aplicadas.Single().Payments.Single();
        (pago.Modality, pago.Amount).Should().Be((ModalidadDePago.FreeAmount, 400_000m));
    }

    [Fact]
    public async Task Un_recogido_en_mora_es_reestructuracion_y_lo_dice_la_vista_previa()
    {
        var s = await AprobadaConRecogidaAsync();
        _d.Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = _viejo.Id, PositionDate = DesembolsoDePrueba.Hoy, Version = 1, RegisteredAt = _d.G.UtcNow, Status = LoanStatus.Active, BranchId = _d.O.Norte.Id,
            CreditLineId = 1, ModalityCode = "CONSUMO", Principal = 1_000_000m, PayoffAmount = 1_050_000m, DaysPastDue = 12,
        });
        await _d.Db.SaveChangesAsync();
        var entrada = _d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(_viejo.PublicId)] };

        var r = await _d.DesembolsarAsync(entrada);

        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoRequiereReestructuracion);
        DesembolsoDePrueba.Datos(r.Error).GetProperty("loans")[0].GetProperty("number").GetString().Should().Be("CR-VIEJO");
        _d.Aplicadas.Should().BeEmpty();
        var vista = await _d.VistaPrevia().Handle(new PreviewDisbursementQuery(entrada), default);
        vista.Value.Payoffs.Single().Should().Match<DisbursementPreviewPayoffDto>(p => p.RequiresRestructuring && p.DaysPastDue == 12 && p.ToPayOff == 1_050_000m);
        vista.Value.Blocking.Should().Contain(ErroresDeDesembolso.CodigoRequiereReestructuracion);
    }

    [Fact]
    public async Task La_recogida_que_la_solicitud_no_autoriza_no_se_admite()
    {
        var otro = _d.O.Credito(_d.O.Ana, 500_000m, "CR-OTRO");
        var s = await AprobadaConRecogidaAsync();

        var r = await _d.DesembolsarAsync(_d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(otro.PublicId)] });

        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoRecogidaNoAutorizada);
        _d.Aplicadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_neto_que_no_queda_positivo_se_rechaza()
    {
        var s = await AprobadaConRecogidaAsync(new OwnLoanPayoffInput(_viejo.PublicId, "Partial", 3_000_000m));
        _d.Db.LoanTransactions.Add(new Domain.Entities.Lending.Transactions.LoanTransaction
        {
            LoanId = _viejo.Id, Kind = LoanTransactionKind.Disbursement, Concept = LoanConcept.Principal, Amount = 2_500_000m, ValueDate = _viejo.OpenedOn,
            AccountingDate = _viejo.OpenedOn, RegisteredAt = _d.G.UtcNow.AddMonths(-9), OperationId = 1,
        });
        await _d.Db.SaveChangesAsync();

        var r = await _d.DesembolsarAsync(_d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(_viejo.PublicId, "Partial", 3_000_000m)] });

        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoNetoNoPositivo);
    }

    [Fact]
    public async Task Si_el_contrato_no_aplica_un_item_el_desembolso_no_existe()
    {
        var s = await AprobadaConRecogidaAsync();
        _d.Contrato.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>()).Returns(ci => Task.FromResult(new RespuestaDeAplicacionDto(
            [new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(ResultadoDelContrato.Rejected, ContratoDeObligacionesSinActivar.CodigoNoActivada,
                ContratoDeObligacionesSinActivar.MensajeNoActivada, null)], [])));

        var r = await _d.DesembolsarAsync(_d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(_viejo.PublicId)] });

        r.Error.Code.Should().Be(ContratoDeObligacionesSinActivar.CodigoNoActivada);
        (await _d.Db.Loans.CountAsync()).Should().Be(1, "sólo queda el recogido: el crédito nuevo no nació");
        (await _d.Db.LoanOperations.CountAsync()).Should().Be(0);
    }
}
