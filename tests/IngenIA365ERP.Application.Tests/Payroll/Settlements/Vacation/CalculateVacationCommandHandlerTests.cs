using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Payroll.Vacations;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Vacation;

/// <summary>
/// Feature 010 US4 (T056, contracts/api.md §3.3): registrar un disfrute o una compensación crea el
/// movimiento <c>Registered</c> y la corrida <c>Vacation</c> en <c>Draft</c> en la misma acción, con
/// los hábiles congelados; una compensación por encima del máximo legal se rechaza diciendo cuánto
/// sí cabe; las fechas no se cruzan; un período aprobado se rechaza ofreciendo el retroactivo.
/// </summary>
public class CalculateVacationCommandHandlerTests
{
    // Ana: 15-01-2025, 2.000.000; al 14-07-2026 tiene 22,5 causados. Disfrute del 15 al 28 de julio de 2026
    // (14 calendario; saltan los domingos 19 y 26 y el festivo del lunes 20 → 11 hábiles de lunes a sábado).
    private static readonly DateOnly Desde = new(2026, 7, 15);
    private static readonly DateOnly Hasta = new(2026, 7, 28);

    [Fact]
    public async Task Registrar_el_disfrute_crea_el_movimiento_y_la_corrida_en_una_accion_con_los_dias_congelados()
    {
        var d = VacacionesDePrueba.Escenario();
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);

        var r = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.WorkingDays.Should().Be(11);
        r.Value.CalendarDays.Should().Be(14);
        r.Value.Skipped.Select(s => s.Reason).Should().BeEquivalentTo(["Sunday", "Holiday:Día de la Independencia", "Sunday"]);
        r.Value.CutoffDate.Should().Be(VacacionesDePrueba.CorteDe540Dias, "el corte es la víspera del disfrute, nunca posterior a hoy");
        r.Value.Amount.Should().Be(933_333m, "14 × 66.666,67 al peso; D-01: la liquidación paga los días calendario al valor día del salario ordinario");
        r.Value.Novelties.Should().ContainSingle().Which.Days.Should().Be(14);
        r.Value.Novelties[0].ConceptCode.Should().Be("AUSENCIA_VACACIONES", "política VacacionesPagoAnticipado por defecto en sí");
        r.Value.Blockers.Should().BeEmpty();

        var movimiento = await d.Db.VacationMovements.SingleAsync();
        movimiento.Status.Should().Be(VacationMovementStatus.Registered);
        movimiento.BusinessDays.Should().Be(11m);
        movimiento.CalendarDays.Should().Be(14);
        movimiento.WeekPolicyUsed.Should().Be("LunesASabado");
        movimiento.SkippedDaysJson.Should().Contain("2026-07-20");
        movimiento.PayrollRunId.Should().BeNull("la corrida que lo liquidó se anota al aprobar");

