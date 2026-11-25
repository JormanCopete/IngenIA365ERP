using FluentAssertions;
using IngenIA365ERP.Application.Core.Associates.Queries;
using IngenIA365ERP.Application.Tests.Core.People;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.Associates;

/// <summary>
/// 2026-11-24 (pedido del dueño: «que el campo que permite buscar los asociados se comporte de una forma más ágil»): la búsqueda de asociados no
/// distingue mayúsculas, cada palabra puede caer en un campo distinto (documento, nombres, apellidos, razón social o empresa empleadora) y la lista
/// filtra en el servidor por columna (documento, nombre y empresa), como Personas. Antes era un <c>Contains</c> literal: en PostgreSQL «perez» no
/// encontraba «PEREZ».
/// </summary>
public class BusquedaDeAsociadosTests
{
    private readonly PersonasTestData _d = new();

    private async Task<IReadOnlyList<AssociateRowDto>> Buscar(ListAssociatesQuery q) =>
        (await new ListAssociatesQueryHandler(_d.Db).Handle(q with { PageSize = 500 }, default)).Value.Items;

    private async Task SembrarAsync()
    {
        _d.Persona("1023456789", "JUAN CARLOS", "PEREZ", asociada: true);
        _d.Persona("79111222", "MARIA", "GOMEZ", asociada: true);
        _d.Persona("52333444", "Pedro", "Pérez", asociada: true);
        _d.Persona("900111", "Ana", "Perea");
        var maria = await _d.Db.Associates.Include(a => a.Person).SingleAsync(a => a.Person!.TaxId == "79111222");
        maria.ExternalEmployerName = "Taxis Unidos S.A.S.";
        await _d.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task No_distingue_mayusculas_y_cada_palabra_puede_caer_en_un_campo_distinto()
    {
        await SembrarAsync();

        (await Buscar(new ListAssociatesQuery { SearchTerm = "juan perez" })).Should().ContainSingle()
            .Which.IdentificationNumber.Should().Be("1023456789");
        (await Buscar(new ListAssociatesQuery { SearchTerm = "pere" })).Select(a => a.IdentificationNumber)
            .Should().BeEquivalentTo(["1023456789"], "«Pérez» con tilde no coincide sin ella y «Perea» no es asociada");
        (await Buscar(new ListAssociatesQuery { SearchTerm = "taxis" })).Should().ContainSingle()
            .Which.IdentificationNumber.Should().Be("79111222", "la empresa empleadora también se busca");
    }

    [Fact]
    public async Task Los_filtros_de_columna_van_al_servidor()
    {
        await SembrarAsync();

        (await Buscar(new ListAssociatesQuery { TaxId = "7911" })).Should().ContainSingle().Which.FullName.Should().Contain("MARIA");
        (await Buscar(new ListAssociatesQuery { Name = "carlos" })).Should().ContainSingle().Which.IdentificationNumber.Should().Be("1023456789");
        (await Buscar(new ListAssociatesQuery { Employer = "UNIDOS" })).Should().ContainSingle().Which.IdentificationNumber.Should().Be("79111222");
        (await Buscar(new ListAssociatesQuery { Name = "pedro", TaxId = "1023" })).Should().BeEmpty("los filtros se suman");
    }
}
