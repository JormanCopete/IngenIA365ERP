using FluentAssertions;
using IngenIA365ERP.Shared.Components.Shared;
using IngenIA365ERP.Shared.Services.Core;
using IngenIA365ERP.Shared.Tests.Helpers;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Personas;

/// <summary>
/// Feature 009 E2 (US5): quien llega a «Estado de cuenta del tercero» por <c>?person=</c> —desde el
/// auxiliar o desde «Documentos pendientes»— tiene que ver de quién son las cifras. Hasta el
/// 2026-09-20 la página ponía el literal «Tercero» en el buscador y las tres tablas iban sin título, así
/// que el nombre no aparecía en ningún sitio. Ahora la persona se resuelve con <c>PersonasClient</c> y
/// se convierte al elemento del buscador con <see cref="PersonSearchPicker.PersonSearchItem.Desde"/>.
/// </summary>
public class PersonSearchItemDesdePersonaTests
{
    [Fact]
    public void Una_persona_natural_se_muestra_con_nombres_apellidos_y_documento()
    {
        var id = Guid.NewGuid();
        var persona = new PersonaDto { PublicId = id, FirstName = "Ana", LastName = "Pérez", TaxId = "1020304050", IsAssociate = true, Status = "A", CityName = "Cali" };

        var item = PersonSearchPicker.PersonSearchItem.Desde(persona);

        item.PublicId.Should().Be(id);
        item.FullName.Should().Be("Ana Pérez");
        item.IdentificationNumber.Should().Be("1020304050");
        item.IsAssociate.Should().BeTrue();
        item.Status.Should().Be("A");
        item.CityName.Should().Be("Cali");
    }

    [Fact]
    public void Una_persona_juridica_se_muestra_por_su_razon_social()
    {
        var persona = new PersonaDto { PublicId = Guid.NewGuid(), FirstName = "", LastName = "", BusinessName = "Cooperativa El Roble", TaxId = "900123456", IsSupplier = true };

        var item = PersonSearchPicker.PersonSearchItem.Desde(persona);

        item.FullName.Should().Be("Cooperativa El Roble");
        item.IsSupplier.Should().BeTrue();
    }

    [Fact]
    public void La_pantalla_del_estado_de_cuenta_resuelve_al_tercero_en_vez_de_inventarle_un_nombre()
    {
        var marcado = File.ReadAllText(Path.Combine(Repositorio.Shared(), "Pages", "Contabilidad", "EstadoDeCuentaTercero.razor"));

        marcado.Should().Contain("@inject PersonasClient", "la persona se pide al maestro antes de consultar");
        marcado.Should().Contain("PersonSearchItem.Desde(");
        marcado.Should().NotContain("FullName = \"Tercero\"", "el literal «Tercero» era lo único que veía quien llegaba por ?person=");
        marcado.Should().Contain("@_movimientos.Titulo", "el título que arma la API («Estado de cuenta — Nombre (NIT)») se pinta sobre las cifras");
    }
}
