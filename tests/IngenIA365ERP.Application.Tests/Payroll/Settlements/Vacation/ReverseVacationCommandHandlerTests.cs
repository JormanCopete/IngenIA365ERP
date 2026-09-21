using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Settlements.Vacation;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Tests.Payroll.Vacations;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Vacation;

/// <summary>
/// Feature 010 US4 (T056): reversar deja el asiento espejo, devuelve el movimiento a <c>Registered</c>
/// y anula las novedades de ausencia que aún no consumió ninguna nómina aprobada; descartar el
/// borrador anula el movimiento; anular un movimiento con borrador lo descarta en la misma acción, y
/// uno ya liquidado exige reversar la corrida.
/// </summary>
public class ReverseVacationCommandHandlerTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);
    private static readonly DateOnly Desde = new(2026, 7, 27);
    private static readonly DateOnly Hasta = new(2026, 8, 5);

    private static async Task<(NominaTestData D, Guid RunId, Guid MovementId)> AprobadaAsync()
    {
        var d = VacacionesDePrueba.Escenario(hoy: new DateTime(2026, 7, 26, 12, 0, 0));
        for (var mes = 1; mes <= 6; mes++)
            d.MesAprobado(2026, mes, d.Ana, 2_000_000m, 249_095m, provPrima: 187_424m, provCesantias: 187_424m, provIntereses: 22_491m, provVacaciones: 83_333m);
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        d.Periodo(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), PayPeriodStatus.Open);
        LiquidacionDePrueba.ConContabilidad(d);
        d.PeriodoContable(2026, 8);
        var calculo = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);
        calculo.IsSuccess.Should().BeTrue(calculo.Error.Message);
        var aprobada = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(calculo.Value.RunPublicId, Confirm: true), CancellationToken.None);
        aprobada.IsSuccess.Should().BeTrue(aprobada.Error.Message);
        return (d, calculo.Value.RunPublicId, calculo.Value.MovementPublicId);
    }

    [Fact]
    public async Task Reversar_deja_el_espejo_devuelve_el_movimiento_a_Registered_y_anula_las_novedades_de_periodos_abiertos()
    {
        var (d, runId, movementId) = await AprobadaAsync();

        var r = await VacacionesDePrueba.Reversar(d, Contadora).Handle(new ReverseVacationCommand(runId, "Fechas mal registradas"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.ReversalNumber.Should().StartWith("NM");
        var run = await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId);
        run.Status.Should().Be(PayrollRunStatus.Reversed);
        run.ReversalAccountingDocumentId.Should().NotBeNull();

        var movimiento = await d.Db.VacationMovements.SingleAsync(m => m.PublicId == movementId);
        movimiento.Status.Should().Be(VacationMovementStatus.Registered);
        movimiento.PayrollRunId.Should().BeNull();

        var novedades = await d.Db.PayrollNovelties.Where(n => n.VacationMovementId == movimiento.Id).ToListAsync();
        novedades.Should().HaveCount(2).And.OnlyContain(n => n.Status == NoveltyStatus.Cancelled && n.StatusReason!.StartsWith(VacationNoveltyPlanner.AnuladaPorReversion));

        // Tras reversar se admite otra liquidación del mismo movimiento (recalcular sobre la reversada no: se registra de nuevo).
        var otra = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);
        otra.Error.Code.Should().Be("Payroll.Vacation.Overlaps", "el movimiento reversado sigue vivo y ocupa las fechas: anúlelo antes de registrar otro");
    }

    [Fact]
    public async Task La_novedad_de_un_periodo_ya_aprobado_no_se_anula_al_reversar()
    {
        var (d, runId, movementId) = await AprobadaAsync();
        var julio = await d.Db.PayPeriods.SingleAsync(p => p.StartDate == new DateTime(2026, 7, 1));
        julio.Status = PayPeriodStatus.Approved;
        await d.Db.SaveChangesAsync();

        var r = await VacacionesDePrueba.Reversar(d, Contadora).Handle(new ReverseVacationCommand(runId, "Reversión con julio ya pagado"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var movimiento = await d.Db.VacationMovements.SingleAsync(m => m.PublicId == movementId);
        var novedades = await d.Db.PayrollNovelties.Include(n => n.PayPeriod).Where(n => n.VacationMovementId == movimiento.Id).ToListAsync();
        novedades.Single(n => n.PayPeriod!.StartDate.Month == 7).Status.Should().Be(NoveltyStatus.Active, "la nómina de julio ya la pagó: es inmutable");
        novedades.Single(n => n.PayPeriod!.StartDate.Month == 8).Status.Should().Be(NoveltyStatus.Cancelled);
    }

    [Fact]
    public async Task Descartar_el_borrador_anula_el_movimiento_y_anular_un_movimiento_con_borrador_lo_descarta()
    {
        var d = VacacionesDePrueba.Escenario();
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        var primero = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 18)), CancellationToken.None);
        primero.IsSuccess.Should().BeTrue(primero.Error.Message);

        var descartado = await VacacionesDePrueba.Descartar(d).Handle(new DiscardVacationCommand(primero.Value.RunPublicId, "Se cambió la fecha"), CancellationToken.None);
        descartado.IsSuccess.Should().BeTrue(descartado.Error.Message);
        (await d.Db.PayrollRuns.SingleAsync(r => r.PublicId == primero.Value.RunPublicId)).Status.Should().Be(PayrollRunStatus.Superseded);
        var m1 = await d.Db.VacationMovements.SingleAsync(m => m.PublicId == primero.Value.MovementPublicId);
        m1.Status.Should().Be(VacationMovementStatus.Cancelled);
        m1.CancelReason.Should().Be("Se cambió la fecha");

        // Las fechas quedan libres: el anulado no cruza.
        var segundo = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 18)), CancellationToken.None);
        segundo.IsSuccess.Should().BeTrue(segundo.Error.Message);

        var cancelado = await VacacionesDePrueba.Cancelar(d).Handle(new CancelVacationMovementCommand(segundo.Value.MovementPublicId, "Ya no viaja"), CancellationToken.None);
        cancelado.IsSuccess.Should().BeTrue(cancelado.Error.Message);
        (await d.Db.PayrollRuns.SingleAsync(r => r.PublicId == segundo.Value.RunPublicId)).Status.Should().Be(PayrollRunStatus.Superseded, "el borrador cae con el movimiento");
        (await d.Db.VacationMovements.SingleAsync(m => m.PublicId == segundo.Value.MovementPublicId)).Status.Should().Be(VacationMovementStatus.Cancelled);
    }

    [Fact]
    public async Task Un_movimiento_liquidado_no_se_anula_y_la_reversion_exige_motivo()
    {
        var (d, runId, movementId) = await AprobadaAsync();

        var anular = await VacacionesDePrueba.Cancelar(d).Handle(new CancelVacationMovementCommand(movementId, "Intento"), CancellationToken.None);
        anular.Error.Code.Should().Be("Payroll.Vacation.MovementConfirmed");

        var sinMotivo = await VacacionesDePrueba.Reversar(d, Contadora).Handle(new ReverseVacationCommand(runId, "  "), CancellationToken.None);
        sinMotivo.Error.Code.Should().Be("Payroll.Settlement.ReasonRequired");
    }

    [Fact]
    public async Task Un_ajuste_con_signo_entra_al_saldo_de_inmediato_y_se_anula_con_motivo()
    {
        var d = VacacionesDePrueba.Escenario();
        var calculador = VacacionesDePrueba.Calculador(d);
        var handler = new AddVacationAdjustmentCommandHandler(d.Db, d.Clock, d.User, d.AuditEmitter);

        var ajuste = await handler.Handle(new AddVacationAdjustmentCommand(d.Ana.PublicId, -2.5m, "Días tomados antes del arranque sin registro"), CancellationToken.None);
        ajuste.IsSuccess.Should().BeTrue(ajuste.Error.Message);
        (await calculador.CalcularAsync(d.Ana, VacacionesDePrueba.CorteDe540Dias, CancellationToken.None)).Value.PendingDays.Should().Be(20m);

        var anulado = await VacacionesDePrueba.Cancelar(d).Handle(new CancelVacationMovementCommand(ajuste.Value, "Registrado por error"), CancellationToken.None);
        anulado.IsSuccess.Should().BeTrue(anulado.Error.Message);
        (await calculador.CalcularAsync(d.Ana, VacacionesDePrueba.CorteDe540Dias, CancellationToken.None)).Value.PendingDays.Should().Be(22.5m);
    }
}
