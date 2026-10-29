using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Liquidity;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Entities.Treasury.Liquidity;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Treasury.Liquidity;

/// <summary>
/// Feature 015, T2, fase 43 (T0457; FR-137, FR-138; US20): la posición del día es exactamente la suma de sus componentes con su signo —bancos en libros, bóvedas y cajas de
/// Recaudo, compromisos (lotes aprobados, órdenes en bandeja, dispersiones de Nómina), cheques en tránsito informativos— y lo esperado de Cartera y Captaciones entra sólo
/// en lo proyectado; si una consulta no responde, la posición sale sin ese componente y con el aviso. El flujo toma lo programado de las categorías automáticas, lo digitado
/// manda sobre lo calculado y lo real son los movimientos contabilizados de las cuentas bancarias. Prueba escrita, sin correr.
/// </summary>
public class PosicionYFlujoTests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly IEfectivoDeRecaudo _efectivo = Substitute.For<IEfectivoDeRecaudo>();
    private readonly IRecaudosEsperadosDeCartera _cartera = Substitute.For<IRecaudosEsperadosDeCartera>();
    private readonly IRecaudosEsperadosDeCaptaciones _captaciones = Substitute.For<IRecaudosEsperadosDeCaptaciones>();

    private static DateOnly Hoy => EscenarioDeRecaudo.Hoy;

    public PosicionYFlujoTests()
    {
        _t.Contabilidad.SaldoEnLibrosAsync(_t.Pagos.PublicId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(10_000_000m);
        _t.Contabilidad.SaldoEnLibrosAsync(_t.FondoDeLiquidez.PublicId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(5_000_000m);
        _efectivo.SaldosAsync(Arg.Any<CancellationToken>()).Returns(new List<EfectivoEnCustodiaDto>
        {
            new(_t.E.OficinaA.PublicId, "Centro", IEfectivoDeRecaudo.ClaseBoveda, "Bóveda Centro", 2_000_000m),
            new(_t.E.OficinaA.PublicId, "Centro", IEfectivoDeRecaudo.ClaseSesion, "Caja 1", 500_000m),
        });
        _cartera.PorFechaAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<RecaudoEsperadoDelDiaDto> { new(Hoy, 4, 800_000m), new(Hoy.AddDays(8), 2, 300_000m) });
        _captaciones.PorFechaAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Throws(new TimeoutException("sin respuesta"));
    }

    private PosicionDeLiquidez Posicion(bool conPuertos = true) => conPuertos
        ? new PosicionDeLiquidez(_t.E.Db, _t.Contabilidad, NullLogger<PosicionDeLiquidez>.Instance, _efectivo, _cartera, _captaciones)
        : new PosicionDeLiquidez(_t.E.Db, _t.Contabilidad, NullLogger<PosicionDeLiquidez>.Instance);

    private void Compromisos()
    {
        _t.Orden(1_000_000m, PaymentOrderState.Ready);
        _t.Orden(3_000_000m, PaymentOrderState.Approved);
        _t.Orden(9_000_000m, PaymentOrderState.Disbursed);
        _t.E.Db.TreasuryPayrollDispersions.Add(new PayrollDispersion
        {
            MessagePublicId = Guid.NewGuid(), FilePublicId = Guid.NewGuid(), Total = 700_000m, LineCount = 3, PayDate = Hoy, ReceivedAt = DateTime.UtcNow,
        });
        var chequera = new Checkbook { BankAccountId = _t.Pagos.Id, FromNumber = 1, ToNumber = 50, NextCheckNumber = 2, CustodianUserId = _t.E.Cajera.Id, RegisteredByUserId = _t.E.Cajera.Id };
        _t.E.Db.Checkbooks.Add(chequera);
        _t.E.Db.SaveChanges();
        var orden = _t.Orden(400_000m, PaymentOrderState.Disbursed, PaymentForm.Check);
        _t.E.Db.IssuedChecks.Add(new IssuedCheck
        {
            CheckbookId = chequera.Id, CheckNumber = 1, PaymentOrderId = orden.Id, BeneficiaryPersonId = _t.Beneficiaria.Id, Amount = 400_000m, IssuedOn = Hoy.AddDays(-2),
            DeliveredOn = Hoy.AddDays(-1), Status = IssuedCheckState.Delivered,
        });
        _t.E.Db.SaveChanges();
    }

    [Fact]
    public async Task La_posicion_es_la_suma_de_sus_componentes_con_su_signo()
    {
        Compromisos();

        var p = await Posicion().CalcularAsync(Hoy, Hoy, default);

        (p.Banks, p.Vaults, p.CashSessions).Should().Be((15_000_000m, 2_000_000m, 500_000m));
        p.Commitments.Should().Be(4_700_000m, "lotes aprobados 3.000.000, bandeja 1.000.000 y nómina 700.000; el cheque en tránsito es informativo");
        p.Available.Should().Be(15_000_000m + 2_000_000m + 500_000m - 4_700_000m);
        p.Available.Should().Be(p.Components.Where(c => !c.OnlyProjected).Sum(c => c.Sign * c.Amount));
        p.Components.Single(c => c.Code == PosicionDeLiquidez.CodigoCheques).Sign.Should().Be(0);
        p.ExpectedLending.Should().Be(800_000m);
        p.Projected.Should().Be(p.Available + 800_000m);
        p.LiquidityFund.Should().Be(5_000_000m);
        p.FundAccounts.Should().ContainSingle(f => f.OwnBankAccountPublicId == _t.FondoDeLiquidez.PublicId && f.Balance == 5_000_000m);
    }

    [Fact]
    public async Task Sin_respuesta_de_una_consulta_la_posicion_sale_sin_ese_componente_y_con_el_aviso()
    {
        var p = await Posicion().CalcularAsync(Hoy, Hoy, default);

        p.ExpectedDeposits.Should().BeNull();
        p.Missing.Should().ContainSingle(m => m.Code == PosicionDeLiquidez.CodigoCaptaciones);
        p.Components.Should().NotContain(c => c.Code == PosicionDeLiquidez.CodigoCaptaciones);
    }

    [Fact]
    public async Task Sin_los_puertos_faltan_el_efectivo_y_los_recaudos_esperados()
    {
        var p = await Posicion(conPuertos: false).CalcularAsync(Hoy, Hoy, default);

        p.Missing.Select(m => m.Code).Should().BeEquivalentTo([PosicionDeLiquidez.CodigoEfectivo, PosicionDeLiquidez.CodigoCartera, PosicionDeLiquidez.CodigoCaptaciones]);
        p.Projected.Should().Be(p.Available);
    }

    [Fact]
    public async Task El_flujo_toma_lo_programado_lo_digitado_manda_y_lo_real_sale_de_los_libros()
    {
        _t.E.Como(_t.E.Cajera);
        var ordenes = new CashFlowCategory { Code = "ORDENES", Name = "Órdenes de pago", Direction = CashFlowDirection.Outflow, Source = CashFlowCategorySource.PaymentOrders };
        var cartera = new CashFlowCategory { Code = "CARTERA", Name = "Cuotas de Cartera", Direction = CashFlowDirection.Inflow, Source = CashFlowCategorySource.ExpectedLending };
        var otros = new CashFlowCategory { Code = "OTROS", Name = "Otros egresos", Direction = CashFlowDirection.Outflow, Source = CashFlowCategorySource.Manual };
        _t.E.Db.TreasuryCashFlowCategories.AddRange(ordenes, cartera, otros);
        _t.E.Db.SaveChanges();
        _t.Orden(1_000_000m);
        var siguiente = _t.Orden(250_000m);
        siguiente.PayDate = Hoy.AddDays(7);
        _t.E.Db.SaveChanges();
        var guardar = new SaveCashFlowProjectionCommandHandler(_t.E.Db, _t.E.Actor, new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance));
        (await guardar.Handle(new SaveCashFlowProjectionCommand(otros.PublicId, CashFlowPeriodKind.Week, Hoy, 300_000m) { OperationKey = Guid.NewGuid() }, default))
            .IsSuccess.Should().BeTrue();
        _t.Contabilidad.MovimientosDeCuentaBancariaAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MovimientoContableDeCuentaDto>());
        _t.Contabilidad.MovimientosDeCuentaBancariaAsync(_t.Pagos.PublicId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MovimientoContableDeCuentaDto> { new(Guid.NewGuid(), "RC-1", Hoy, 900_000m, 100_000m, "Movimiento", "REC") });

        var r = await new GetCashFlowQueryHandler(_t.E.Db, _t.Contabilidad, _t.Parametros, _t.E.Reloj, NullLogger<GetCashFlowQueryHandler>.Instance, _cartera, _captaciones)
            .Handle(new GetCashFlowQuery(CashFlowPeriodKind.Week, Hoy, 2), default);

        r.IsSuccess.Should().BeTrue();
        var (primera, segunda) = (r.Value.Periods[0], r.Value.Periods[1]);
        primera.Start.Should().Be(LiquidezDeTesoreria.InicioDelPeriodo(CashFlowPeriodKind.Week, Hoy));
        primera.Lines.Single(l => l.Code == "ORDENES").Should().Match<LineaDeFlujoDto>(l => l.Projected == 1_000_000m && l.Automatic);
        primera.Lines.Single(l => l.Code == "OTROS").Should().Match<LineaDeFlujoDto>(l => l.Projected == 300_000m && !l.Automatic);
        primera.Lines.Single(l => l.Code == "CARTERA").Projected.Should().Be(800_000m);
        (primera.RealInflows, primera.RealOutflows).Should().Be((900_000m, 100_000m));
        segunda.Lines.Single(l => l.Code == "ORDENES").Projected.Should().Be(250_000m);
        segunda.Lines.Single(l => l.Code == "CARTERA").Projected.Should().Be(300_000m);
        segunda.RealInflows.Should().BeNull("la semana todavía no empieza");
    }

    [Fact]
    public void Una_categoria_automatica_lleva_el_sentido_de_su_fuente()
    {
        var v = new SaveCashFlowCategoryCommandValidator();
        v.Validate(new SaveCashFlowCategoryCommand(null, "CARTERA", "Cuotas", CashFlowDirection.Outflow, CashFlowCategorySource.ExpectedLending, 1)).IsValid.Should().BeFalse();
        v.Validate(new SaveCashFlowCategoryCommand(null, "CARTERA", "Cuotas", CashFlowDirection.Inflow, CashFlowCategorySource.ExpectedLending, 1)).IsValid.Should().BeTrue();
        v.Validate(new SaveCashFlowCategoryCommand(null, "OTROS", "Otros", CashFlowDirection.Outflow, CashFlowCategorySource.Manual, 1)).IsValid.Should().BeTrue();
    }
}
