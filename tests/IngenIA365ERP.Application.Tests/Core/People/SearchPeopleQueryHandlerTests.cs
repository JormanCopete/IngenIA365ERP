using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Core.People.Queries;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>Feature 008, FR-013: la búsqueda trae las ocho banderas, filtra por rol y sabe encontrar eliminadas por documento.</summary>
public class SearchPeopleQueryHandlerTests
{
    [Fact]
    public async Task Trae_las_banderas_y_filtra_por_rol()
    {
        var d = new PersonasTestData();
        d.Persona("1", "Ana", "Pérez", asociada: true);
        d.Persona("2", "Beto", "Pérez", empleada: true);
        var c = d.Persona("3", "Caro", "Pérez");
        c.IsCustomer = true; c.IsSupplier = true;
        await d.Db.SaveChangesAsync();
        var handler = new SearchPeopleQueryHandler(d.Db);

        var todas = await handler.Handle(new SearchPeopleQuery("Pérez"), CancellationToken.None);
        var asociadas = await handler.Handle(new SearchPeopleQuery("Pérez", "associate"), CancellationToken.None);
        var clientes = await handler.Handle(new SearchPeopleQuery("Pérez", "customer"), CancellationToken.None);

        todas.Value.Should().HaveCount(3);
        todas.Value.Single(p => p.IdentificationNumber == "3").Should().Match<PersonSearchDto>(p => p.IsCustomer && p.IsSupplier && !p.IsAssociate);
        asociadas.Value.Select(p => p.IdentificationNumber).Should().Equal("1");
        clientes.Value.Select(p => p.IdentificationNumber).Should().Equal("3");
    }

    [Fact]
    public async Task Nunca_devuelve_eliminadas_y_sin_termino_no_busca()
    {
        var d = new PersonasTestData();
        d.Persona("1", "Ana", "Pérez", eliminada: true);
        var handler = new SearchPeopleQueryHandler(d.Db);

        (await handler.Handle(new SearchPeopleQuery("Pérez"), CancellationToken.None)).Value.Should().BeEmpty();
        (await handler.Handle(new SearchPeopleQuery("  "), CancellationToken.None)).Value.Should().BeEmpty();
    }

    [Fact]
    public void Un_rol_desconocido_no_pasa_la_validacion()
    {
        var v = new SearchPeopleQueryValidator();

        v.TestValidate(new SearchPeopleQuery("x", "gerente")).ShouldHaveValidationErrorFor(q => q.Role);
        v.TestValidate(new SearchPeopleQuery("x", "Employee")).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(new SearchPeopleQuery("x", null)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Por_documento_encuentra_eliminadas_y_lo_dice()
    {
        var d = new PersonasTestData();
        d.Persona("1023456789", "Carlos", "Gómez", eliminada: true, asociada: true);
        var handler = new GetPersonByDocumentQueryHandler(d.Db);

        var r = await handler.Handle(new GetPersonByDocumentQuery(" 1023456789 "), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.IsDeleted.Should().BeTrue();
        r.Value.DeletedAt.Should().NotBeNull();
        r.Value.FullName.Should().Be("Carlos Gómez");
        r.Value.IsAssociate.Should().BeTrue();
    }

    [Fact]
    public async Task Por_documento_sin_nadie_es_NotFound()
    {
        var d = new PersonasTestData();

        var r = await new GetPersonByDocumentQueryHandler(d.Db).Handle(new GetPersonByDocumentQuery("999"), CancellationToken.None);

        r.Error.Code.Should().Be("Person.NotFound");
    }
}
