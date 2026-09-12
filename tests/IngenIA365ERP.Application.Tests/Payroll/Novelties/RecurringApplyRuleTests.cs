using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Novelties.RecurringNovelties;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Payroll.Novelties;

/// <summary>
/// Feature 006 US2: una recurrente puede generarse en todos los períodos del mes, sólo en el
/// primero o sólo en el último. En quincenal, «último» es la quincena 2; en semanal, la mayor
/// semana creada para ese mes.
/// </summary>
public class RecurringApplyRuleTests
{
    private static async Task<(NominaTestData d, PayrollPlan plan, Employee emp)> QuincenalAsync()
    {
        var d = new NominaTestData();
        var plan = new PayrollPlan { Code = "QUINCENAL", Name = "Quincenal", Periodicity = PayrollPeriodicity.Biweekly, IsActive = true, CreatedBy = "test" };
        d.Db.PayrollPlans.Add(plan);
        await d.Db.SaveChangesAsync();
        var emp = d.Empleado("Quince", 2_000_000m, new DateTime(2025, 1, 1));
        emp.PayrollPlanId = plan.Id;
        await d.Db.SaveChangesAsync();
        return (d, plan, emp);
    }

    private static async Task RecurrenteAsync(NominaTestData d, Employee emp, RecurringApplyRule regla) =>
        (await new CreateRecurringNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker)
            .Handle(new CreateRecurringNoveltyCommand(emp.PublicId, "PRESTAMO_EMP", null, 100_000m, new DateTime(2026, 1, 1), null, 12, null, regla), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

    private static async Task<int> GeneradasAsync(NominaTestData d, PayPeriod periodo)
    {
        await d.Recurrentes.MaterializeAsync(periodo, CancellationToken.None);
        return await d.Db.PayrollNovelties.CountAsync(n => n.PayPeriodId == periodo.Id && n.Origin == NoveltyOrigin.Recurring && n.Status == NoveltyStatus.Active);
    }

    [Theory]
    [InlineData(RecurringApplyRule.EveryPeriod, 1, 1)]
    [InlineData(RecurringApplyRule.FirstOfMonth, 1, 0)]
    [InlineData(RecurringApplyRule.LastOfMonth, 0, 1)]
    public async Task En_quincenal_la_regla_decide_en_que_quincena_se_genera(RecurringApplyRule regla, int enPrimera, int enSegunda)
    {
        var (d, plan, emp) = await QuincenalAsync();
        await RecurrenteAsync(d, emp, regla);
        var q1 = d.Periodo(new DateTime(2026, 3, 1), new DateTime(2026, 3, 15), PayPeriodStatus.Open, plan: plan);
        var q2 = d.Periodo(new DateTime(2026, 3, 16), new DateTime(2026, 3, 31), PayPeriodStatus.Open, plan: plan);
        q1.SubPeriodNumber.Should().Be(1); q2.SubPeriodNumber.Should().Be(2);

        (await GeneradasAsync(d, q1)).Should().Be(enPrimera);
        (await GeneradasAsync(d, q2)).Should().Be(enSegunda);
    }

    [Theory]
    [InlineData(RecurringApplyRule.EveryPeriod)]
    [InlineData(RecurringApplyRule.FirstOfMonth)]
    [InlineData(RecurringApplyRule.LastOfMonth)]
    public async Task En_mensual_las_tres_reglas_dan_lo_mismo(RecurringApplyRule regla)
    {
        var d = new NominaTestData();
        await RecurrenteAsync(d, d.Ana, regla);

        (await GeneradasAsync(d, d.Marzo)).Should().Be(1);
    }

    [Fact]
    public async Task En_semanal_el_ultimo_del_mes_es_la_mayor_semana_creada()
    {
        var d = new NominaTestData();
        var plan = new PayrollPlan { Code = "SEMANAL", Name = "Semanal", Periodicity = PayrollPeriodicity.Weekly, IsActive = true, CreatedBy = "test" };
        d.Db.PayrollPlans.Add(plan);
        await d.Db.SaveChangesAsync();
        var emp = d.Empleado("Semana", 2_100_000m, new DateTime(2025, 1, 1));
        emp.PayrollPlanId = plan.Id;
        await d.Db.SaveChangesAsync();
        await RecurrenteAsync(d, emp, RecurringApplyRule.LastOfMonth);
        // Marzo 2026 con cuatro semanas creadas: la 4 es la última aunque el calendario admita 5.
        var s1 = d.Periodo(new DateTime(2026, 3, 2), new DateTime(2026, 3, 8), PayPeriodStatus.Open, plan: plan);
        var s4 = d.Periodo(new DateTime(2026, 3, 23), new DateTime(2026, 3, 29), PayPeriodStatus.Open, plan: plan);

        (await GeneradasAsync(d, s1)).Should().Be(0);
        (await GeneradasAsync(d, s4)).Should().Be(1, "es la mayor semana creada del mes");
    }
}
