using FluentAssertions;
using IngenIA365ERP.Domain.Common.Compliance;

namespace IngenIA365ERP.Domain.Tests.Common.Compliance;

/// <summary>
/// Feature 013, T0020 (T70, F-18; quickstart §1.4; SC-012): la coincidencia por nombre de las listas restrictivas, en
/// memoria (SQL Server no tiene <c>pg_trgm</c>). El nombre se normaliza (mayúsculas, sin tildes ni signos, tokens
/// ordenados) para que el orden de nombres y apellidos no importe; el umbral llega por parámetro
/// (<c>Listas.UmbralDeCoincidenciaDeNombre</c>), nunca está en el código. El documento se compara normalizado.
/// </summary>
public class CoincidenciaDeNombresTests
{
    [Theory]
    [InlineData("José  Pérez", "JOSE PEREZ")]
    [InlineData("Pérez, José", "JOSE PEREZ")]
    [InlineData("pérez-gómez josé", "GOMEZ JOSE PEREZ")]
    [InlineData("MUÑOZ O'Neill", "MUNOZ ONEILL")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void Normaliza_mayusculas_sin_tildes_ni_signos_y_ordena_los_tokens(string? nombre, string esperado) =>
        CoincidenciaDeNombres.Normalizar(nombre).Should().Be(esperado);

    [Theory]
    [InlineData("1.234.567-8", "12345678")]
    [InlineData("0001234567", "1234567")]
    [InlineData(" 890 900 123 ", "890900123")]
    [InlineData("ab-12345", "AB12345")]
    [InlineData("000", "")]
    [InlineData(null, "")]
    public void Normaliza_el_documento_sin_puntos_guiones_espacios_ni_ceros_a_la_izquierda(string? documento, string esperado) =>
        CoincidenciaDeNombres.NormalizarDocumento(documento).Should().Be(esperado);

    [Fact]
    public void Los_homonimos_con_tildes_y_en_otro_orden_coinciden_del_todo()
    {
        CoincidenciaDeNombres.Similitud("José Ángel Pérez Gómez", "PEREZ GOMEZ JOSE ANGEL").Should().Be(1m);
    }

    [Fact]
    public void Nombres_distintos_tienen_similitud_baja()
    {
        CoincidenciaDeNombres.Similitud("María López", "Carlos Ruiz").Should().BeLessThan(0.3m);
    }

    [Fact]
    public void Un_error_de_digitacion_queda_cerca_pero_no_en_uno()
    {
        var s = CoincidenciaDeNombres.Similitud("Jose Perez Gomez", "Jose Peres Gomez");
        s.Should().BeGreaterThan(0.8m).And.BeLessThan(1m);
    }

    [Fact]
    public void El_umbral_lo_decide_quien_llama()
    {
        var parecido = CoincidenciaDeNombres.Similitud("Jose Perez Gomez", "Jose Peres Gomez");

        CoincidenciaDeNombres.Evaluar("Jose Perez Gomez", "Jose Peres Gomez", [], umbral: parecido).Coincide.Should().BeTrue();
        CoincidenciaDeNombres.Evaluar("Jose Perez Gomez", "Jose Peres Gomez", [], umbral: 1m).Coincide.Should().BeFalse();
    }

    [Fact]
    public void Coincide_por_un_alias_y_dice_cual()
    {
        var r = CoincidenciaDeNombres.Evaluar("Escobar, Pablo","Escobar Gaviria Pablo Emilio", ["El Patrón", "Pablo Escobar"], umbral: 0.95m);

        r.Coincide.Should().BeTrue();
        r.Similitud.Should().Be(1m);
        r.NombreComparado.Should().Be("ESCOBAR PABLO", "el alias «Pablo Escobar» normalizado es el que alcanzó el umbral");
    }

    [Fact]
    public void Sin_coincidencia_devuelve_la_mejor_similitud_encontrada()
    {
        var r = CoincidenciaDeNombres.Evaluar("Ana Torres", "Pedro Díaz", ["Pedrito"], umbral: 0.9m);

        r.Coincide.Should().BeFalse();
        r.Similitud.Should().BeLessThan(0.9m);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.1")]
    [InlineData("1.01")]
    public void Un_umbral_fuera_de_cero_a_uno_se_rechaza(string umbral)
    {
        var acto = () => CoincidenciaDeNombres.Evaluar("A B", "A B", [], decimal.Parse(umbral, System.Globalization.CultureInfo.InvariantCulture));
        acto.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Un_nombre_vacio_no_coincide_con_nada()
    {
        CoincidenciaDeNombres.Similitud("", "").Should().Be(0m);
        CoincidenciaDeNombres.Evaluar("  ", "Ana", [], 0.5m).Coincide.Should().BeFalse();
    }
}
