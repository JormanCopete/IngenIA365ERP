using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.UpdateEmployee;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Queries;
using IngenIA365ERP.Application.Tests.Core.People;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.EmployeeManagement;

/// <summary>
/// Decisión del dueño del 2026-10-22: cada empleado puede llevar un centro de costo contable
/// (<c>PAY_Employees.CostCenterRefId</c>, FK a <c>COR_CostCenters</c>) que toma el comprobante de nómina.
/// Alta y edición lo aceptan y lo validan, la ficha y el listado lo devuelven, el listado filtra por centro,
/// un PUT que no lo manda no lo borra y el cambio queda auditado con antes y después.
/// </summary>
public class CentroDeCostoDelEmpleadoTests
{
    private readonly NominaTestData _d = new();

    private CostCenter Centro(string codigo, string nombre, bool eliminado = false)
    {
        var c = new CostCenter { LegacyCode = codigo, Name = nombre, IsDeleted = eliminado, CreatedBy = "test" };
        _d.Db.CostCenters.Add(c);
        _d.Db.SaveChanges();
        return c;
    }

    private UpdateEmployeeCommand Basico() => new() { EmployeePublicId = _d.Ana.PublicId, BaseSalary = _d.Ana.Salary, ContractType = 1 };

    private Task<Result> ActualizarAsync(UpdateEmployeeCommand c) =>
        new UpdateEmployeeCommandHandler(_d.Db, _d.Clock, _d.User, _d.AuditEmitter).Handle(c, CancellationToken.None);

    private Task<int?> CentroDeAnaAsync() =>
        _d.Db.Employees.AsNoTracking().Where(e => e.Id == _d.Ana.Id).Select(e => e.CostCenterRefId).SingleAsync();

