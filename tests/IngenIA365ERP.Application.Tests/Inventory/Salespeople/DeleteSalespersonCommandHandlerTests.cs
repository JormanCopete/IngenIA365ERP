using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Salespeople;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.DeleteSalesperson;
using IngenIA365ERP.Application.Tests.Core.People;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Salespeople;

/// <summary>
/// Feature 008, US3 / Principio V: la bandera derivada la escribe quien crea <b>o retira</b> la
/// fila hija. Hasta el 2026-09-13 eliminar la ficha de vendedor dejaba «Vendedor» encendido.
/// Desde la feature 012 (T412, T425) el retiro lleva motivo y pasa por <see cref="RolDeVendedor"/>.
/// </summary>
public class DeleteSalespersonCommandHandlerTests
{
    private static DeleteSalespersonCommandHandler Handler(PersonasTestData d)
    {
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "operador@demo", null, ExecutionChannel.Web, "/api", null, null));
        return new DeleteSalespersonCommandHandler(d.Db, new RolDeVendedor(d.Db, d.Clock, actor));
    }

    [Fact]
    public async Task Eliminar_la_ficha_apaga_IsSalesperson_y_conserva_las_demas()
    {
        var d = new PersonasTestData();
        var p = d.Persona(asociada: true, vendedora: true);
        var ficha = await d.Db.Salespeople.SingleAsync(s => s.PersonId == p.Id);

        var r = await Handler(d).Handle(new DeleteSalespersonCommand(ficha.PublicId, "Dejó de vender"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.Salespeople.IgnoreQueryFilters().SingleAsync(s => s.Id == ficha.Id)).IsDeleted.Should().BeTrue();
        var persona = await d.Db.People.SingleAsync(x => x.Id == p.Id);
        persona.IsSalesperson.Should().BeFalse();
        persona.IsAssociate.Should().BeTrue();
    }

    [Fact]
    public async Task Ficha_inexistente_es_NotFound()
    {
        var d = new PersonasTestData();

        var r = await Handler(d).Handle(new DeleteSalespersonCommand(Guid.NewGuid(), "x"), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
    }
}
