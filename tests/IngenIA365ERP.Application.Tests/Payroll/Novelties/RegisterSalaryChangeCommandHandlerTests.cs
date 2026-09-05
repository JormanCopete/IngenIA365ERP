using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterSalaryChange;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Novelties;

/// <summary>T057 — FR-004: cambios de salario con fecha de efecto.</summary>
public class RegisterSalaryChangeCommandHandlerTests
{
    private static RegisterSalaryChangeCommandHandler Handler(NominaTestData d) =>
        new(d.Db, d.Clock, d.User, d.StaleMarker, d.AuditEmitter);

    [Fact]
    public async Task Crea_el_cambio_siembra_la_linea_base_y_espeja_el_salario_de_la_ficha()
    {
        var d = new NominaTestData();

        var r = await Handler(d).Handle(new RegisterSalaryChangeCommand(d.Ana.PublicId, 2_200_000m, new DateTime(2026, 3, 16), "Aumento"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var historial = await d.Db.SalaryChanges.Where(s => s.EmployeeId == d.Ana.Id).OrderBy(s => s.EffectiveDate).ToListAsync();
        historial.Should().HaveCount(2, "la línea base del ingreso y el cambio");
        historial[0].EffectiveDate.Should().Be(new DateTime(2025, 1, 15));
        historial[0].NewSalary.Should().Be(2_000_000m);
        historial[1].EffectiveDate.Should().Be(new DateTime(2026, 3, 16));
        historial[1].NewSalary.Should().Be(2_200_000m);
        (await d.Db.Employees.SingleAsync(e => e.Id == d.Ana.Id)).Salary.Should().Be(2_200_000m, "la fecha de efecto ya pasó (hoy es 20/03/2026)");

        await d.Audit.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollSalaryChanged && a.EntityPublicId == d.Ana.PublicId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_cambio_futuro_no_toca_todavia_el_salario_de_la_ficha()
    {
        var d = new NominaTestData();

        var r = await Handler(d).Handle(new RegisterSalaryChangeCommand(d.Ana.PublicId, 2_500_000m, new DateTime(2026, 4, 1), "Desde abril"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.Employees.SingleAsync(e => e.Id == d.Ana.Id)).Salary.Should().Be(2_000_000m);
    }

    [Fact]
    public async Task Rechaza_una_fecha_dentro_de_un_periodo_aprobado()
    {
        var d = new NominaTestData();
        d.Marzo.Status = PayPeriodStatus.Approved;
        d.Db.SaveChanges();

        var r = await Handler(d).Handle(new RegisterSalaryChangeCommand(d.Ana.PublicId, 2_200_000m, new DateTime(2026, 3, 16), "Tarde"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.SalaryChangeInApprovedPeriod");
        d.Db.SalaryChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task Deja_desactualizado_el_borrador_del_periodo_que_toca_la_fecha()
    {
        var d = new NominaTestData();
        var run = d.Borrador(d.Marzo);

        var r = await Handler(d).Handle(new RegisterSalaryChangeCommand(d.Ana.PublicId, 2_200_000m, new DateTime(2026, 3, 16), "Aumento"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Stale);
    }

    [Fact]
    public void El_validador_exige_salario_positivo_y_motivo()
    {
        var v = new RegisterSalaryChangeCommandValidator();
        v.Validate(new RegisterSalaryChangeCommand(Guid.NewGuid(), 0m, new DateTime(2026, 3, 1), "x")).IsValid.Should().BeFalse();
        v.Validate(new RegisterSalaryChangeCommand(Guid.NewGuid(), 100m, new DateTime(2026, 3, 1), "")).IsValid.Should().BeFalse();
        v.Validate(new RegisterSalaryChangeCommand(Guid.NewGuid(), 100m, new DateTime(2026, 3, 1), "ok")).IsValid.Should().BeTrue();
    }
}
