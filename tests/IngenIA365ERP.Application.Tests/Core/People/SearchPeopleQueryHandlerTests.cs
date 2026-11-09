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

    // ---------------------------------------------------------------- Feature 022 (T015) --

    [Theory]
    [InlineData("perez juan")]
    [InlineData("PÉREZ")]
    [InlineData("Perez")]
    [InlineData("juan pérez")]
    public async Task Ignora_mayusculas_tildes_y_el_orden_de_las_palabras(string termino)
    {
        var d = new PersonasTestData();
        d.Persona("1", "Juan", "Pérez");
        d.Persona("2", "Ana", "Gómez");

        var r = await new SearchPeopleQueryHandler(d.Db).Handle(new SearchPeopleQuery(termino), default);

        r.Value.Select(p => p.IdentificationNumber).Should().Equal("1");
    }

    [Fact]
    public async Task Todos_los_terminos_son_obligatorios_y_busca_otros_nombres_segundo_apellido_y_razon_social()
    {
        var d = new PersonasTestData();
        var a = d.Persona("1", "Juan", "Pérez");
        a.OtherNames = "Carlos"; a.SecondLastName = "Mejía";
        var b = d.Persona("2", "Juan", "Ruiz");
        var c = d.Persona("900123", "X", "Y");
        c.BusinessName = "Distribuidora Andina";
        await d.Db.SaveChangesAsync();
        var h = new SearchPeopleQueryHandler(d.Db);

        (await h.Handle(new SearchPeopleQuery("juan mejia"), default)).Value.Select(p => p.IdentificationNumber).Should().Equal("1");
        (await h.Handle(new SearchPeopleQuery("carlos"), default)).Value.Select(p => p.IdentificationNumber).Should().Equal("1");
        (await h.Handle(new SearchPeopleQuery("andina"), default)).Value.Single().FullName.Should().Be("Distribuidora Andina");
    }

    [Fact]
    public async Task Ordena_documento_exacto_prefijo_de_documento_inicio_de_palabra_y_el_resto()
    {
        var d = new PersonasTestData();
        d.Persona("77123", "Zoila", "Ruiz");            // documento por prefijo
        d.Persona("55", "Ana", "Montaña77");            // contiene 77 dentro de una palabra
        var exacta = d.Persona("77", "Bruno", "Díaz");  // documento exacto
        var codigo = d.Persona("88", "Carla", "Sosa");
        codigo.LegacyCode = "77-A";                      // 77 al comienzo de una palabra
        await d.Db.SaveChangesAsync();

        var r = await new SearchPeopleQueryHandler(d.Db).Handle(new SearchPeopleQuery("77"), default);

        r.Value.Select(p => p.IdentificationNumber).Should().Equal("77", "77123", "88", "55");
        r.Value[0].PublicId.Should().Be(exacta.PublicId);
    }

    [Fact]
    public async Task El_documento_escrito_con_puntos_encuentra_a_la_persona_primero()
    {
        var d = new PersonasTestData();
        d.Persona("1023456789", "Juan", "Pérez");

        var r = await new SearchPeopleQueryHandler(d.Db).Handle(new SearchPeopleQuery("1.023.456.789"), default);

        r.Value.Select(p => p.IdentificationNumber).Should().Equal("1023456789");
    }

    [Fact]
    public async Task Devuelve_hasta_veinte_ordenadas_por_nombre()
    {
        var d = new PersonasTestData();
        for (var i = 25; i > 0; i--) d.Persona($"{1000 + i}", $"Ana {i:00}", "López");

        var r = await new SearchPeopleQueryHandler(d.Db).Handle(new SearchPeopleQuery("lopez"), default);

        r.Value.Should().HaveCount(20);
        r.Value[0].FullName.Should().Be("Ana 01 López");
    }

    [Fact]
    public async Task Los_recientes_vuelven_en_su_orden_sin_eliminadas_y_con_el_rol()
    {
        var d = new PersonasTestData();
        var a = d.Persona("1", "Ana", "Pérez", asociada: true);
        var b = d.Persona("2", "Beto", "Ruiz");
        var c = d.Persona("3", "Caro", "Díaz", eliminada: true);
        var h = new SearchPeopleQueryHandler(d.Db);

        var todas = await h.Handle(new SearchPeopleQuery("", Ids: [b.PublicId, c.PublicId, Guid.NewGuid(), a.PublicId]), default);
        var asociadas = await h.Handle(new SearchPeopleQuery("", "associate", [b.PublicId, a.PublicId]), default);

        todas.Value.Select(p => p.PublicId).Should().Equal(b.PublicId, a.PublicId);
        asociadas.Value.Select(p => p.PublicId).Should().Equal(a.PublicId);
    }

    [Fact]
    public void Los_recientes_son_hasta_veinte_y_validos()
    {
        var v = new SearchPeopleQueryValidator();

        v.TestValidate(new SearchPeopleQuery("", Ids: Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList()))
            .ShouldHaveValidationErrorFor(q => q.Ids);
        v.TestValidate(new SearchPeopleQuery("", Ids: [Guid.Empty])).IsValid.Should().BeFalse();
        v.TestValidate(new SearchPeopleQuery("", Ids: [Guid.NewGuid()])).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Editar_la_persona_recalcula_su_texto_de_busqueda()
    {
        var d = new PersonasTestData();
        var p = d.Persona("1", "Juan", "Pérez");
        p.LastName = "Quintero";
        await d.Db.SaveChangesAsync();
        var h = new SearchPeopleQueryHandler(d.Db);

        (await h.Handle(new SearchPeopleQuery("quintero"), default)).Value.Should().ContainSingle();
        (await h.Handle(new SearchPeopleQuery("perez"), default)).Value.Should().BeEmpty();
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
