using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Periods;
using IngenIA365ERP.Application.Common.Cuadres;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Lending;
using IngenIA365ERP.Application.Tests.Treasury;
using IngenIA365ERP.Application.Treasury.Activation;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using ConciliacionBancaria = IngenIA365ERP.Domain.Entities.Treasury.Reconciliation.Reconciliation;

namespace IngenIA365ERP.Application.Tests.Accounting.Periods;

/// <summary>
/// Feature 015, T1, T0420 (FR-132; D29; E-04): <c>IEstadoOperativoParaElCierre</c> informa las conciliaciones abiertas (<c>Accounting.Period.ReconciliationsOpen</c>) y las
/// sesiones sin cerrar (<c>.CashSessionsOpen</c>); por defecto son un aviso que se reconoce y con <c>Contabilidad.CierreExigeConciliaciones</c> bloquean. Prueba escrita, sin
/// correr.
/// </summary>
public class AvisoDeCierreContableTests
{
    private readonly EscenarioContableDeCartera _c = new();
    private readonly EscenarioDeTesoreria _t = new();

    private Task<Result> CerrarMarzoAsync(bool reconocer, params IEstadoOperativoParaElCierre[] estados) =>
        new ClosePeriodCommandHandler(_c.D.Db, _c.D.Clock, _c.D.User, _c.Emisor, estados).Handle(new ClosePeriodCommand(2026, 3, AcknowledgeOperationalPending: reconocer), default);

    private static IEstadoOperativoParaElCierre Estado(params PendienteDeCierreDto[] pendientes)
    {
        var estado = Substitute.For<IEstadoOperativoParaElCierre>();
        estado.PendientesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(pendientes);
        return estado;
    }

    private void ConciliacionAbierta(int anio, int mes)
    {
        _t.E.Db.TreasuryReconciliations.Add(new ConciliacionBancaria
        {
            BankAccountId = _t.Pagos.Id, PeriodYear = anio, PeriodMonth = mes, Version = 1, Status = ReconciliationState.Open, ConciliatorUserId = _t.E.Cajera.Id,
        });
        _t.E.Db.SaveChanges();
    }

    [Fact]
    public async Task Tesoreria_informa_las_conciliaciones_abiertas_del_mes_como_aviso_por_defecto()
    {
        ConciliacionAbierta(2026, 11);

        var pendientes = await new EstadoOperativoDeTesoreriaParaElCierre(_t.E.Db, _t.E.Lector).PendientesAsync(2026, 11, default);

        pendientes.Should().ContainSingle(p => p.Code == AvisoOperativoDelCierre.CodigoConciliacionesAbiertas && !p.Blocks);
        (await new EstadoOperativoDeTesoreriaParaElCierre(_t.E.Db, _t.E.Lector).PendientesAsync(2026, 10, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Con_la_cooperativa_que_exige_conciliaciones_el_aviso_bloquea()
    {
        ConciliacionAbierta(2026, 11);
        _t.E.Parametro(ParametrosDeRecaudoYTesoreria.ContabilidadCierreExigeConciliaciones, "true", ParametrosDeRecaudoYTesoreria.ModuloTesoreria);

        var pendientes = await new EstadoOperativoDeTesoreriaParaElCierre(_t.E.Db, _t.E.Lector).PendientesAsync(2026, 11, default);

        pendientes.Should().ContainSingle(p => p.Code == AvisoOperativoDelCierre.CodigoConciliacionesAbiertas && p.Blocks);
    }

    [Fact]
    public async Task El_aviso_se_reconoce_y_el_bloqueo_no()
    {
        var aviso = new PendienteDeCierreDto("TES", AvisoOperativoDelCierre.CodigoConciliacionesAbiertas, "Conciliaciones abiertas.", Blocks: false);
        var bloqueo = new PendienteDeCierreDto("REC", AvisoOperativoDelCierre.CodigoSesionesSinCerrar, "Sesiones sin cerrar.", Blocks: true);

        (await CerrarMarzoAsync(false, Estado(aviso))).Error.Code.Should().Be(AvisoOperativoDelCierre.CodigoConciliacionesAbiertas);
        (await CerrarMarzoAsync(true, Estado(bloqueo))).Error.Code.Should().Be(AvisoOperativoDelCierre.CodigoSesionesSinCerrar);
        (await CerrarMarzoAsync(true, Estado(aviso))).IsSuccess.Should().BeTrue();
        (await _c.D.Db.AccountingPeriods.SingleAsync(p => p.StartDate == new DateOnly(2026, 3, 1))).Status.Should().Be(PeriodStatus.Closed);
    }
}