        var run = await d.Db.PayrollRuns.Include(x => x.Employees).SingleAsync();
        run.Kind.Should().Be(PayrollRunKind.Vacation);
        run.Status.Should().Be(PayrollRunStatus.Draft);
        run.EmployeeId.Should().Be(d.Ana.Id);
        run.VacationMovementId.Should().Be(movimiento.Id);
        run.PayPeriodId.Should().BeNull();
        run.Employees.Should().ContainSingle();
        var lineas = await d.Db.PayrollRunLines.Where(l => l.PayrollRunEmployeeId == run.Employees.First().Id).ToListAsync();
        lineas.Should().Contain(l => l.ConceptCode == "VACACIONES_LIQ" && l.Quantity == 14m);

        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollVacationRegistered), Arg.Any<CancellationToken>());
        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollSettlementCalculated), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Compensar_12_dias_con_22_5_causados_se_rechaza_con_el_maximo_de_11_25()
    {
        var d = VacacionesDePrueba.Escenario();

        var r = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Compensacion(d.Ana, 12m), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Vacation.CompensationOverMax");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { requestedDays = 12m, maxDays = 11.25m, accruedDays = 22.5m, policyCode = "VACACIONES_COMPENSABLE_PCT" });
        (await d.Db.VacationMovements.AnyAsync()).Should().BeFalse("nada se crea cuando se rechaza");
        (await d.Db.PayrollRuns.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Compensar_dentro_del_maximo_crea_el_movimiento_con_la_fecha_de_pago_y_paga_los_habiles()
    {
        var d = VacacionesDePrueba.Escenario();

        var r = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Compensacion(d.Ana, 10m), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.MovementKind.Should().Be(VacationMovementKind.Compensation);
        r.Value.WorkingDays.Should().Be(10m);
        r.Value.Amount.Should().Be(666_667m, "10 × 66.666,67 al peso");
        r.Value.Novelties.Should().BeEmpty("la compensación no deja ausencia en la ordinaria");
        var movimiento = await d.Db.VacationMovements.SingleAsync();
        movimiento.Kind.Should().Be(VacationMovementKind.Compensation);
        movimiento.StartDate.Should().Be(VacacionesDePrueba.CorteDe540Dias);
    }

    [Fact]
    public async Task Un_disfrute_que_se_cruza_con_otro_vivo_responde_Overlaps_con_el_movimiento()
    {
        var d = VacacionesDePrueba.Escenario();
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        var primero = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);
        primero.IsSuccess.Should().BeTrue(primero.Error.Message);

        var cruzado = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, new DateOnly(2026, 7, 27), new DateOnly(2026, 8, 1)), CancellationToken.None);

        cruzado.IsFailure.Should().BeTrue();
        cruzado.Error.Code.Should().Be("Payroll.Vacation.Overlaps");
        cruzado.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { movementPublicId = primero.Value.MovementPublicId });
    }

    [Fact]
    public async Task Un_disfrute_sobre_un_periodo_aprobado_responde_PeriodApproved_con_el_destino_del_retroactivo_y_se_admite_si_se_acepta()
    {
        var d = VacacionesDePrueba.Escenario(hoy: new DateTime(2026, 8, 5, 12, 0, 0));
        var julio = d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Approved);
        var agosto = d.Periodo(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), PayPeriodStatus.Open);

        var rechazo = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, new DateOnly(2026, 7, 27), new DateOnly(2026, 8, 8)), CancellationToken.None);

        rechazo.IsFailure.Should().BeTrue();
        rechazo.Error.Code.Should().Be("Payroll.Vacation.PeriodApproved");
        rechazo.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { periodPublicId = julio.PublicId, retroactiveTargetPeriodPublicId = (Guid?)agosto.PublicId });
        (await d.Db.VacationMovements.AnyAsync()).Should().BeFalse();

        var aceptado = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, new DateOnly(2026, 7, 27), new DateOnly(2026, 8, 8), aceptarRetroactivo: true), CancellationToken.None);

        aceptado.IsSuccess.Should().BeTrue(aceptado.Error.Message);
        aceptado.Value.Novelties.Should().HaveCount(2);
        aceptado.Value.Novelties.Should().ContainSingle(n => n.Retroactive && n.PeriodPublicId == agosto.PublicId && n.RetroactiveOfPeriodPublicId == julio.PublicId && n.Days == 4);
        aceptado.Value.Novelties.Should().ContainSingle(n => !n.Retroactive && n.PeriodPublicId == agosto.PublicId && n.Days == 8);
        aceptado.Value.CutoffDate.Should().Be(new DateOnly(2026, 7, 26));
    }

    [Fact]
    public async Task Sin_saldo_o_retirado_o_sin_habiles_se_rechaza_con_su_codigo()
    {
        var d = VacacionesDePrueba.Escenario();
        var recienIngresado = d.Empleado("Beto", 2_000_000m, new DateTime(2026, 7, 10));
        var retirado = d.Empleado("Caro", 2_000_000m, new DateTime(2024, 1, 1), retiro: new DateTime(2026, 6, 30));
        var handler = VacacionesDePrueba.Registrar(d);

        var sinSaldo = await handler.Handle(VacacionesDePrueba.Compensacion(recienIngresado, 1m), CancellationToken.None);
        var yaRetirado = await handler.Handle(VacacionesDePrueba.Disfrute(retirado, Desde, Hasta), CancellationToken.None);
        var domingoYFestivo = await handler.Handle(VacacionesDePrueba.Disfrute(d.Ana, new DateOnly(2026, 7, 19), new DateOnly(2026, 7, 20)), CancellationToken.None);
        var alReves = await handler.Handle(VacacionesDePrueba.Disfrute(d.Ana, Hasta, Desde), CancellationToken.None);

        sinSaldo.Error.Code.Should().Be("Payroll.Vacation.NoBalance");
        yaRetirado.Error.Code.Should().Be("Payroll.Vacation.EmployeeTerminated");
        domingoYFestivo.Error.Code.Should().Be("Payroll.Vacation.NoWorkingDays");
        alReves.Error.Code.Should().Be("Payroll.Vacation.DatesInvalid");
    }

    [Fact]
    public async Task Dos_disfrutes_disjuntos_o_un_disfrute_y_una_compensacion_registrados_el_mismo_dia_conviven_con_el_mismo_corte()
    {
        // D-30. Hoy es el 14-07-2026: todo disfrute futuro y toda compensación sin fecha de pago llevan corte «hoy».
        // Hasta el 2026-09-21 la llave era (empleado, corte) y el segundo registro del día respondía Duplicate.
        var d = VacacionesDePrueba.Escenario();
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        d.Periodo(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), PayPeriodStatus.Open);
        var handler = VacacionesDePrueba.Registrar(d);

        var julio = await handler.Handle(VacacionesDePrueba.Disfrute(d.Ana, new DateOnly(2026, 7, 21), new DateOnly(2026, 7, 25)), CancellationToken.None);
        var agosto = await handler.Handle(VacacionesDePrueba.Disfrute(d.Ana, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 8)), CancellationToken.None);
        var compensacion = await handler.Handle(VacacionesDePrueba.Compensacion(d.Ana, 2m), CancellationToken.None);

        julio.IsSuccess.Should().BeTrue(julio.Error.Message);
        agosto.IsSuccess.Should().BeTrue(agosto.Error.Message);
        compensacion.IsSuccess.Should().BeTrue(compensacion.Error.Message);
        var corridas = await d.Db.PayrollRuns.Where(r => r.Kind == PayrollRunKind.Vacation).ToListAsync();
        corridas.Should().HaveCount(3);
        corridas.Should().OnlyContain(r => r.CutoffDate == VacacionesDePrueba.CorteDe540Dias && r.EmployeeId == d.Ana.Id && r.Version == 1, "las tres comparten corte y empleado: la unicidad es por movimiento");
        corridas.Select(r => r.VacationMovementId).Should().OnlyHaveUniqueItems().And.NotContainNulls();

        // El saldo del último ya descuenta los dos disfrutes anteriores (movimientos vivos, aunque sean posteriores al corte).
        compensacion.Value.Warnings.Should().Contain(w => w.Message.Contains("posterior al 14/07/2026", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Recalcular_crea_una_version_nueva_y_deja_la_anterior_Superseded_sin_tocar_el_movimiento()
    {
        var d = VacacionesDePrueba.Escenario();
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        var v1 = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, Desde, Hasta), CancellationToken.None);

        var v2 = await VacacionesDePrueba.Recalcular(d).Handle(new(v1.Value.RunPublicId), CancellationToken.None);

        v2.IsSuccess.Should().BeTrue(v2.Error.Message);
        v2.Value.Version.Should().Be(2);
        v2.Value.MovementPublicId.Should().Be(v1.Value.MovementPublicId);
        var corridas = await d.Db.PayrollRuns.OrderBy(r => r.Version).ToListAsync();
        corridas.Select(r => r.Status).Should().Equal(PayrollRunStatus.Superseded, PayrollRunStatus.Draft);
        corridas.Should().OnlyContain(r => r.VacationMovementId != null);
        (await d.Db.VacationMovements.CountAsync()).Should().Be(1);
    }
}
