using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Salespeople;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.RegisterSalespersonWithPerson;
using IngenIA365ERP.Application.Tests.Core.People;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Salespeople;

/// <summary>
/// Feature 008, fase 2: persona nueva y vendedor en un solo paso, las dos o ninguna. Las mismas reglas que Empleados y
/// Asociados: el documento duplicado o de una eliminada no deja nada, y la marca <c>IsSalesperson</c> la enciende el rol.
/// </summary>
public class RegisterSalespersonWithPersonCommandHandlerTests
{
    private readonly PersonasTestData _d = new();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();

    public RegisterSalespersonWithPersonCommandHandlerTests() =>
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "Jefe de ventas", "jefe@coop.test", ExecutionChannel.Web, "/api/inventory/salespeople", "10.0.0.1", null));

    private RegisterSalespersonWithPersonCommandHandler Handler() => new(_d.Altas, new RolDeVendedor(_d.Db, _d.Clock, _actor));

    [Fact]
    public async Task Crea_persona_y_vendedor_en_un_solo_guardado()
    {
        var r = await Handler().Handle(new RegisterSalespersonWithPersonCommand(PersonasTestData.Entrada(), new SalespersonInput(3, true))
        {
            OperationKey = Guid.NewGuid(),
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var persona = await _d.Db.People.AsNoTracking().SingleAsync(p => p.PublicId == r.Value.PersonPublicId);
        var fila = await _d.Db.Salespeople.AsNoTracking().SingleAsync(s => s.PublicId == r.Value.SalespersonPublicId);
        persona.IsSalesperson.Should().BeTrue();
        persona.IsEmployee.Should().BeFalse();
        persona.IsAssociate.Should().BeFalse();
        fila.PersonId.Should().Be(persona.Id);
        fila.SalespersonType.Should().Be(3);
        fila.AppliesCommission.Should().BeTrue();
        _d.Db.ChangeTracker.HasChanges().Should().BeFalse("un solo SaveChangesAsync");
    }

    [Fact]
    public async Task Documento_duplicado_o_eliminado_no_persiste_nada()
    {
        _d.Persona("1", "Carlos", "Gómez");
        _d.Persona("2", "Diana", "Ríos", eliminada: true);

        var duplicada = await Handler().Handle(new RegisterSalespersonWithPersonCommand(PersonasTestData.Entrada("1"), new SalespersonInput(null, null)), CancellationToken.None);
        var eliminada = await Handler().Handle(new RegisterSalespersonWithPersonCommand(PersonasTestData.Entrada("2"), new SalespersonInput(null, null)), CancellationToken.None);

        duplicada.Error.Code.Should().Be("Person.TaxIdDuplicate");
        eliminada.Error.Code.Should().Be("Person.TaxIdDeleted");
        (await _d.Db.Salespeople.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await _d.Db.People.IgnoreQueryFilters().CountAsync()).Should().Be(2);
    }

    [Fact]
    public void El_validador_exige_persona_y_rechaza_un_tipo_negativo()
    {
        var v = new RegisterSalespersonWithPersonCommandValidator();

        v.Validate(new RegisterSalespersonWithPersonCommand(PersonasTestData.Entrada(), new SalespersonInput(null, null))).IsValid.Should().BeTrue();
        v.Validate(new RegisterSalespersonWithPersonCommand(PersonasTestData.Entrada(), new SalespersonInput(-1, null))).IsValid.Should().BeFalse();
        v.Validate(new RegisterSalespersonWithPersonCommand(null!, new SalespersonInput(null, null))).IsValid.Should().BeFalse();
    }
}
