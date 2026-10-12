using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Payments.Commands;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// Feature 013, US5, T0520 (api.md §10; FR-072, C115; nombre de quickstart §1.5): la simulación de pantalla usa <c>AplicadorDePagos</c> igual que el
/// contrato y no escribe; <c>Lending.Payment.ValueDateOutOfRange</c> con <c>data.minDate</c>; <c>Lending.Payment.SpecificInstallmentBlocked</c> con
/// <c>data.pendingBefore[]</c> (el mismo código que el contrato); el aviso <c>Lending.Payment.AdvanceDoesNotReduceInterest</c> en un anticipo.
/// </summary>
public class SimulateLoanPaymentQueryHandlerTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;

    private PagosDePrueba _p = null!;

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Simula_con_la_misma_funcion_que_el_contrato_y_no_escribe()
    {
        var antes = await _p.Db.LoanOperations.CountAsync();

        var r = await _p.Simular().Handle(new SimulateLoanPaymentQuery(_p.Credito.PublicId, PaymentModality.FreeAmount, 250_000m, Hoy), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var contrato = await _p.Contrato().SimularAsync(new SolicitudDeSimulacion(PagosDePrueba.Prueba, _p.Credito.PublicId, ModalidadDePago.FreeAmount, 250_000m,
            Hoy), default);
        r.Value.SimulationId.Should().Be(contrato.Value!.SimulationId, "la misma función distribuye al simular y al aplicar (FR-072)");
        r.Value.Advance.Should().Be(250_000m);
        r.Value.Notices.Should().Contain(n => n.Code == "Lending.Payment.AdvanceDoesNotReduceInterest" && n.Message.Contains("no reduce intereses"));
        r.Value.ValidUntil.Should().Be(Hoy);
        (await _p.Db.LoanOperations.CountAsync()).Should().Be(antes);
    }

    [Fact]
    public async Task Sin_valor_una_cancelacion_simula_lo_que_cuesta_cancelar()
    {
        var r = await _p.Simular().Handle(new SimulateLoanPaymentQuery(_p.Credito.PublicId, PaymentModality.FullPayoff, null, Hoy), default);
        r.Value.Amount.Should().Be(3_000_000m);
        r.Value.ResultingBalances["PayoffAmount"].Should().Be(0m);
    }

    [Fact]
    public async Task Un_abono_sin_eleccion_trae_los_dos_planes()
    {
        var r = await _p.Simular().Handle(new SimulateLoanPaymentQuery(_p.Credito.PublicId, PaymentModality.ExtraordinaryPrincipal, 600_000m, Hoy), default);
        r.Value.Alternatives.Keys.Should().BeEquivalentTo(["ReduceTerm", "ReduceInstallment"]);
        r.Value.Alternatives["ReduceTerm"].Installments.Should().BeLessThan(24);
        r.Value.Alternatives["ReduceInstallment"].Installments.Should().Be(24);
    }

    [Fact]
    public async Task Una_fecha_fuera_de_la_ventana_responde_ValueDateOutOfRange_con_la_minima()
    {
        var r = await _p.Simular().Handle(new SimulateLoanPaymentQuery(_p.Credito.PublicId, PaymentModality.FreeAmount, 1m, Hoy.AddDays(-6)), default);
        r.Error.Code.Should().Be("Lending.Payment.ValueDateOutOfRange");
        Disbursements.DesembolsoDePrueba.Datos(r.Error).GetProperty("minDate").GetString().Should().Be(Hoy.AddDays(-5).ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task Una_cuota_especifica_con_anteriores_impagas_responde_el_mismo_codigo_que_el_contrato()
    {
        await _p.CausarHastaAsync(new DateOnly(2027, 2, 3));
        var r = await _p.Simular().Handle(new SimulateLoanPaymentQuery(_p.Credito.PublicId, PaymentModality.SpecificInstallment, 100_000m, _p.G.HoyLocal, null, 2), default);
        r.Error.Code.Should().Be("Lending.Payment.SpecificInstallmentBlocked");
        Disbursements.DesembolsoDePrueba.Datos(r.Error).GetProperty("pendingBefore").EnumerateArray().Select(e => e.GetInt32()).Should().Equal(1);
    }

    [Fact]
    public async Task Fuera_del_alcance_responde_el_404_del_credito()
    {
        _p.G.AlcanceActual = AlcanceDeCartera.Vacio;
        var r = await _p.Simular().Handle(new SimulateLoanPaymentQuery(_p.Credito.PublicId, PaymentModality.FreeAmount, 1m, Hoy), default);
        r.Error.Code.Should().Be("Lending.Loan.NotFound");
    }
}
