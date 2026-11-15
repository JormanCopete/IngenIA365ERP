using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Tests.Entities;

/// <summary>Feature 022 (T015): el texto de búsqueda de la persona: partes normalizadas, sin repetir y dentro del largo.</summary>
public class PersonSearchTextTests
{
    [Fact]
    public void Une_documento_nombres_apellidos_razon_social_y_codigo_normalizados()
    {
        var p = new Person
        {
            TaxId = "1023456789", FirstName = "José", OtherNames = "Ángel", LastName = "Peña", SecondLastName = "de la  Hoz",
            BusinessName = "Panadería Ñapa", LegacyCode = "a-01",
        };

        p.RecalcularTextoDeBusqueda();

        p.SearchText.Should().Be("1023456789 JOSE ANGEL PEÑA DE LA HOZ PANADERIA ÑAPA A-01");
    }

    [Fact]
    public void Omite_partes_vacias_y_repetidas_y_respeta_el_largo()
    {
        Person.TextoDeBusqueda("9001", "ACME", null, "ACME", "  ", "acme", null).Should().Be("9001 ACME");

        var largo = new string('X', 150);
        Person.TextoDeBusqueda("1", largo + "A", largo + "B", largo + "C", largo + "D", largo + "E", "Z")
            .Length.Should().BeLessThanOrEqualTo(Person.LargoDeTextoDeBusqueda);
    }
}