    [Fact]
    public async Task Editar_con_un_centro_lo_asigna_y_la_ficha_lo_devuelve_con_codigo_y_nombre()
    {
        var comercial = Centro("02", "Comercial");

        var r = await ActualizarAsync(Basico() with { CostCenterPublicId = comercial.PublicId });

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await CentroDeAnaAsync()).Should().Be(comercial.Id);
        var ficha = await new GetEmployeeByIdQueryHandler(_d.Db, _d.Clock).Handle(new GetEmployeeByIdQuery(_d.Ana.PublicId), CancellationToken.None);
        ficha.Value.CostCenterPublicId.Should().Be(comercial.PublicId);
        ficha.Value.CostCenterCode.Should().Be("02");
        ficha.Value.CostCenterName.Should().Be("Comercial");
    }

    [Fact]
    public async Task Un_centro_inexistente_o_eliminado_se_rechaza_y_no_cambia_nada()
    {
        var eliminado = Centro("09", "Viejo", eliminado: true);

        var inexistente = await ActualizarAsync(Basico() with { CostCenterPublicId = Guid.NewGuid() });
        var borrado = await ActualizarAsync(Basico() with { CostCenterPublicId = eliminado.PublicId });

        inexistente.Error.Code.Should().Be("Core.CostCenter.NotFound");
        borrado.Error.Code.Should().Be("Core.CostCenter.NotFound");
        (await CentroDeAnaAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Un_PUT_que_no_manda_el_centro_no_lo_borra_y_ClearCostCenter_lo_quita()
    {
        var comercial = Centro("02", "Comercial");
        (await ActualizarAsync(Basico() with { CostCenterPublicId = comercial.PublicId })).IsSuccess.Should().BeTrue();

        // Cliente anterior a la 2026-10-22: no manda nada del centro.
        (await ActualizarAsync(Basico() with { BaseSalary = _d.Ana.Salary + 1 })).IsSuccess.Should().BeTrue();
        (await CentroDeAnaAsync()).Should().Be(comercial.Id);

        var quitar = await ActualizarAsync(Basico() with { ClearCostCenter = true });

        quitar.IsSuccess.Should().BeTrue(quitar.Error.Message);
        (await CentroDeAnaAsync()).Should().BeNull();
    }

    [Fact]
    public async Task El_cambio_de_centro_queda_auditado_con_antes_y_despues_y_sin_cambio_no_audita()
    {
        var administracion = Centro("A1", "Administración");
        var comercial = Centro("02", "Comercial");
        var eventos = new List<AuditEventDocument>();
        _d.Audit.AppendAsync(Arg.Do<AuditEventDocument>(eventos.Add), Arg.Any<CancellationToken>());

        await ActualizarAsync(Basico() with { CostCenterPublicId = administracion.PublicId });
        await ActualizarAsync(Basico() with { CostCenterPublicId = comercial.PublicId });
        var antes = eventos.Count;
        await ActualizarAsync(Basico() with { CostCenterPublicId = comercial.PublicId });

        var cambios = eventos.Where(e => e.Action == "Payroll.Employee.CostCenterChanged").ToList();
        cambios.Should().HaveCount(2);
        cambios[1].EntityPublicId.Should().Be(_d.Ana.PublicId.ToString());
        cambios[1].OldValuesJson.Should().Contain("A1");
        cambios[1].NewValuesJson.Should().Contain("02").And.Contain("Comercial");
        eventos.Count.Should().Be(antes, "reasignar el mismo centro no cambia nada");
    }

    [Fact]
    public async Task El_alta_con_centro_lo_guarda_y_con_un_centro_inexistente_falla()
    {
        var p = new PersonasTestData();
        var centro = new CostCenter { LegacyCode = "07", Name = "Operaciones", CreatedBy = "test" };
        p.Db.CostCenters.Add(centro);
        p.Db.SaveChanges();
        var persona = p.Persona("1099000111", "Luis", "Mora");
        var input = new EmployeeInput { BaseSalary = 2_000_000m, ContractType = 1, HireDate = new DateTime(2026, 5, 1), CostCenterPublicId = centro.PublicId };

        var malo = await p.Empleados.PrepareAsync(persona, input with { CostCenterPublicId = Guid.NewGuid() }, CancellationToken.None);
        var bueno = await p.Empleados.PrepareAsync(persona, input, CancellationToken.None);
        await p.Db.SaveChangesAsync();

        malo.Error.Code.Should().Be("Core.CostCenter.NotFound");
        bueno.IsSuccess.Should().BeTrue(bueno.Error.Message);
        (await p.Db.Employees.AsNoTracking().SingleAsync(e => e.PersonId == persona.Id)).CostCenterRefId.Should().Be(centro.Id);

        var sin = p.Persona("1099000222", "Eva", "Rios");
        (await p.Empleados.PrepareAsync(sin, input with { CostCenterPublicId = null }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        await p.Db.SaveChangesAsync();
        (await p.Db.Employees.AsNoTracking().SingleAsync(e => e.PersonId == sin.Id)).CostCenterRefId.Should().BeNull("sin centro es válido");
    }

    [Fact]
    public async Task El_listado_trae_el_centro_y_filtra_por_el()
    {
        var comercial = Centro("02", "Comercial");
        var bruno = _d.Empleado("Bruno", 3_000_000m, new DateTime(2024, 2, 1));
        bruno.CostCenterRefId = comercial.Id;
        await _d.Db.SaveChangesAsync();
        var handler = new ListEmployeesQueryHandler(_d.Db);

        var todos = await handler.Handle(new ListEmployeesQuery(), CancellationToken.None);
        var filtrado = await handler.Handle(new ListEmployeesQuery { CostCenterPublicId = comercial.PublicId }, CancellationToken.None);

        todos.Value.Items.Should().HaveCount(2);
        filtrado.Value.Items.Should().ContainSingle().Which.PublicId.Should().Be(bruno.PublicId);
        var fila = filtrado.Value.Items.Single();
        fila.CostCenterCode.Should().Be("02");
        fila.CostCenterName.Should().Be("Comercial");
        fila.CostCenterPublicId.Should().Be(comercial.PublicId);
        todos.Value.Items.Single(i => i.PublicId == _d.Ana.PublicId).CostCenterPublicId.Should().BeNull();
    }
}
