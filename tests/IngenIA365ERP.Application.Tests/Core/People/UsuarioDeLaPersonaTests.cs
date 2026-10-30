using IngenIA365ERP.Application.Common.Personas;
using FluentAssertions;
using IngenIA365ERP.Application.Core.People.Services;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Feature 017 (T029, D-13): la llave de la persona es el usuario derivado de tipo y número. CC 123456 y
/// CE 123456 conviven; buscar por el número solo sin tipo, con las dos, pide el tipo.
/// </summary>
public class UsuarioDeLaPersonaTests
{
    [Fact]
    public async Task CC_y_CE_con_el_mismo_numero_son_dos_personas()
    {
        var d = new PersonasTestData();

        var cc = await d.Personas.PrepareAsync(PersonasTestData.Entrada("123456") with { IdType = "C" }, default);
        await d.Db.SaveChangesAsync();
        var ce = await d.Personas.PrepareAsync(PersonasTestData.Entrada("123456", "Juan") with { IdType = "CE" }, default);
        await d.Db.SaveChangesAsync();

        cc.IsSuccess.Should().BeTrue();
        ce.IsSuccess.Should().BeTrue(ce.IsFailure ? ce.Error.Message : null);
        cc.Value.Username.Should().Be("123456");
        ce.Value.Username.Should().Be("CE123456");
    }

    [Fact]
    public async Task El_mismo_tipo_y_numero_sigue_siendo_unico_aunque_lleve_puntos()
    {
        var d = new PersonasTestData();
        d.Persona("1023456789", "Carlos", "Gómez");

        var r = await d.Personas.PrepareAsync(PersonasTestData.Entrada("1.023.456.789"), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Person.TaxIdDuplicate");
        r.Error.Message.Should().Contain("Carlos Gómez");
    }

    [Fact]
    public async Task El_numero_nuevo_se_guarda_normalizado()
    {
        var d = new PersonasTestData();

        var r = await d.Personas.PrepareAsync(PersonasTestData.Entrada("1.023.456.789"), default);

        r.Value.TaxId.Should().Be("1023456789");
        r.Value.Username.Should().Be("1023456789");
    }

    [Fact]
    public async Task Sin_tipo_y_con_dos_personas_el_buscador_pide_el_tipo()
    {
        var d = new PersonasTestData();
        await d.Personas.PrepareAsync(PersonasTestData.Entrada("123456") with { IdType = "C" }, default);
        await d.Personas.PrepareAsync(PersonasTestData.Entrada("123456", "Juan") with { IdType = "CE" }, default);
        await d.Db.SaveChangesAsync();

        var sinTipo = await ResolutorDePersonaPorDocumento.BuscarAsync(d.Db, null, "123456", default);
        var conTipo = await ResolutorDePersonaPorDocumento.BuscarAsync(d.Db, "CE", "123456", default);
        var deAcceso = await ResolutorDePersonaPorDocumento.BuscarAsync(d.Db, "CC", "123.456", default);

        sinTipo.IsFailure.Should().BeTrue();
        sinTipo.Error.Code.Should().Be(ResolutorDePersonaPorDocumento.CodigoAmbiguo);
        conTipo.Value!.FirstName.Should().Be("Juan");
        deAcceso.Value!.FirstName.Should().Be("Ana", "el código de acceso CC equivale a la «C» de la persona");
    }

    [Fact]
    public void El_indice_para_importaciones_no_lanza_con_numeros_repetidos()
    {
        var personas = new[]
        {
            (Usuario: "123456", Numero: "123456", Nombre: "Ana"),
            (Usuario: "CE123456", Numero: "123456", Nombre: "Juan"),
            (Usuario: "777", Numero: "777", Nombre: "Luz"),
        };

        var indice = ResolutorDePersonaPorDocumento.Indexar(personas, p => p.Usuario, p => p.Numero);

        indice.TryGet(null, "777", out var luz, out _).Should().BeTrue();
        luz.Nombre.Should().Be("Luz");
        indice.TryGet(null, "123456", out _, out var ambigua).Should().BeFalse();
        ambigua.Should().BeTrue();
        indice.TryGet("CE", "123456", out var juan, out _).Should().BeTrue();
        juan.Nombre.Should().Be("Juan");
    }
}
