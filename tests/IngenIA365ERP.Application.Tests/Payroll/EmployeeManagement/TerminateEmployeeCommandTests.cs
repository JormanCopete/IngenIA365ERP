using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.TerminateEmployee;
using IngenIA365ERP.Application.Tests.Core.People;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.EmployeeManagement;

/// <summary>
/// P14 (2026-09-13): el motivo de retiro es texto libre de hasta 120 caracteres. Antes la columna
/// era el código de 4 caracteres de SOLIDO y «Renuncia» hacía fallar la API con 500.
/// </summary>
public class TerminateEmployeeCommandTests
{
    [Fact]
    public void Un_motivo_largo_se_rechaza_con_422_y_no_con_500()
    {
        var v = new TerminateEmployeeCommandValidator();

        var r = v.TestValidate(new TerminateEmployeeCommand(Guid.NewGuid(), new DateTime(2026, 10, 31), new string('x', 121)));

        r.ShouldHaveValidationErrorFor(x => x.TerminationCause).WithErrorMessage("El motivo admite hasta 120 caracteres.");
        v.TestValidate(new TerminateEmployeeCommand(Guid.NewGuid(), new DateTime(2026, 10, 31), "Renuncia voluntaria")).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new TerminateEmployeeCommand(Guid.NewGuid(), new DateTime(2026, 10, 31), null)).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new TerminateEmployeeCommand(Guid.Empty, default, null)).ShouldHaveValidationErrorFor(x => x.EmployeePublicId);
    }

    [Fact]
    public async Task Terminar_guarda_el_motivo_completo_y_apaga_solo_la_bandera_de_empleado()
    {
        var d = new PersonasTestData();
        var p = d.Persona(asociada: true, empleada: true);
        var ficha = await d.Db.Employees.SingleAsync(e => e.PersonId == p.Id);
        var handler = new TerminateEmployeeCommandHandler(d.Db, d.Clock, d.User);

        var r = await handler.Handle(new TerminateEmployeeCommand(ficha.PublicId, new DateTime(2026, 10, 31), "Renuncia voluntaria por cambio de ciudad"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var retirada = await d.Db.Employees.SingleAsync(e => e.Id == ficha.Id);
        retirada.Status.Should().Be(-1);
        retirada.TerminationCause.Should().Be("Renuncia voluntaria por cambio de ciudad");
        var persona = await d.Db.People.SingleAsync(x => x.Id == p.Id);
        persona.IsEmployee.Should().BeFalse();
        persona.IsAssociate.Should().BeTrue();
    }
}
