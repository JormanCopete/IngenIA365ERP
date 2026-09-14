using FluentAssertions;
using IngenIA365ERP.Application.Core.People.Commands.UpdatePerson;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Feature 008, US3 / SC-002: editar una persona nunca toca sus banderas derivadas. Hasta el
/// 2026-09-13 el handler sobrescribía las ocho y un módulo que mandaba sólo la suya apagaba las
/// demás.
/// </summary>
public class UpdatePersonCommandHandlerTests
{
    [Fact]
    public async Task Editar_el_correo_de_una_asociada_y_empleada_conserva_ambas_banderas()
    {
        var d = new PersonasTestData();
        var p = d.Persona(asociada: true, empleada: true);
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = p.PublicId, IdType = "C", TaxId = p.TaxId, FirstName = p.FirstName, LastName = p.LastName,
            Email = "nuevo@demo.co",
            // Un cliente viejo no puede mandarlas: el contrato ya no las tiene.
            IsCustomer = true,
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var guardada = await d.Db.People.SingleAsync(x => x.PublicId == p.PublicId);
        guardada.Email.Should().Be("nuevo@demo.co");
        guardada.IsAssociate.Should().BeTrue("la escribe RegisterAssociate, no el formulario");
        guardada.IsEmployee.Should().BeTrue("la escribe RegisterEmployee, no el formulario");
        guardada.IsCustomer.Should().BeTrue("las banderas simples sí se editan en Personas");
        guardada.UpdatedBy.Should().Be("operador@demo");
    }

    [Fact]
    public async Task Cambiar_el_documento_al_de_otra_persona_es_TaxIdDuplicate()
    {
        var d = new PersonasTestData();
        var ana = d.Persona("1");
        d.Persona("2", "Carlos", "Gómez");
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = ana.PublicId, IdType = "C", TaxId = "2", FirstName = "Ana", LastName = "Pérez",
        }, CancellationToken.None);

        r.Error.Code.Should().Be("Person.TaxIdDuplicate");
        r.Error.Message.Should().Contain("Carlos Gómez");
    }

    [Fact]
    public async Task Cambiar_el_documento_al_de_una_eliminada_es_TaxIdDeleted()
    {
        var d = new PersonasTestData();
        var ana = d.Persona("1");
        d.Persona("2", "Carlos", "Gómez", eliminada: true);
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = ana.PublicId, IdType = "C", TaxId = "2", FirstName = "Ana", LastName = "Pérez",
        }, CancellationToken.None);

        r.Error.Code.Should().Be("Person.TaxIdDeleted");
    }

    [Fact]
    public async Task Conservar_el_propio_documento_no_es_duplicado()
    {
        var d = new PersonasTestData();
        var ana = d.Persona("1");
        var handler = new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas);

        var r = await handler.Handle(new UpdatePersonCommand
        {
            PublicId = ana.PublicId, IdType = "C", TaxId = "1", FirstName = "Ana María", LastName = "Pérez",
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
    }
}
