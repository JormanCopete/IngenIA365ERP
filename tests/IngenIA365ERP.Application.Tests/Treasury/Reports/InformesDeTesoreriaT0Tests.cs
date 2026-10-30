using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Liquidity;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Reports;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Treasury.Reports;

/// <summary>
/// Feature 015, US21, T0312 (FR-153 parte T0; api.md §17): las siete vistas de T0 del centro de informes de Tesorería están registradas y responden con alcance por
/// cuenta propia; <c>bank-balances</c> pregunta el saldo en libros a Contabilidad por el puerto y resalta la diferencia con el extracto; <c>rejected-and-reissue</c>
/// muestra la orden que reemplazó a la rechazada; <c>recent-account-changes</c> se reenvía a su consulta de la fase 9. Prueba escrita, sin correr.
/// </summary>
public class InformesDeTesoreriaT0Tests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly ISender _sender = Substitute.For<ISender>();

    public InformesDeTesoreriaT0Tests()
    {
        // La vista cash-flow (T2) pide el flujo por el mediador: aquí, un flujo vacío (corrida del 2026-10-26; InformesDeTesoreriaT2Tests prueba el lleno).
        _sender.Send(Arg.Any<GetCashFlowQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new CashFlowDto(CashFlowPeriodKind.Month, [], [])));
    }

    private TreasuryReportQueryHandler Handler() =>
        new(_t.E.Db, _t.Alcance, _t.Contabilidad, new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _sender, _t.E.Reloj);

    private static FiltrosDeInformeDeRecaudoYTesoreria Filtros(Guid? cuenta = null) =>
        new() { From = EscenarioDeRecaudo.Hoy.AddDays(-30), To = EscenarioDeRecaudo.Hoy, AsOf = EscenarioDeRecaudo.Hoy, Account = cuenta };

    [Fact]
    public void Las_siete_vistas_de_T0_estan_registradas()
    {
        // T1 (fase 35) y T2 (fase 45) suman las suyas: las de T0 siguen todas (InformesDeTesoreriaT2Tests fija la lista completa).
        VistasDeTesoreria.Todas.Should().Contain(
            ["orders-by-state", "batches", "rejected-and-reissue", "recent-account-changes", "issued-checks", "bank-balances", "minimal-reconciliation"]);
    }

    public static TheoryData<string> Claves()
    {
        var d = new TheoryData<string>();
        foreach (var v in VistasDeTesoreria.Todas.Where(v => v != VistasDeTesoreria.CambiosRecientesDeCuenta)) d.Add(v);
        return d;
    }

    [Theory]
    [MemberData(nameof(Claves))]
    public async Task Cada_vista_responde_su_tabla(string vista)
    {
        var r = await Handler().Handle(new TreasuryReportQuery(vista, Filtros()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Columnas.Where(c => c.Clave is not null).Should().OnlyContain(c => c.Clave!.StartsWith('_'));
    }

    [Fact]
    public async Task Los_cambios_recientes_se_reenvian_a_su_consulta()
    {
        var tabla = TablaExportable.Vacia("x", "y", [new ColumnaExportable("A")]);
        _sender.Send(Arg.Any<RecentAccountChangesQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(tabla));

        var r = await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.CambiosRecientesDeCuenta, Filtros()), default);

        r.Value.Should().BeSameAs(tabla);
    }

    [Fact]
    public async Task Los_saldos_bancarios_salen_de_Contabilidad_y_resaltan_la_diferencia_con_el_extracto()
    {
        _t.Pagos.LastStatementBalance = 1_200_000m;
        _t.Pagos.LastStatementDate = EscenarioDeRecaudo.Hoy.AddDays(-1);
        _t.E.Db.SaveChanges();
        _t.Contabilidad.SaldoEnLibrosAsync(_t.Pagos.PublicId, EscenarioDeRecaudo.Hoy, Arg.Any<CancellationToken>()).Returns(1_000_000m);

        var r = await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.SaldosBancarios, Filtros(_t.Pagos.PublicId)), default);

        var fila = r.Value.Filas.Should().ContainSingle().Subject;
        fila.Valores[4].Should().Be(1_000_000m);
        fila.Valores[7].Should().Be(200_000m);
        fila.Resaltada.Should().BeTrue();
    }

    [Fact]
    public async Task Una_cuenta_fuera_del_alcance_no_aparece()
    {
        _t.Alcance.Valor = new AlcanceDeTesoreria(false, new HashSet<int> { _t.FondoDeLiquidez.Id });

        var r = await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.SaldosBancarios, Filtros()), default);

        r.Value.Filas.Should().ContainSingle().Which.Valores[0].Should().Be("FONDO · Fondo de liquidez");
    }

    [Fact]
    public async Task La_rechazada_muestra_la_orden_que_la_reemplazo()
    {
        var rechazada = _t.Orden(estado: PaymentOrderState.Rejected, numero: "OP-R");
        var nueva = _t.Orden(numero: "OP-N");
        nueva.ReplacesOrderId = rechazada.Id;
        _t.E.Db.DisbursementResults.Add(new DisbursementResult
        {
            PaymentOrderId = rechazada.Id, Outcome = DisbursementOutcome.Rejected, OccurredOn = EscenarioDeRecaudo.Hoy, Amount = rechazada.NetAmount, TreasuryReference = "X",
            Reason = "Cuenta inexistente", RecordedByName = "test", RecordedAt = DateTime.UtcNow,
        });
        _t.E.Db.SaveChanges();

        var r = await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.RechazadasYRegiro, Filtros()), default);

        var fila = r.Value.Filas.Should().ContainSingle().Subject;
        fila.Valores[2].Should().Be("OP-R");
        fila.Valores[9].Should().Be("OP-N");
        fila.Resaltada.Should().BeFalse();
    }

    [Fact]
    public async Task Las_ordenes_vivas_se_agrupan_por_estado_con_su_antiguedad()
    {
        _t.Orden(estado: PaymentOrderState.Pending);
        _t.Orden(estado: PaymentOrderState.Disbursed);

        var r = await Handler().Handle(new TreasuryReportQuery(VistasDeTesoreria.OrdenesPorEstado, Filtros()), default);

        r.Value.Filas.Should().ContainSingle("la girada ya no está viva").Which.Valores[0].Should().Be("Pending");
    }
}
