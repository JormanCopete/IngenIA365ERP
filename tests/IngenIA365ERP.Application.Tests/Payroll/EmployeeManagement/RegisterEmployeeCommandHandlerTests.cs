using FluentAssertions;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployee;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.TerminateEmployee;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Queries;
using IngenIA365ERP.Application.Tests.Core.People;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.EmployeeManagement;

/// <summary>
/// Feature 008, US3: registrar un empleado enciende sólo su bandera, guarda una vez, y el
/// reingreso tras un retiro es una ficha nueva que la consulta por persona sabe distinguir.
/// </summary>
public class RegisterEmployeeCommandHandlerTests
{
    private static RegisterEmployeeCommand Comando(Guid persona) => new()
    {
        PersonPublicId = persona, BaseSalary = 2_500_000m, ContractType = 1, HireDate = new DateTime(2026, 9, 15),
    };

    [Fact]
    public async Task Registrar_a_una_asociada_conserva_IsAssociate_y_enciende_IsEmployee()
    {
        var d = new PersonasTestData();
        var p = d.Persona(asociada: true);
        var handler = new RegisterEmployeeCommandHandler(d.Db, d.Empleados);

        var r = await handler.Handle(Comando(p.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var guardada = await d.Db.People.SingleAsync(x => x.Id == p.Id);
        guardada.IsAssociate.Should().BeTrue();
        guardada.IsEmployee.Should().BeTrue();
    }

    [Fact]
    public async Task Ficha_y_primer_cambio_de_salario_quedan_enlazados_en_un_solo_guardado()
    {
        var d = new PersonasTestData();
        var p = d.Persona();
        var handler = new RegisterEmployeeCommandHandler(d.Db, d.Empleados);

        var r = await handler.Handle(Comando(p.PublicId), CancellationToken.None);

        var ficha = await d.Db.Employees.SingleAsync(e => e.PublicId == r.Value);
        ficha.PersonId.Should().Be(p.Id);
        ficha.PayrollPlanId.Should().Be(d.Plan.Id, "sin plan elegido entra al plan por defecto");
        ficha.Status.Should().Be(1);
        var cambio = await d.Db.SalaryChanges.SingleAsync();
        cambio.EmployeeId.Should().Be(ficha.Id, "EF resolvió la FK por navegación en el mismo SaveChanges");
        cambio.NewSalary.Should().Be(2_500_000m);
    }

    [Fact]
    public async Task Ya_empleada_activa_es_AlreadyExists()
    {
        var d = new PersonasTestData();
        var p = d.Persona(empleada: true);
        var handler = new RegisterEmployeeCommandHandler(d.Db, d.Empleados);

        var r = await handler.Handle(Comando(p.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Employee.AlreadyExists");
    }

    [Fact]
    public async Task Plan_inexistente_es_PlanNotFound_y_no_persiste_nada()
    {
        var d = new PersonasTestData();
        var p = d.Persona();
        var handler = new RegisterEmployeeCommandHandler(d.Db, d.Empleados);

        var r = await handler.Handle(Comando(p.PublicId) with { PayrollPlanPublicId = Guid.NewGuid() }, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.PlanNotFound");
        (await d.Db.Employees.CountAsync()).Should().Be(0);
        (await d.Db.People.SingleAsync(x => x.Id == p.Id)).IsEmployee.Should().BeFalse();
    }

    [Fact]
    public async Task Clase_ARL_inexistente_es_WorkRiskRateNotFound()
    {
        var d = new PersonasTestData();
        var p = d.Persona();
        var handler = new RegisterEmployeeCommandHandler(d.Db, d.Empleados);

        var r = await handler.Handle(Comando(p.PublicId) with { WorkRiskRatePublicId = Guid.NewGuid() }, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.WorkRiskRateNotFound");
    }

    [Fact]
    public async Task Tras_terminar_el_contrato_el_reingreso_crea_ficha_nueva_y_by_person_devuelve_la_viva()
    {
        var d = new PersonasTestData();
        var p = d.Persona();
        var registrar = new RegisterEmployeeCommandHandler(d.Db, d.Empleados);
        var primera = await registrar.Handle(Comando(p.PublicId), CancellationToken.None);

        var terminar = new TerminateEmployeeCommandHandler(d.Db, d.Clock, d.User);
        var t = await terminar.Handle(new TerminateEmployeeCommand(primera.Value, new DateTime(2026, 10, 31), "Renuncia"), CancellationToken.None);
        t.IsSuccess.Should().BeTrue(t.Error.Message);
        (await d.Db.People.SingleAsync(x => x.Id == p.Id)).IsEmployee.Should().BeFalse("terminar apaga sólo «Empleado»");

        var segunda = await registrar.Handle(Comando(p.PublicId) with { HireDate = new DateTime(2027, 1, 15) }, CancellationToken.None);
        segunda.IsSuccess.Should().BeTrue(segunda.Error.Message);
        segunda.Value.Should().NotBe(primera.Value, "el reingreso es una ficha nueva, no la retirada reabierta");
        (await d.Db.Employees.CountAsync(e => e.PersonId == p.Id)).Should().Be(2);
        (await d.Db.Employees.SingleAsync(e => e.PublicId == primera.Value)).Status.Should().Be(-1, "la retirada es historial");
        (await d.Db.People.SingleAsync(x => x.Id == p.Id)).IsEmployee.Should().BeTrue();

        var porPersona = await new GetEmployeeByPersonIdQueryHandler(d.Db, d.Clock).Handle(new GetEmployeeByPersonIdQuery(p.PublicId), CancellationToken.None);
        porPersona.IsSuccess.Should().BeTrue(porPersona.Error.Message);
        porPersona.Value.PublicId.Should().Be(segunda.Value, "la consulta por persona mira sólo la ficha viva");
        porPersona.Value.PersonPublicId.Should().Be(p.PublicId);
    }

    [Fact]
    public async Task Con_la_ficha_retirada_y_sin_viva_by_person_es_NotFound()
    {
        var d = new PersonasTestData();
        var p = d.Persona();
        d.Db.Employees.Add(d.Ficha(p, activa: false));
        await d.Db.SaveChangesAsync();

        var r = await new GetEmployeeByPersonIdQueryHandler(d.Db, d.Clock).Handle(new GetEmployeeByPersonIdQuery(p.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Employee.NotFound", "la pantalla pasa a modo registro: reingreso");
    }
}
