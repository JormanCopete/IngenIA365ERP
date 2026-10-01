using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployeeWithPerson;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;
using IngenIA365ERP.Application.Tests.Core.People;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.EmployeeManagement;

/// <summary>Feature 008, US1 / FR-001: persona y empleado en un solo paso, los dos o ninguno.</summary>
public class RegisterEmployeeWithPersonCommandHandlerTests
{
    private static EmployeeInput Laboral(Guid? plan = null, Guid? claseArl = null) => new()
    {
        BaseSalary = 2_500_000m, ContractType = 1, HireDate = new DateTime(2026, 9, 15),
        PayrollPlanPublicId = plan, WorkRiskRatePublicId = claseArl, PayrollBankAccountType = 1,
    };

    private static RegisterEmployeeWithPersonCommandHandler Handler(PersonasTestData d) => new(d.Altas, d.Empleados);

    [Fact]
    public async Task Crea_persona_ficha_y_primer_salario_en_un_solo_guardado()
    {
        var d = new PersonasTestData();

        var r = await Handler(d).Handle(new RegisterEmployeeWithPersonCommand(PersonasTestData.Entrada(), Laboral()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var persona = await d.Db.People.SingleAsync(p => p.PublicId == r.Value.PersonPublicId);
        var ficha = await d.Db.Employees.SingleAsync(e => e.PublicId == r.Value.EmployeePublicId);
        var salario = await d.Db.SalaryChanges.SingleAsync();
        persona.IsEmployee.Should().BeTrue();
        persona.IsAssociate.Should().BeFalse();
        persona.CreatedBy.Should().Be("operador@demo");
        ficha.PersonId.Should().Be(persona.Id, "la FK se resolvió por navegación en el mismo SaveChanges");
        ficha.PayrollPlanId.Should().Be(d.Plan.Id);
        salario.EmployeeId.Should().Be(ficha.Id);
    }

    [Fact]
    public async Task Documento_duplicado_no_persiste_nada()
    {
        var d = new PersonasTestData();
        d.Persona("1023456789", "Carlos", "Gómez");
        var antes = await d.Db.Employees.CountAsync();

        var r = await Handler(d).Handle(new RegisterEmployeeWithPersonCommand(PersonasTestData.Entrada("1023456789"), Laboral()), CancellationToken.None);

        r.Error.Code.Should().Be("Person.TaxIdDuplicate");
        r.Error.Message.Should().Contain("Carlos Gómez");
        (await d.Db.Employees.CountAsync()).Should().Be(antes);
        (await d.Db.People.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Documento_de_eliminada_no_persiste_nada()
    {
        var d = new PersonasTestData();
        d.Persona("1023456789", "Carlos", "Gómez", eliminada: true);

        var r = await Handler(d).Handle(new RegisterEmployeeWithPersonCommand(PersonasTestData.Entrada("1023456789"), Laboral()), CancellationToken.None);

        r.Error.Code.Should().Be("Person.TaxIdDeleted");
        (await d.Db.People.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await d.Db.Employees.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Plan_inexistente_no_deja_ni_la_persona()
    {
        var d = new PersonasTestData();

        var r = await Handler(d).Handle(new RegisterEmployeeWithPersonCommand(PersonasTestData.Entrada(), Laboral(plan: Guid.NewGuid())), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.PlanNotFound");
        (await d.Db.People.CountAsync()).Should().Be(0, "la persona se había agregado al contexto pero nunca se guardó");
        (await d.Db.Employees.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Clase_ARL_inexistente_no_deja_ni_la_persona()
    {
        var d = new PersonasTestData();

        var r = await Handler(d).Handle(new RegisterEmployeeWithPersonCommand(PersonasTestData.Entrada(), Laboral(claseArl: Guid.NewGuid())), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.WorkRiskRateNotFound");
        (await d.Db.People.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void El_validador_compuesto_acumula_los_errores_de_las_dos_partes()
    {
        var r = new RegisterEmployeeWithPersonCommandValidator().TestValidate(
            new RegisterEmployeeWithPersonCommand(
                PersonasTestData.Entrada(taxId: "", nombre: ""),
                new EmployeeInput { BaseSalary = 0, ContractType = 99 }));

        r.ShouldHaveValidationErrorFor(x => x.Person.TaxId);
        r.ShouldHaveValidationErrorFor(x => x.Person.FirstName);
        r.ShouldHaveValidationErrorFor(x => x.Employee.BaseSalary);
        r.ShouldHaveValidationErrorFor(x => x.Employee.ContractType);
    }

    [Fact]
    public void Sin_persona_o_sin_datos_laborales_no_pasa()
    {
        var r = new RegisterEmployeeWithPersonCommandValidator().TestValidate(new RegisterEmployeeWithPersonCommand(null!, null!));

        r.ShouldHaveValidationErrorFor(x => x.Person);
        r.ShouldHaveValidationErrorFor(x => x.Employee);
    }
}
