using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// 2026-11-16 (pedido del dueño): el detalle de una obligación que la caja de Recaudo mira antes de cobrar (<see cref="IContratoDeObligaciones.DetalleAsync"/>), con
/// el contrato <b>real</b> sobre InMemory (<see cref="PagosDePrueba"/>: el crédito de Ana, 3.000.000 a 24 cuotas, desembolsado hoy). Lo que el detalle dice que
/// cuesta anticipar una cuota es exactamente lo que la cuota específica toma como anticipo de esa cuota; la cuota vencida se paga, no se anticipa; y la lectura
/// se audita como las otras del contrato.
/// </summary>
public class DetalleDeObligacionTests : IAsyncLifetime
{
    private PagosDePrueba _p = null!;

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private async Task<DetalleDeObligacionDto> DetalleAsync(DateOnly fecha)
    {
        var r = await _p.Contrato().DetalleAsync(new SolicitudDeDetalle(PagosDePrueba.Prueba, _p.Credito.PublicId, fecha), default);
        r.Result.Should().Be(ResultadoDelContrato.Answered, r.Message);
        return r.Value!;
    }

    [Fact]
    public async Task Lo_que_cuesta_anticipar_una_cuota_es_lo_que_toma_la_cuota_especifica()
    {
        var hoy = _p.G.HoyLocal;
        var d = await DetalleAsync(hoy);

        d.Number.Should().Be(_p.Credito.Number);
        d.Status.Should().Be("Vigente");
        d.PayoffAmount.Should().Be(3_000_000m, "se desembolsó hoy: cancelar es el capital");
        d.DueToday.Should().Be(0m);
        d.Installments.Should().HaveCount(24).And.OnlyContain(q => q.Status == "Upcoming");
        d.Balances.Should().ContainSingle(b => b.Concept == "Principal").Which.Name.Should().Be("Capital");
        d.AdmittedModalities.Should().Contain(ModalidadDePago.SpecificInstallment, "las mismas del listado: sin nada exigible se anticipa por número");
        d.AllowsSpecificInstallmentOutOfOrder.Should().BeFalse();
        d.NextDueDate.Should().Be(d.Installments[0].DueDate);

        foreach (var cuota in d.Installments.Take(2))
        {
            cuota.ValueToAnticipate.Should().Be(cuota.Principal + cuota.Interest + cuota.Insurance + cuota.Other - cuota.AdvanceApplied);
            var simulada = await _p.Contrato().SimularAsync(new SolicitudDeSimulacion(PagosDePrueba.Prueba, _p.Credito.PublicId, ModalidadDePago.SpecificInstallment,
                cuota.ValueToAnticipate + 1_000m, hoy, null, cuota.Number), default);
            simulada.Result.Should().Be(ResultadoDelContrato.Answered, simulada.Message);
            simulada.Value!.AdvanceAmount.Should().Be(cuota.ValueToAnticipate, $"la cuota {cuota.Number} completa");
            simulada.Value.Surplus.Should().Be(1_000m);
        }
        d.Installments[1].Interest.Should().BeLessThan(d.Installments[0].Interest, "la 2 causa sobre el saldo después del capital de la 1");
        (await _p.EventosAsync()).Should().Contain(AuditEventTypes.LendingObligationContractDetailed);
    }

    [Fact]
    public async Task La_cuota_vencida_se_paga_no_se_anticipa()
    {
        var primera = (await _p.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync()).DueDate;
        await _p.CausarHastaAsync(primera.AddDays(5));

        var d = await DetalleAsync(_p.G.HoyLocal);

        var cuota1 = d.Installments.Single(q => q.Number == 1);
        cuota1.Status.Should().Be("PastDue");
        cuota1.Owed.Should().BeGreaterThan(0m);
        cuota1.ValueToAnticipate.Should().Be(0m);
        d.DueToday.Should().BeGreaterThanOrEqualTo(cuota1.Owed, "lo exigible incluye lo que debe la cuota vencida");
        d.DaysPastDue.Should().BeGreaterThan(0);
        d.Installments.Single(q => q.Number == 2).Status.Should().Be("Upcoming");
    }

    [Fact]
    public async Task Una_obligacion_que_no_existe_se_rechaza_con_el_codigo_de_Cartera()
    {
        var r = await _p.Contrato().DetalleAsync(new SolicitudDeDetalle(PagosDePrueba.Prueba, Guid.NewGuid(), _p.G.HoyLocal), default);

        r.Result.Should().Be(ResultadoDelContrato.Rejected);
        r.Value.Should().BeNull();
    }
}
