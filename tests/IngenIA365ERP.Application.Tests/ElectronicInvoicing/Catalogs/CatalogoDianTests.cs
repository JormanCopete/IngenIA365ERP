using FluentAssertions;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Catalogs;

/// <summary>
/// Feature 012, T179 (decisiones-transversales T24, contracts/dian.md §1 y §4.4): los códigos DIAN son datos versionados
/// por vigencia (JSON embebidos), no tablas ni literales en el programa. <see cref="CatalogoDian"/> los lee a una fecha.
/// </summary>
public class CatalogoDianTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 25);
    private static readonly CatalogoDian Catalogo = CatalogoDian.Embebido;

    [Theory]
    [InlineData("94")]
    [InlineData("KGM")]
    [InlineData("LTR")]
    [InlineData("kgm")]
    public void Reconoce_unidades_Rec20_vigentes(string codigo)
    {
        Catalogo.EsUnidadValida(codigo, Hoy).Should().BeTrue();
        Catalogo.Unidad(codigo, Hoy)!.Codigo.Should().Be(codigo.ToUpperInvariant());
    }

    [Theory]
    [InlineData("KILO")]
    [InlineData("")]
    [InlineData(null)]
    public void Rechaza_una_unidad_inexistente(string? codigo)
    {
        Catalogo.EsUnidadValida(codigo, Hoy).Should().BeFalse();
    }

    [Fact]
    public void Resuelve_medios_de_pago()
    {
        Catalogo.MedioDePago("10", Hoy)!.Nombre.Should().Be("Efectivo");
        Catalogo.MedioDePago("48", Hoy).Should().NotBeNull();
        Catalogo.MedioDePago("XX", Hoy).Should().BeNull();
    }

    [Fact]
    public void Resuelve_tipos_de_identificacion()
    {
        Catalogo.TipoDeIdentificacion("13", Hoy)!.Nombre.Should().Be("Cédula de ciudadanía");
        Catalogo.TipoDeIdentificacion("31", Hoy)!.Nombre.Should().Be("NIT");
        Catalogo.TipoDeIdentificacion("99", Hoy).Should().BeNull();
    }

    [Theory]
    [InlineData("C", "13")]
    [InlineData("CC", "13")]
    [InlineData("NI", "31")]
    [InlineData("N", "31")]
    [InlineData("CE", "22")]
    [InlineData("TI", "12")]
    [InlineData("RC", "11")]
    [InlineData("PA", "41")]
    [InlineData(" c ", "13")]
    public void Traduce_el_IdType_heredado_de_COR_People(string idType, string dian)
    {
        Catalogo.TipoDeIdentificacionDe(idType, Hoy).Should().Be(dian);
    }

    [Fact]
    public void Un_IdType_sin_traduccion_es_nulo_y_no_se_inventa_la_cedula()
    {
        Catalogo.TipoDeIdentificacionDe("ZZ", Hoy).Should().BeNull();
        Catalogo.TipoDeIdentificacionDe(null, Hoy).Should().BeNull();
    }

    [Fact]
    public void Da_la_identificacion_del_consumidor_final()
    {
        var consumidor = Catalogo.ConsumidorFinal(Hoy);

        consumidor.Should().NotBeNull();
        consumidor!.TipoDeIdentificacion.Should().Be("13");
        consumidor.Numero.Should().Be("222222222222");
        consumidor.Nombre.Should().Be("Consumidor final");
        Catalogo.TipoDeIdentificacion(consumidor.TipoDeIdentificacion, Hoy).Should().NotBeNull();
    }

    [Fact]
    public void Resuelve_los_conceptos_de_correccion_de_las_notas()
    {
        var credito = Catalogo.ConceptosDeCorreccion(ClaseDeNotaDian.NotaCredito, Hoy);
        var debito = Catalogo.ConceptosDeCorreccion(ClaseDeNotaDian.NotaDebito, Hoy);

        credito.Select(c => c.Codigo).Should().Contain(["1", "2", "3", "4"]);
        credito.Should().ContainSingle(c => c.EsAnulacion).Which.Codigo.Should().Be("2");
        debito.Select(c => c.Codigo).Should().Contain(["1", "2", "3", "4"]);
        debito.Should().NotContain(c => c.EsAnulacion);
        Catalogo.ConceptoDeCorreccion(ClaseDeNotaDian.NotaCredito, "2", Hoy)!.EsAnulacion.Should().BeTrue();
        Catalogo.ConceptoDeCorreccion(ClaseDeNotaDian.NotaCredito, "9", Hoy).Should().BeNull();
        Catalogo.ConceptosDeCorreccion(ClaseDeNotaDian.NotaDeAjusteDelDocumentoSoporte, Hoy).Should().NotBeEmpty();
    }

    [Fact]
    public void Un_JSON_con_dos_vigencias_devuelve_la_de_la_fecha()
    {
        const string unidades = """
        {
          "catalogo": "unidades",
          "versiones": [
            { "vigenteDesde": "2020-01-01", "vigenteHasta": "2026-06-30", "fuente": "vieja",
              "codigos": [ { "codigo": "94", "nombre": "unidad" }, { "codigo": "XYZ", "nombre": "retirada" } ] },
            { "vigenteDesde": "2026-07-01", "fuente": "nueva",
              "codigos": [ { "codigo": "94", "nombre": "unidad" }, { "codigo": "KGM", "nombre": "kilogramo" } ] }
          ]
        }
        """;
        var catalogo = CatalogoDian.DesdeJson(unidades);

        catalogo.EsUnidadValida("XYZ", new DateOnly(2026, 6, 30)).Should().BeTrue();
        catalogo.EsUnidadValida("XYZ", new DateOnly(2026, 7, 1)).Should().BeFalse();
        catalogo.EsUnidadValida("KGM", new DateOnly(2026, 6, 30)).Should().BeFalse();
        catalogo.EsUnidadValida("KGM", new DateOnly(2026, 7, 1)).Should().BeTrue();
        catalogo.EsUnidadValida("94", new DateOnly(2019, 12, 31)).Should().BeFalse("antes de la primera vigencia no hay catálogo");
        catalogo.Unidades(new DateOnly(2026, 7, 1)).Select(u => u.Codigo).Should().Equal("94", "KGM");
    }

    [Fact]
    public void Dos_vigencias_que_se_cruzan_no_se_admiten()
    {
        const string cruzadas = """
        { "catalogo": "mediosDePago", "versiones": [
            { "vigenteDesde": "2020-01-01", "codigos": [ { "codigo": "10", "nombre": "Efectivo" } ] },
            { "vigenteDesde": "2025-01-01", "codigos": [ { "codigo": "10", "nombre": "Efectivo" } ] } ] }
        """;

        var accion = () => CatalogoDian.DesdeJson(cruzadas);

        accion.Should().Throw<InvalidOperationException>().WithMessage("*mediosDePago*");
    }

    [Fact]
    public void Los_catalogos_embebidos_cubren_hoy()
    {
        Catalogo.Unidades(Hoy).Should().NotBeEmpty();
        Catalogo.MediosDePago(Hoy).Should().NotBeEmpty();
        Catalogo.TiposDeIdentificacion(Hoy).Should().NotBeEmpty();
    }
}
