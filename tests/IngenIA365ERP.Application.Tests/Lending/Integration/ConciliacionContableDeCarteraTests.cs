using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US7, T0628 (contabilidad.md §14.2; T58, FR-130, SC-006, C51): la conciliación de Cartera con Contabilidad por conjuntos de
/// cuentas. Cada diferencia se explica hecho por hecho con los <c>Pending</c>, <c>InBatch</c>, <c>Rejected</c> y <c>NotApplicable</c>; con cero
/// pendientes la diferencia es cero; en un puente lo que los hechos no explican es «del otro lado»; un descuadre sin explicar alerta
/// <c>Cartera.DescuadreContable</c> una vez por corte y conjunto; filtra por el alcance; sin contabilidad, <c>Lending.Accounting.NotStarted</c>.
/// Sobre un desembolso real por transferencia y un doble del lado contable (<see cref="ContabilidadDePruebaParaConciliar"/>).
/// </summary>
public class ConciliacionContableDeCarteraTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private ContabilidadDePruebaParaConciliar _contable = null!;
    private readonly List<AlertaALevantar> _alertas = [];
    private readonly IAlertas _alertador = Substitute.For<IAlertas>();
    private static readonly DateOnly Hoy = DesembolsoDePrueba.Hoy;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _contable = new ContabilidadDePruebaParaConciliar(_d.Contabilidad, "Capital", "DesembolsoPorGirar", "RecaudoPorAplicar", "PuenteComercial",
            "CuentaPorCobrarComercial");
        _alertador.LevantarAsync(Arg.Do<AlertaALevantar>(_alertas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private ConciliacionContableDeCartera Conciliacion() => new(_d.Db, new SaldosContablesDeCartera(_d.Db, _d.Contabilidad), _d.Contabilidad, _alertador);

    private Task<Result<ConciliacionContableDto>> ConciliarAsync(AlcanceDeCartera? alcance = null) =>
        Conciliacion().ConciliarAsync(Hoy, alcance ?? AlcanceDeCartera.Total, default);

    private async Task<(DisbursementResult R, Guid Sucursal)> DesembolsarAsync()
    {
        var s = await _d.AprobadaAsync();
        var r = await _d.DesembolsadoAsync(_d.Entrada(s));
        var oficina = await _d.Db.Loans.AsNoTracking().Where(l => l.PublicId == r.LoanPublicId).Select(l => l.BranchId).SingleAsync();
        var sucursal = await _d.Db.Branches.AsNoTracking().Where(b => b.Id == oficina).Select(b => b.PublicId).SingleAsync();
        return (new DisbursementResult(r.GrossAmount, r.NetToDisburse, r.Messages.Single(m => m.Type == "CreditoDesembolsado").MessagePublicId), sucursal);
    }

    private sealed record DisbursementResult(decimal Bruto, decimal Neto, Guid Hecho);

    private async Task MarcarAsync(Guid hecho, DeliveryStatus estado)
    {
        var entrega = await _d.Db.IntegrationMessageDeliveries.SingleAsync(x => x.Message!.PublicId == hecho && x.Destination == IntegrationDestinations.Accounting);
        entrega.Status = estado;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Un_hecho_pendiente_explica_toda_la_diferencia_hecho_por_hecho()
    {
        var (r, _) = await DesembolsarAsync();

        var c = (await ConciliarAsync()).Value;

        var capital = c.Sets.Single(s => s.Family == "Capital");
        (capital.LendingBalance, capital.AccountingBalance, capital.Difference).Should().Be((r.Bruto, 0m, r.Bruto));
        capital.ExplainedBy.Should().Be(new ExplicacionPorEstadoDto(r.Bruto, 0m, 0m, 0m));
        capital.Facts.Should().ContainSingle().Which.Should().BeEquivalentTo(new { MessagePublicId = r.Hecho, Type = "CreditoDesembolsado", Amount = r.Bruto });
        capital.Unexplained.Should().Be(0m);
        c.UnexplainedTotal.Should().Be(0m);
        _alertas.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DeliveryStatus.InBatch)]
    [InlineData(DeliveryStatus.Rejected)]
    [InlineData(DeliveryStatus.NotApplicable)]
    public async Task Los_tres_otros_estados_sin_procesar_tambien_explican(DeliveryStatus estado)
    {
        var (r, _) = await DesembolsarAsync();
        await MarcarAsync(r.Hecho, estado);

        var capital = (await ConciliarAsync()).Value.Sets.Single(s => s.Family == "Capital");

        capital.Unexplained.Should().Be(0m);
        var porEstado = capital.ExplainedBy;
        (estado switch
        {
            DeliveryStatus.InBatch => porEstado.InBatch,
            DeliveryStatus.Rejected => porEstado.Rejected,
            _ => porEstado.NotPosted,
        }).Should().Be(r.Bruto);
    }

    [Fact]
    public async Task Con_cero_pendientes_la_diferencia_es_cero()
    {
        var (r, sucursal) = await DesembolsarAsync();
        await MarcarAsync(r.Hecho, DeliveryStatus.Processed);
        _contable.Saldo("Capital", sucursal, r.Bruto);
        _contable.Saldo("DesembolsoPorGirar", sucursal, -r.Neto);

        var c = (await ConciliarAsync()).Value;

        c.Sets.Should().OnlyContain(s => s.Difference == 0m && s.Unexplained == 0m);
        c.Sets.Single(s => s.Family == "DesembolsoPorGirar").LendingBalance.Should().Be(r.Neto, "la orden de giro sigue emitida");
        _alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_descuadre_sin_explicar_alerta_una_vez_por_corte_y_conjunto()
    {
        var (r, sucursal) = await DesembolsarAsync();
        await MarcarAsync(r.Hecho, DeliveryStatus.Processed);
        _contable.Saldo("Capital", sucursal, r.Bruto - 1_000m);
        _contable.Saldo("DesembolsoPorGirar", sucursal, -r.Neto);

        var capital = (await ConciliarAsync()).Value.Sets.Single(s => s.Family == "Capital");

        capital.Unexplained.Should().Be(1_000m);
        capital.UnexplainedIsOtherSide.Should().BeFalse();
        var alerta = _alertas.Should().ContainSingle().Subject;
        alerta.TypeCode.Should().Be(TiposDeAlerta.DescuadreContable);
        alerta.DedupKey.Should().Be($"Cartera.DescuadreContable:{Hoy:yyyy-MM-dd}:Capital:C-Capital");
        alerta.RecipientPermissions.Should().Equal("Accounting.LendingRules.Manage", "Lending.Messages.Reprocess");
    }

    [Fact]
    public async Task En_un_puente_lo_que_los_hechos_no_explican_es_del_otro_lado_y_no_alerta()
    {
        var sucursal = await _d.Db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => b.PublicId).FirstAsync();
        _contable.Saldo("RecaudoPorAplicar", sucursal, -500m);

        var c = (await ConciliarAsync()).Value;

        var puente = c.Sets.Single(s => s.Family == "RecaudoPorAplicar");
        (puente.LendingBalance, puente.AccountingBalance, puente.Unexplained, puente.UnexplainedIsOtherSide).Should().Be((0m, -500m, 500m, true));
        c.Bridges.Should().ContainSingle(b => b.CollectorClass == "OwnLoanPayoff");
        c.UnexplainedTotal.Should().Be(0m, "lo del otro lado no es un descuadre de Cartera");
        _alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task Las_combinaciones_sin_regla_se_listan_aparte()
    {
        _contable = new ContabilidadDePruebaParaConciliar(_d.Contabilidad, "DesembolsoPorGirar");
        var (r, _) = await DesembolsarAsync();

        var c = (await ConciliarAsync()).Value;

        c.UnresolvedCombinations.Should().ContainSingle(x => x.Role == "Capital").Which.LendingBalance.Should().Be(r.Bruto);
    }

    [Fact]
    public async Task Filtra_por_el_alcance_de_oficinas()
    {
        await DesembolsarAsync();

        var c = (await ConciliarAsync(AlcanceDeCartera.Vacio)).Value;

        c.Sets.Where(s => s.Family == "Capital").Should().BeEmpty("sin oficinas en su alcance no ve los créditos");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_no_hay_con_que_conciliar()
    {
        _d.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null)));

        (await ConciliarAsync()).Error.Code.Should().Be("Lending.Accounting.NotStarted");
    }
}
