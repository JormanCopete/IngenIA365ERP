using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.UpdateEmployee;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.EmployeeManagement;

/// <summary>
/// QA, 2026-09-18: en /nomina/empleados la «Fecha de ingreso» se editaba y no cambiaba, porque
/// el PUT no la llevaba y el comando no la tenía. Ahora viaja y se aplica, con tres candados:
/// nómina aprobada, novedad en período cerrado antes del ingreso y cambio de salario anterior.
/// </summary>
public class UpdateEmployeeHireDateTests
{
    private readonly NominaTestData _d = new();

    private UpdateEmployeeCommand Comando(DateTime ingreso) => new()
    {
        EmployeePublicId = _d.Ana.PublicId, BaseSalary = _d.Ana.Salary, ContractType = 1, HireDate = ingreso,
    };

    private Task<Result> EjecutarAsync(DateTime ingreso) =>
        new UpdateEmployeeCommandHandler(_d.Db, _d.Clock, _d.User).Handle(Comando(ingreso), CancellationToken.None);

    [Fact]
    public async Task Sin_historia_la_fecha_cambia_y_el_cambio_de_salario_inicial_se_mueve_con_ella()
    {
        var ingresoViejo = _d.Ana.JoinDate;
        _d.Db.SalaryChanges.Add(new SalaryChange { EmployeeId = _d.Ana.Id, PayrollCompanyId = 1, EffectiveDate = ingresoViejo, NewSalary = _d.Ana.Salary, UserName = "seed", CreatedBy = "seed" });
        await _d.Db.SaveChangesAsync();
        var nuevo = new DateTime(2025, 2, 1);

        var r = await EjecutarAsync(nuevo);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await _d.Db.Employees.SingleAsync(e => e.Id == _d.Ana.Id)).JoinDate.Should().Be(nuevo);
        (await _d.Db.SalaryChanges.SingleAsync(c => c.EmployeeId == _d.Ana.Id)).EffectiveDate.Should().Be(nuevo);
    }

    [Fact]
    public async Task Con_una_nomina_aprobada_del_periodo_la_fecha_no_se_toca()
    {
        var corrida = _d.Borrador(_d.Marzo);
        corrida.Status = PayrollRunStatus.Approved;
        _d.Db.PayrollRunEmployees.Add(new PayrollRunEmployee { PayrollRunId = corrida.Id, EmployeeId = _d.Ana.Id, PayrollPlanId = _d.Plan.Id, DaysWorked = 30 });
        await _d.Db.SaveChangesAsync();

        var r = await EjecutarAsync(new DateTime(2026, 3, 10));

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Employee.HireDateLocked");
        r.Error.Message.Should().Contain("01/03/2026");
        (await _d.Db.Employees.SingleAsync(e => e.Id == _d.Ana.Id)).JoinDate.Should().Be(new DateTime(2025, 1, 15));
    }

    [Fact]
    public async Task Una_nomina_aprobada_de_un_periodo_posterior_al_nuevo_ingreso_no_estorba()
    {
        var corrida = _d.Borrador(_d.Marzo);
        corrida.Status = PayrollRunStatus.Approved;
        _d.Db.PayrollRunEmployees.Add(new PayrollRunEmployee { PayrollRunId = corrida.Id, EmployeeId = _d.Ana.Id, PayrollPlanId = _d.Plan.Id, DaysWorked = 30 });
        await _d.Db.SaveChangesAsync();

        var r = await EjecutarAsync(new DateTime(2025, 1, 2));

        r.IsSuccess.Should().BeTrue(r.Error.Message);
    }

    [Fact]
    public async Task Un_cambio_de_salario_anterior_al_nuevo_ingreso_lo_impide()
    {
        _d.Db.SalaryChanges.Add(new SalaryChange { EmployeeId = _d.Ana.Id, PayrollCompanyId = 1, EffectiveDate = new DateTime(2025, 6, 1), NewSalary = 2_500_000m, UserName = "ana", CreatedBy = "ana" });
        await _d.Db.SaveChangesAsync();

        var r = await EjecutarAsync(new DateTime(2025, 7, 1));

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Employee.HireDateLocked");
        r.Error.Message.Should().Contain("01/06/2025");
    }

    [Fact]
    public async Task Sin_fecha_en_el_PUT_la_de_ingreso_no_cambia()
    {
        var r = await new UpdateEmployeeCommandHandler(_d.Db, _d.Clock, _d.User)
            .Handle(new UpdateEmployeeCommand { EmployeePublicId = _d.Ana.PublicId, BaseSalary = _d.Ana.Salary, ContractType = 1 }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await _d.Db.Employees.SingleAsync(e => e.Id == _d.Ana.Id)).JoinDate.Should().Be(new DateTime(2025, 1, 15));
    }

    [Fact]
    public void Una_fecha_vacia_se_rechaza_antes_de_llegar_al_handler()
    {
        var validador = new UpdateEmployeeCommandValidator();
        validador.Validate(new UpdateEmployeeCommand { EmployeePublicId = Guid.NewGuid(), BaseSalary = 1, ContractType = 1, HireDate = default(DateTime) })
            .Errors.Should().ContainSingle(e => e.PropertyName == "HireDate");
        validador.Validate(new UpdateEmployeeCommand { EmployeePublicId = Guid.NewGuid(), BaseSalary = 1, ContractType = 1 })
            .IsValid.Should().BeTrue();
    }
}
