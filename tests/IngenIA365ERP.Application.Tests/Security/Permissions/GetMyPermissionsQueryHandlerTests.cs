using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Security.Permissions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Permissions;

/// <summary>Feature 008, FR-011: lo que la interfaz recibe para ocultar lo que la API negaría.</summary>
public class GetMyPermissionsQueryHandlerTests
{
    [Fact]
    public async Task Devuelve_los_codigos_ordenados_y_sin_repetidos()
    {
        var permisos = Substitute.For<ICurrentUserPermissions>();
        permisos.EsMaestroGlobal.Returns(false);
        permisos.ListAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<string>>(["Payroll.Employees.View", "Core.People.View", "core.people.view", "Core.People.Create"]));

        var r = await new GetMyPermissionsQueryHandler(permisos).Handle(new GetMyPermissionsQuery(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.IsGlobalMasterAdmin.Should().BeFalse();
        r.Value.Permissions.Should().Equal("Core.People.Create", "Core.People.View", "Payroll.Employees.View");
    }

    [Fact]
    public async Task El_maestro_global_no_lista_permisos_y_se_marca()
    {
        var permisos = Substitute.For<ICurrentUserPermissions>();
        permisos.EsMaestroGlobal.Returns(true);

        var r = await new GetMyPermissionsQueryHandler(permisos).Handle(new GetMyPermissionsQuery(), CancellationToken.None);

        r.Value.IsGlobalMasterAdmin.Should().BeTrue();
        r.Value.Permissions.Should().BeEmpty();
        await permisos.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_fila_en_la_cooperativa_la_lista_es_vacia_y_no_falla()
    {
        var permisos = Substitute.For<ICurrentUserPermissions>();
        permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>([]));

        var r = await new GetMyPermissionsQueryHandler(permisos).Handle(new GetMyPermissionsQuery(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue("el cliente oculta todo; la API ya niega con 404");
        r.Value.Permissions.Should().BeEmpty();
    }
}
