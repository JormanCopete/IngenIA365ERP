using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Queries;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// 2026-11-16 (pedido del dueño): el listado de Maestros › Personas busca sin distinguir mayúsculas, cada palabra en cualquier campo del
/// nombre o el documento, y filtra por columna en el servidor (con 12.000 personas, filtrar sólo las cargadas no encontraba a nadie).
/// </summary>
public class ListPeopleQueryHandlerTests
{
    private static async Task<PagedList<PersonListItemDto>> ListarAsync(PersonasTestData d, ListPeopleQuery q) =>
        (await new ListPeopleQueryHandler(d.Db).Handle(q with { Pagination = new PaginationParams { PageSize = 50 } }, CancellationToken.None)).Value;

    [Fact]
    public async Task La_busqueda_no_distingue_mayusculas_y_cada_palabra_cae_en_cualquier_campo()
    {
        var d = new PersonasTestData();
        d.Persona("1010", "JUAN", "PEREZ");
        d.Persona("2020", "Ana", "Gómez");
        await d.Db.SaveChangesAsync();

        (await ListarAsync(d, new ListPeopleQuery { SearchTerm = "perez" })).Items.Select(p => p.TaxId).Should().Equal("1010");
        (await ListarAsync(d, new ListPeopleQuery { SearchTerm = "juan perez" })).Items.Select(p => p.TaxId).Should().Equal("1010");
        (await ListarAsync(d, new ListPeopleQuery { SearchTerm = "juan gómez" })).Items.Should().BeEmpty("las dos palabras tienen que estar");
        (await ListarAsync(d, new ListPeopleQuery { SearchTerm = "GÓM" })).Items.Select(p => p.TaxId).Should().Equal("2020");
    }

    [Fact]
    public async Task Los_filtros_por_columna_van_al_servidor_y_dicen_el_total()
    {
        var d = new PersonasTestData();
        var juan = d.Persona("1010", "Juan", "Pérez");
        juan.Email = "Juan@Correo.com";
        juan.Mobile = "3001234567";
        d.Persona("2020", "Ana", "Pérez");
        await d.Db.SaveChangesAsync();

        (await ListarAsync(d, new ListPeopleQuery { Email = "juan@" })).Items.Select(p => p.TaxId).Should().Equal("1010");
        (await ListarAsync(d, new ListPeopleQuery { Mobile = "300" })).Items.Select(p => p.TaxId).Should().Equal("1010");
        (await ListarAsync(d, new ListPeopleQuery { TaxId = "202" })).Items.Select(p => p.TaxId).Should().Equal("2020");
        var porNombre = await ListarAsync(d, new ListPeopleQuery { Name = "pérez" });
        porNombre.Items.Should().HaveCount(2);
        porNombre.TotalCount.Should().Be(2);
    }
}
