using FluentAssertions;
using IngenIA365ERP.Application.Core.People.Commands.RestorePerson;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>Feature 008, FR-016: restaurar reactiva la misma fila y recalcula sus banderas derivadas.</summary>
public class RestorePersonCommandHandlerTests
{
    [Fact]
    public async Task Restaura_la_misma_fila_y_recalcula_las_derivadas()
    {
        var d = new PersonasTestData();
        // Eliminada con afiliación viva pero con la bandera de asociado mal (apagada) y la de
        // empleado mal (encendida, sin ficha viva): lo que el bug de banderas pudo dejar.
        var p = d.Persona(asociada: true, eliminada: true);
        p.IsAssociate = false; p.IsEmployee = true;
        await d.Db.SaveChangesAsync();
        var handler = new RestorePersonCommandHandler(d.Db, d.Clock, d.User);

        var r = await handler.Handle(new RestorePersonCommand(p.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var restaurada = await d.Db.People.IgnoreQueryFilters().SingleAsync(x => x.Id == p.Id);
        restaurada.IsDeleted.Should().BeFalse();
        restaurada.DeletedAt.Should().BeNull();
        restaurada.DeletedBy.Should().BeNull();
        restaurada.IsAssociate.Should().BeTrue("tiene afiliación viva");
        restaurada.IsEmployee.Should().BeFalse("no tiene ficha viva");
        restaurada.IsSalesperson.Should().BeFalse();
        restaurada.UpdatedBy.Should().Be("operador@demo");
        (await d.Db.People.IgnoreQueryFilters().CountAsync()).Should().Be(1, "misma fila, no una nueva");
    }

    [Fact]
    public async Task Una_persona_viva_no_se_restaura()
    {
        var d = new PersonasTestData();
        var p = d.Persona();

        var r = await new RestorePersonCommandHandler(d.Db, d.Clock, d.User).Handle(new RestorePersonCommand(p.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Person.NotDeleted");
    }

    [Fact]
    public async Task Una_persona_que_no_existe_es_NotFound()
    {
        var d = new PersonasTestData();

        var r = await new RestorePersonCommandHandler(d.Db, d.Clock, d.User).Handle(new RestorePersonCommand(Guid.NewGuid()), CancellationToken.None);

        r.Error.Code.Should().Be("Person.NotFound");
    }
}
