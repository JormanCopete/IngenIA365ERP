using FluentAssertions;
using IngenIA365ERP.Application.Core.People.Commands.UpdatePerson;
using IngenIA365ERP.Application.Core.People.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Feature 010 (D-06): la persona guarda el segundo apellido y los otros nombres por separado
/// para la DIAN y la PILA; nada parte <c>LastName</c> por dato, y un cliente que no los manda
/// los deja vacíos (nulos), no los inventa.
/// </summary>
public class SegundoApellidoYOtrosNombresTests
{
    [Fact]
    public async Task Crear_y_editar_guardan_los_dos_campos_recortados_y_vacio_es_nulo()
    {
        var d = new PersonasTestData();
        var creada = await d.Personas.PrepareAsync(PersonasTestData.Entrada() with { SecondLastName = " Gómez ", OtherNames = "María " }, CancellationToken.None);
        creada.IsSuccess.Should().BeTrue(creada.Error.Message);
        await d.Db.SaveChangesAsync();

        var guardada = await d.Db.People.SingleAsync(p => p.PublicId == creada.Value.PublicId);
        guardada.SecondLastName.Should().Be("Gómez");
        guardada.OtherNames.Should().Be("María");
        guardada.LastName.Should().Be("Pérez", "el primer apellido no se toca");

        var r = await new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas).Handle(new UpdatePersonCommand
        {
            PublicId = guardada.PublicId, IdType = "C", TaxId = guardada.TaxId, FirstName = "Ana", LastName = "Pérez", SecondLastName = "", OtherNames = "  ",
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var editada = await d.Db.People.SingleAsync(p => p.PublicId == guardada.PublicId);
        editada.SecondLastName.Should().BeNull();
        editada.OtherNames.Should().BeNull();
    }

    [Fact]
    public void El_validador_acota_los_dos_campos_a_150()
    {
        var v = new PersonInputValidator();
        v.Validate(PersonasTestData.Entrada() with { SecondLastName = new string('a', 151) }).IsValid.Should().BeFalse();
        v.Validate(PersonasTestData.Entrada() with { OtherNames = new string('a', 150) }).IsValid.Should().BeTrue();
    }
}
