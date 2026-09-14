using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.DeleteSalesperson;
using IngenIA365ERP.Application.Tests.Core.People;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Salespeople;

/// <summary>
/// Feature 008, US3 / Principio V: la bandera derivada la escribe quien crea <b>o retira</b> la
/// fila hija. Hasta el 2026-09-13 eliminar la ficha de vendedor dejaba «Vendedor» encendido.
/// </summary>
public class DeleteSalespersonCommandHandlerTests
{
    [Fact]
    public async Task Eliminar_la_ficha_apaga_IsSalesperson_y_conserva_las_demas()
    {
        var d = new PersonasTestData();
        var p = d.Persona(asociada: true, vendedora: true);
        var ficha = await d.Db.Salespeople.SingleAsync(s => s.PersonId == p.Id);
        var handler = new DeleteSalespersonCommandHandler(d.Db, d.Clock, d.User);

        var r = await handler.Handle(new DeleteSalespersonCommand(ficha.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.Salespeople.SingleAsync(s => s.Id == ficha.Id)).IsDeleted.Should().BeTrue();
        var persona = await d.Db.People.SingleAsync(x => x.Id == p.Id);
        persona.IsSalesperson.Should().BeFalse();
        persona.IsAssociate.Should().BeTrue();
        persona.UpdatedBy.Should().Be("operador@demo");
    }

    [Fact]
    public async Task Ficha_inexistente_es_NotFound()
    {
        var d = new PersonasTestData();
        var handler = new DeleteSalespersonCommandHandler(d.Db, d.Clock, d.User);

        var r = await handler.Handle(new DeleteSalespersonCommand(Guid.NewGuid()), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
    }
}
