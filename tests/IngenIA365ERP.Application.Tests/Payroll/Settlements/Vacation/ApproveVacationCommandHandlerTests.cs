using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Vacation;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Tests.Payroll.Vacations;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Vacation;

/// <summary>
/// Feature 010 US4 (T056): aprobar deja el comprobante <c>VacationRun</c> contra <c>PROV_VACACIONES</c>
/// (SC-003), el movimiento <c>Liquidated</c> y la novedad de ausencia con origen <c>VacationLeave</c>
/// en cada período que cubre el disfrute (D-01); con la política en «paga la nómina» la novedad es
/// <c>VACACIONES</c>. Todo en la misma transacción del ciclo común.
/// </summary>
public class ApproveVacationCommandHandlerTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    // Disfrute del 27-07 al 05-08-2026: cubre julio y agosto (dos períodos del plan mensual).
    private static readonly DateOnly Desde = new(2026, 7, 27);
    private static readonly DateOnly Hasta = new(2026, 8, 5);

    private static NominaTestData EscenarioConDosPeriodos()
    {
        var d = VacacionesDePrueba.Escenario(hoy: new DateTime(2026, 7, 26, 12, 0, 0));
        for (var mes = 1; mes <= 6; mes++)
            d.MesAprobado(2026, mes, d.Ana, 2_000_000m, 249_095m, provPrima: 187_424m, provCesantias: 187_424m, provIntereses: 22_491m, provVacaciones: 83_333m);
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        d.Periodo(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), PayPeriodStatus.Open);
        LiquidacionDePrueba.ConContabilidad(d);
        d.PeriodoContable(2026, 8);
        return d;
    }

    [Fact]
    public async Task Aprobar_contabiliza_contra_la_provision_liquida_el_movimiento_y_deja_la_ausencia_en_los_dos_periodos()
    {
        var d = EscenarioConDosPeriodos();
        var calculo = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);
        calculo.IsSuccess.Should().BeTrue(calculo.Error.Message);
        calculo.Value.Novelties.Should().HaveCount(2);

        var r = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(calculo.Value.RunPublicId, Confirm: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.PostingDate.Should().Be(new DateOnly(2026, 7, 26), "D-04: el comprobante se fecha al corte");
        r.Value.Number.Should().StartWith("NM");

        var run = await d.Db.PayrollRuns.Include(x => x.AccountingDocument).Include(x => x.Employees).SingleAsync(x => x.PublicId == calculo.Value.RunPublicId);
        run.Status.Should().Be(PayrollRunStatus.Approved);
        run.ApprovedBy.Should().Be("contadora@demo");
        run.AccountingDocument!.SourceType.Should().Be("VacationRun");
        var lineas = await d.Db.PayrollRunLines.Where(l => l.PayrollRunEmployeeId == run.Employees.First().Id).ToListAsync();
        lineas.Should().Contain(l => l.ConceptCode == "VACACIONES_LIQ" && l.Amount > 0m);
        lineas.Should().Contain(l => l.ConceptCode == "VACACIONES_AJUSTE_PROV", "SC-003: la liquidación cancela PROV_VACACIONES y lleva la diferencia al ajuste");
        var asientos = await d.Db.JournalEntries.Where(j => j.DocumentId == run.AccountingDocument.Id).ToListAsync();
        asientos.Sum(j => j.Debit).Should().Be(asientos.Sum(j => j.Credit));

        var movimiento = await d.Db.VacationMovements.SingleAsync();
        movimiento.Status.Should().Be(VacationMovementStatus.Liquidated);
        movimiento.PayrollRunId.Should().Be(run.Id);

        var novedades = await d.Db.PayrollNovelties.Include(n => n.PayPeriod).Where(n => n.VacationMovementId == movimiento.Id).OrderBy(n => n.StartDate).ToListAsync();
        novedades.Should().HaveCount(2);
        novedades.Should().OnlyContain(n => n.Origin == NoveltyOrigin.VacationLeave && n.ConceptCode == "AUSENCIA_VACACIONES" && n.Status == NoveltyStatus.Active);
        novedades[0].PayPeriod!.StartDate.Should().Be(new DateTime(2026, 7, 1));
        novedades[0].StartDate.Should().Be(new DateTime(2026, 7, 27));
        novedades[0].EndDate.Should().Be(new DateTime(2026, 7, 31));
        novedades[0].DaysInPeriod.Should().Be(4);
        novedades[1].PayPeriod!.StartDate.Should().Be(new DateTime(2026, 8, 1));
        novedades[1].DaysInPeriod.Should().Be(5);
    }

    [Fact]
    public async Task Con_la_politica_en_paga_la_nomina_la_novedad_es_VACACIONES_y_la_liquidacion_no_paga_los_dias()
    {
        var d = EscenarioConDosPeriodos();
        d.Politica(CompanyPolicyKeys.VacacionesPagoAnticipado, "false", new DateOnly(2026, 1, 1));
        var calculo = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);
        calculo.IsSuccess.Should().BeTrue(calculo.Error.Message);
        calculo.Value.Amount.Should().Be(0m, "los días los paga la ordinaria");
        calculo.Value.Novelties.Should().OnlyContain(n => n.ConceptCode == "VACACIONES");

        var r = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(calculo.Value.RunPublicId, Confirm: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.DocumentPublicId.Should().Be(Guid.Empty, "no hay nada que contabilizar: la ordinaria paga y provisiona");
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == calculo.Value.RunPublicId)).Status.Should().Be(PayrollRunStatus.Approved);
        var novedades = await d.Db.PayrollNovelties.Where(n => n.Origin == NoveltyOrigin.VacationLeave).ToListAsync();
        novedades.Should().HaveCount(2).And.OnlyContain(n => n.ConceptCode == "VACACIONES");
    }

    [Fact]
    public async Task Si_un_periodo_cubierto_se_aprobo_entre_el_calculo_y_la_aprobacion_se_rechaza_salvo_que_se_acepte_el_retroactivo()
    {
        var d = EscenarioConDosPeriodos();
        var calculo = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);
        var julio = await d.Db.PayPeriods.SingleAsync(p => p.StartDate == new DateTime(2026, 7, 1));
        julio.Status = PayPeriodStatus.Approved;
        await d.Db.SaveChangesAsync();

        var rechazo = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(calculo.Value.RunPublicId, Confirm: true), CancellationToken.None);
        rechazo.Error.Code.Should().Be("Payroll.Vacation.PeriodApproved");
        // El contexto de prueba es uno solo: lo que el ciclo cambió en memoria antes de fallar no se guardó, y aquí se descarta como haría el fin de la petición.
        d.Db.ChangeTracker.Clear();
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == calculo.Value.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft, "nada se guardó");
        (await d.Db.PayrollNovelties.AnyAsync(n => n.Origin == NoveltyOrigin.VacationLeave)).Should().BeFalse();

        var aceptado = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(calculo.Value.RunPublicId, Confirm: true, AcceptRetroactive: true), CancellationToken.None);

        aceptado.IsSuccess.Should().BeTrue(aceptado.Error.Message);
        var novedades = await d.Db.PayrollNovelties.Include(n => n.PayPeriod).Where(n => n.Origin == NoveltyOrigin.VacationLeave).ToListAsync();
        novedades.Should().HaveCount(2).And.OnlyContain(n => n.PayPeriod!.StartDate == new DateTime(2026, 8, 1), "ambas quedan en agosto: la de julio como retroactiva");
        novedades.Should().ContainSingle(n => n.RetroactiveOfPeriodId == julio.Id && n.Quantity == 4m);
    }

    [Fact]
    public async Task Sin_periodo_de_nomina_que_cubra_el_disfrute_registrar_avisa_y_aprobar_se_rechaza_con_los_tramos()
    {
        // D-33. Registrar y pagar por anticipado antes de abrir el período del mes es la operación normal; hasta el
        // 2026-09-21 la aprobación pasaba con el aviso, nadie creaba después la novedad y la ordinaria pagaba los días.
        var d = VacacionesDePrueba.Escenario(hoy: new DateTime(2026, 7, 26, 12, 0, 0));
        for (var mes = 1; mes <= 6; mes++)
            d.MesAprobado(2026, mes, d.Ana, 2_000_000m, 249_095m, provPrima: 187_424m, provCesantias: 187_424m, provIntereses: 22_491m, provVacaciones: 83_333m);
        LiquidacionDePrueba.ConContabilidad(d);
        d.PeriodoContable(2026, 7);
        d.PeriodoContable(2026, 8);
        var calculo = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);
        calculo.IsSuccess.Should().BeTrue(calculo.Error.Message);
        calculo.Value.Novelties.Should().BeEmpty();
        calculo.Value.Warnings.Should().Contain(w => w.Code == VacationNoveltyPlanner.PeriodMissingCode);

        var rechazo = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(calculo.Value.RunPublicId, Confirm: true), CancellationToken.None);

        rechazo.Error.Code.Should().Be("Payroll.Vacation.PeriodMissing");
        rechazo.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { missing = new[] { new { from = Desde, to = Hasta } } });
        d.Db.ChangeTracker.Clear();
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == calculo.Value.RunPublicId)).Status.Should().Be(PayrollRunStatus.Draft, "nada se guardó");
        (await d.Db.VacationMovements.SingleAsync()).Status.Should().Be(VacationMovementStatus.Registered);
        (await d.Db.PayrollNovelties.AnyAsync(n => n.Origin == NoveltyOrigin.VacationLeave)).Should().BeFalse();

        // Sólo agosto: los días de julio quedarían sin novedad (un hueco antes del primer período también se rechaza).
        var agosto = d.Periodo(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), PayPeriodStatus.Open);
        var huecoAntes = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(calculo.Value.RunPublicId, Confirm: true), CancellationToken.None);
        huecoAntes.Error.Code.Should().Be("Payroll.Vacation.PeriodMissing");
        huecoAntes.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { missing = new[] { new { from = Desde, to = new DateOnly(2026, 7, 31) } } });
        d.Db.ChangeTracker.Clear();

        // Con julio creado se aprueba: los días de agosto viajan como traslado si agosto no existiera (FR-003), y aquí van a su período.
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        var aprobada = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(calculo.Value.RunPublicId, Confirm: true), CancellationToken.None);
        aprobada.IsSuccess.Should().BeTrue(aprobada.Error.Message);
        var novedades = await d.Db.PayrollNovelties.Include(n => n.PayPeriod).Where(n => n.Origin == NoveltyOrigin.VacationLeave).OrderBy(n => n.StartDate).ToListAsync();
        novedades.Should().HaveCount(2);
        novedades[1].PayPeriodId.Should().Be(agosto.Id);
    }

    [Fact]
    public async Task Sin_confirmacion_o_por_la_ruta_de_otro_tipo_no_se_aprueba()
    {
        var d = EscenarioConDosPeriodos();
        var calculo = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);

        var sinConfirmar = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(calculo.Value.RunPublicId, Confirm: false), CancellationToken.None);
        sinConfirmar.Error.Code.Should().Be("Payroll.Settlement.ConfirmationRequired");

        var prima = await LiquidacionDePrueba.PrimaAsync(d);
        var otroTipo = await VacacionesDePrueba.Aprobar(d, Contadora).Handle(new ApproveVacationCommand(prima.PublicId, Confirm: true), CancellationToken.None);
        otroTipo.Error.Code.Should().Be("Payroll.Settlement.KindMismatch");
    }
}
