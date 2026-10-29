using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Periods;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ConciliacionBancaria = IngenIA365ERP.Domain.Entities.Treasury.Reconciliation.Reconciliation;

namespace IngenIA365ERP.Application.Tests.Accounting.Periods;

/// <summary>
/// Feature 015, US25, T0273a (E-04; TR41; FR-167, FR-216; decisión del dueño del 2026-10-23, I7): <c>ReopenPeriodCommand</c> marca <c>Outdated</c> las
/// <c>TES_Reconciliations</c> cerradas del mes por <c>IConciliacionBancariaParaContabilidad</c> y no toca <c>ACC_BankReconciliations</c>; las de otro mes y las abiertas no
/// cambian; sin conciliaciones de Tesorería el puerto devuelve cero; una desactualizada se vuelve a conciliar con una versión nueva. Prueba escrita, sin correr.
/// </summary>
public class ReaperturaYConciliacionDeTesoreriaTests
{
    private readonly ContabilidadTestData _d = new();
    private readonly IAuditService _auditoriaDeModulos = Substitute.For<IAuditService>();

    private ConciliacionBancariaParaContabilidad Puerto() =>
        new(_d.Db, new AuditoriaDeTesoreria(new ServiceCollection().AddSingleton(_auditoriaDeModulos).BuildServiceProvider(), NullLogger<AuditoriaDeTesoreria>.Instance),
            _d.Clock);

    private ReopenPeriodCommandHandler Reabridor() =>
        new(_d.Db, _d.Clock, _d.User,
            new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), _d.User, _d.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual),
            Puerto());

    private ConciliacionBancaria Conciliacion(int mes, ReconciliationState estado, int version = 1)
    {
        var c = new ConciliacionBancaria { BankAccountId = 1, PeriodYear = 2026, PeriodMonth = mes, Version = version, ConciliatorUserId = 1, Status = estado };
        _d.Db.TreasuryReconciliations.Add(c);
        _d.Db.SaveChanges();
        return c;
    }

    private async Task CerrarMarzoAsync()
    {
        var marzo = await _d.Db.AccountingPeriods.SingleAsync(p => p.Month == 3);
        marzo.Status = PeriodStatus.Closed;
        await _d.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Reabrir_el_periodo_desactualiza_las_cerradas_del_mes_y_no_toca_las_de_Contabilidad()
    {
        await CerrarMarzoAsync();
        var marzo = await _d.Db.AccountingPeriods.SingleAsync(p => p.Month == 3);
        _d.Db.BankReconciliations.Add(new BankReconciliation { AccountId = _d.Cuenta("111005").Id, PeriodId = marzo.Id, Status = ReconciliationStatus.Closed, CreatedBy = "test" });
        var cerrada = Conciliacion(3, ReconciliationState.Closed);
        var abierta = Conciliacion(3, ReconciliationState.Open, version: 2);
        var deAbril = Conciliacion(4, ReconciliationState.Closed);

        var r = await Reabridor().Handle(new ReopenPeriodCommand(2026, 3, "Llegó una factura tardía"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        (await _d.Db.TreasuryReconciliations.SingleAsync(c => c.Id == cerrada.Id)).Status.Should().Be(ReconciliationState.Outdated);
        (await _d.Db.TreasuryReconciliations.SingleAsync(c => c.Id == cerrada.Id)).OutdatedReason.Should().Be("Llegó una factura tardía");
        (await _d.Db.TreasuryReconciliations.SingleAsync(c => c.Id == abierta.Id)).Status.Should().Be(ReconciliationState.Open);
        (await _d.Db.TreasuryReconciliations.SingleAsync(c => c.Id == deAbril.Id)).Status.Should().Be(ReconciliationState.Closed);
        (await _d.Db.BankReconciliations.SingleAsync()).Status.Should().Be(ReconciliationStatus.Closed, "la conciliación de Contabilidad quedó sin uso (E-04)");
    }

    [Fact]
    public async Task El_puerto_devuelve_cuantas_desactualizo_y_cero_sin_Tesoreria()
    {
        (await Puerto().MarcarDesactualizadasAsync(2026, 3, "Reapertura", CancellationToken.None)).Should().Be(0, "sin conciliaciones de Tesorería no hay nada");

        Conciliacion(3, ReconciliationState.Closed);
        Conciliacion(3, ReconciliationState.Closed, version: 2);
        var n = await Puerto().MarcarDesactualizadasAsync(2026, 3, "Reapertura", CancellationToken.None);
        await _d.Db.SaveChangesAsync();

        n.Should().Be(2, "el evento Accounting.Period.Reopened conserva outdatedReconciliations con este conteo");
        await _auditoriaDeModulos.Received(2).LogAsync(Arg.Any<IngenIA365ERP.Application.Common.Interfaces.AuditLogCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void El_puerto_valida_lo_que_recibe()
    {
        var mesInvalido = () => ConciliacionBancariaParaContabilidad.ValidarPedido(2026, 13, "x");
        var sinMotivo = () => ConciliacionBancariaParaContabilidad.ValidarPedido(2026, 3, " ");

        mesInvalido.Should().Throw<ArgumentOutOfRangeException>();
        sinMotivo.Should().Throw<ArgumentException>();
    }
}
