using FluentAssertions;
using IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociate;
using IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociateWithPerson;
using IngenIA365ERP.Application.Core.Associates.Contracts;
using IngenIA365ERP.Application.Tests.Core.People;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.Associates;

/// <summary>Feature 008, US2: persona y afiliación en un solo paso, las dos o ninguna; la afiliación sobre una existente no pisa «Empleado».</summary>
public class RegisterAssociateWithPersonCommandHandlerTests
{
    private static AssociateInput Afiliacion() => new() { JoinDate = new DateOnly(2026, 9, 15), ContributionRate = 5m };

    [Fact]
    public async Task Crea_persona_y_afiliacion_en_un_solo_guardado()
    {
        var d = new PersonasTestData();
        var handler = new RegisterAssociateWithPersonCommandHandler(d.Altas, d.Asociados);

        var r = await handler.Handle(new RegisterAssociateWithPersonCommand(PersonasTestData.Entrada(), Afiliacion()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var persona = await d.Db.People.SingleAsync(p => p.PublicId == r.Value.PersonPublicId);
        var afiliacion = await d.Db.Associates.SingleAsync(a => a.PublicId == r.Value.AssociatePublicId);
        persona.IsAssociate.Should().BeTrue();
        persona.IsEmployee.Should().BeFalse();
        afiliacion.PersonId.Should().Be(persona.Id);
        afiliacion.Status.Should().Be("A");
        afiliacion.ContributionRate.Should().Be(5m);
    }

    [Fact]
    public async Task Documento_duplicado_o_eliminado_no_persiste_nada()
    {
        var d = new PersonasTestData();
        d.Persona("1", "Carlos", "Gómez");
        d.Persona("2", "Diana", "Ríos", eliminada: true);
        var handler = new RegisterAssociateWithPersonCommandHandler(d.Altas, d.Asociados);

        var duplicada = await handler.Handle(new RegisterAssociateWithPersonCommand(PersonasTestData.Entrada("1"), Afiliacion()), CancellationToken.None);
        var eliminada = await handler.Handle(new RegisterAssociateWithPersonCommand(PersonasTestData.Entrada("2"), Afiliacion()), CancellationToken.None);

        duplicada.Error.Code.Should().Be("Person.TaxIdDuplicate");
        eliminada.Error.Code.Should().Be("Person.TaxIdDeleted");
        (await d.Db.Associates.CountAsync()).Should().Be(0);
        (await d.Db.People.IgnoreQueryFilters().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Afiliar_a_una_empleada_conserva_IsEmployee()
    {
        var d = new PersonasTestData();
        var p = d.Persona(empleada: true);
        var handler = new RegisterAssociateCommandHandler(d.Db, d.Asociados);

        var r = await handler.Handle(new RegisterAssociateCommand { PersonPublicId = p.PublicId, ContributionRate = 5m }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var guardada = await d.Db.People.SingleAsync(x => x.Id == p.Id);
        guardada.IsEmployee.Should().BeTrue();
        guardada.IsAssociate.Should().BeTrue();
    }

    [Fact]
    public async Task Ya_asociada_es_AlreadyExists()
    {
        var d = new PersonasTestData();
        var p = d.Persona(asociada: true);

        var r = await new RegisterAssociateCommandHandler(d.Db, d.Asociados)
            .Handle(new RegisterAssociateCommand { PersonPublicId = p.PublicId }, CancellationToken.None);

        r.Error.Code.Should().Be("Associate.AlreadyExists");
    }
}
