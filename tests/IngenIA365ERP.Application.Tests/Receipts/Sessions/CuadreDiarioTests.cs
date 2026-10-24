using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Sessions.Tareas;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// Feature 015, T0086 y T0095 (FR-036, FR-185): el cuadre diario compara cajas + bóveda + tránsito contra el saldo contable de la caja más lo emitido y
/// pendiente de lote; la diferencia se muestra, se explica y se alerta (<c>Recaudo.CuadreDiarioConDiferencia</c>); la tarea espera el lote del día (sin
/// contabilidad iniciada no hay lote que esperar). Escrita en la fase 5; se corre al final del plan.
/// </summary>
public class CuadreDiarioTests
{
    private readonly CajaDePrueba _c = new();

    private void Contabilidad(decimal saldo) =>
        _c.Contabilidad.SaldoContableAsync(Arg.Any<SolicitudDeSaldo>(), Arg.Any<CancellationToken>())
            .Returns([new SaldoContableDto(CuadreDiarioDeRecaudo.ConceptoCaja, _c.E.OficinaA.PublicId, null, saldo)]);

    [Fact]
    public async Task Cuadra_cajas_y_boveda_contra_el_saldo_contable()
    {
        await _c.SurtirBovedaAsync((_c.B50, 4));
        await _c.AbrirAsync();
        Contabilidad(700_000);

        var cuadres = await _c.Cuadre().CuadrarAsync(EscenarioDeRecaudo.Hoy, default);

        var cuadre = cuadres.Single(q => q.BranchId == _c.E.OficinaA.Id);
        cuadre.CashInBoxes.Should().Be(500_000);
        cuadre.CashInVault.Should().Be(200_000);
        cuadre.Difference.Should().Be(0);
        cuadre.Status.Should().Be(DailySquareStatus.Squared);
    }

    [Fact]
    public async Task Una_diferencia_se_muestra_se_alerta_y_se_explica()
    {
        await _c.AbrirAsync();
        Contabilidad(450_000);

        var cuadre = (await _c.Cuadre().CuadrarAsync(EscenarioDeRecaudo.Hoy, default)).Single(q => q.BranchId == _c.E.OficinaA.Id);

        cuadre.Difference.Should().Be(50_000);
        cuadre.Status.Should().Be(DailySquareStatus.Difference);
        await _c.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoCuadreDiarioConDiferencia), Arg.Any<CancellationToken>());

        _c.E.Como(_c.E.Supervisor);
        var explicado = await new ExplainDailySquareCommandHandler(_c.E.Db, _c.E.Alcance, _c.E.Actor)
            .Handle(new ExplainDailySquareCommand(cuadre.PublicId, "Recibo de ayer sin lote"), default);
        explicado.Value.Status.Should().Be(DailySquareStatus.Explained);

        Contabilidad(0);
        var otraVez = (await _c.Cuadre().CuadrarAsync(EscenarioDeRecaudo.Hoy, default)).Single(q => q.BranchId == _c.E.OficinaA.Id);
        otraVez.Difference.Should().Be(50_000, "un cuadre explicado no se recalcula");
    }

    [Fact]
    public async Task Sin_saldo_contable_no_inventa_un_cero()
    {
        await _c.AbrirAsync();

        var cuadre = (await _c.Cuadre().CuadrarAsync(EscenarioDeRecaudo.Hoy, default)).Single(q => q.BranchId == _c.E.OficinaA.Id);

        cuadre.LedgerBalance.Should().BeNull();
        cuadre.Explanation.Should().Contain("Sin saldo contable");
    }

    [Fact]
    public async Task La_boveda_que_no_cuadra_por_denominacion_deja_diferencia()
    {
        await _c.SurtirBovedaAsync((_c.B50, 4));
        Contabilidad(200_000);
        var saldo = await _c.E.Db.VaultDenominationBalances.SingleAsync();
        saldo.Quantity = 3;
        await _c.E.Db.SaveChangesAsync();

        var cuadre = (await _c.Cuadre().CuadrarAsync(EscenarioDeRecaudo.Hoy, default)).Single(q => q.BranchId == _c.E.OficinaA.Id);

        cuadre.VaultBalanceMatches.Should().BeFalse();
        cuadre.Status.Should().Be(DailySquareStatus.Difference);
    }

    [Fact]
    public async Task El_cuadre_espera_el_lote_del_dia_con_la_contabilidad_iniciada()
    {
        (await MarcaDelLoteDelDia.TerminoAsync(_c.E.Db, _c.Contabilidad, EscenarioDeRecaudo.Hoy, default)).Should().BeTrue("sin contabilidad no hay lote");

        _c.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, null));
        (await MarcaDelLoteDelDia.TerminoAsync(_c.E.Db, _c.Contabilidad, EscenarioDeRecaudo.Hoy, default)).Should().BeFalse();

        await MarcaDelLoteDelDia.RegistrarAsync(_c.E.Db, EscenarioDeRecaudo.Hoy, default);
        await _c.E.Db.SaveChangesAsync();
        (await MarcaDelLoteDelDia.TerminoAsync(_c.E.Db, _c.Contabilidad, EscenarioDeRecaudo.Hoy, default)).Should().BeTrue();
    }
}
