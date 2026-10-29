using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Liquidity;
using IngenIA365ERP.Application.Treasury.Reports;
using IngenIA365ERP.Domain.Entities.Treasury.Liquidity;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Reports;

/// <summary>
/// Feature 015, T2, fase 45 (T0465; FR-153 parte T2; api.md §17): las vistas de T2 del centro de informes de Tesorería —<c>reconciliations</c>, <c>liquidity</c> y
/// <c>cash-flow</c>— están registradas con las de T0 y T1; la de conciliaciones cuenta los cruces automáticos y manuales y resalta la diferencia; la de liquidez y la del
/// flujo, que son de toda la cooperativa, salen vacías y con la nota para quien no ve todas las cuentas; el flujo trae el neto proyectado contra lo real por mes. Prueba
/// escrita, sin correr.
/// </summary>
public class InformesDeTesoreriaT2Tests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly ISender _sender = Substitute.For<ISender>();

    private TreasuryReportQueryHandler Handler() =>
        new(_t.E.Db, _t.Alcance, _t.Contabilidad, new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _sender, _t.E.Reloj);

    private static FiltrosDeInformeDeRecaudoYTesoreria Filtros() =>
        new() { From = EscenarioDeRecaudo.Hoy.AddDays(-30), To = EscenarioDeRecaudo.Hoy, AsOf = EscenarioDeRecaudo.Hoy };

    [Fact]
    public void Las_trece_vistas_de_T0_T1_y_T2_estan_registradas()
    {
        VistasDeTesoreria.Todas.Should().BeEquivalentTo(
        [
            "orders-by-state", "batches", "rejected-and-reissue", "recent-account-changes", "issued-checks", "bank-balances", "minimal-reconciliation",
            "payment-vouchers", "transfers", "month-end-checks", "reconciliations", "liquidity", "cash-flow",
        ]);
    }

    [Fact]
    public async Task Las_conciliaciones_cuentan_sus_cruces_y_resaltan_la_diferencia()
    {
        var hoy = EscenarioDeRecaudo.Hoy;
        var conciliacion = new IngenIA365ERP.Domain.Entities.Treasury.Reconciliation.Reconciliation
        {
            BankAccountId = _t.Pagos.Id, PeriodYear = hoy.Year, PeriodMonth = hoy.Month, Version = 1, Kind = ReconciliationKind.Complete, StatementBalance = 1_000m,
            LedgerBalance = 900m, Difference = 100m, ConciliatorUserId = _t.E.Cajera.Id,
        };
        _t.E.Db.TreasuryReconciliations.Add(conciliacion);
        _t.E.Db.SaveChanges();
        _t.E.Db.TreasuryReconciliationMatches.AddRange(
            new ReconciliationMatch { ReconciliationId = conciliacion.Id, StatementLineId = 1, RecordKind = ReconciliationRecordKind.BankNote, RecordPublicId = Guid.NewGuid(),
                Amount = 10m, Mode = ReconciliationMatchMode.Automatic, Rule = "VALFEC", GroupKey = Guid.NewGuid(), MatchedByUserId = _t.E.Cajera.Id, MatchedAt = DateTime.UtcNow },
            new ReconciliationMatch { ReconciliationId = conciliacion.Id, StatementLineId = 2, RecordKind = ReconciliationRecordKind.AcquirerSettlement,
                RecordPublicId = Guid.NewGuid(), Amount = 20m, Mode = ReconciliationMatchMode.Manual, Rule = "Manual", GroupKey = Guid.NewGuid(),
                MatchedByUserId = _t.E.Cajera.Id, MatchedAt = DateTime.UtcNow });
        _t.E.Db.SaveChanges();

        var r = await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.Conciliaciones, Filtros()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var fila = r.Value.Filas.Single();
        fila.Resaltada.Should().BeTrue();
        fila.Valores[2].Should().Be("Completa");
        (fila.Valores[7], fila.Valores[8], fila.Valores[9], fila.Valores[10]).Should().Be((1, 1, 1, 1));
    }

    [Fact]
    public async Task La_liquidez_y_el_flujo_son_para_quien_ve_todas_las_cuentas()
    {
        _t.E.Db.TreasuryLiquiditySnapshots.Add(new LiquiditySnapshot { Number = "LQ-1", Date = EscenarioDeRecaudo.Hoy, TakenAt = DateTime.UtcNow, TakenByUserId = _t.E.Cajera.Id });
        _t.E.Db.SaveChanges();
        _t.Alcance.Valor = new AlcanceDeTesoreria(false, new HashSet<int> { _t.Pagos.Id });

        var liquidez = await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.Liquidez, Filtros()), default);
        var flujo = await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.FlujoDeCaja, Filtros()), default);

        liquidez.Value.Filas.Should().BeEmpty();
        flujo.Value.Filas.Should().BeEmpty();
        await _sender.DidNotReceive().Send(Arg.Any<GetCashFlowQuery>(), Arg.Any<CancellationToken>());

        _t.Alcance.Valor = AlcanceDeTesoreria.Todo;
        (await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.Liquidez, Filtros()), default)).Value.Filas.Should().ContainSingle();
    }

    [Fact]
    public async Task El_flujo_trae_el_neto_proyectado_contra_lo_real_por_mes()
    {
        var hoy = EscenarioDeRecaudo.Hoy;
        var inicio = new DateOnly(hoy.Year, hoy.Month, 1);
        var categoria = Guid.NewGuid();
        _sender.Send(Arg.Any<GetCashFlowQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new CashFlowDto(CashFlowPeriodKind.Month,
        [
            new PeriodoDeFlujoDto(inicio, inicio.AddMonths(1).AddDays(-1),
                [new LineaDeFlujoDto(categoria, "ORDENES", "Órdenes de pago", CashFlowDirection.Outflow, CashFlowCategorySource.PaymentOrders, 400m, true)],
                1_000m, 400m, 900m, 500m),
        ], [])));

        var r = await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.FlujoDeCaja,
            new FiltrosDeInformeDeRecaudoYTesoreria { From = inicio, To = inicio, AsOf = hoy }), default);

        var neto = r.Value.Filas.Single(f => f.Resaltada);
        (neto.Valores[4], neto.Valores[6], neto.Valores[7]).Should().Be((600m, 400m, -200m));
    }
}
