using FluentAssertions;
using IngenIA365ERP.Domain.Inventory.Analytics;

namespace IngenIA365ERP.Domain.Tests.Inventory.Analytics;

/// <summary>
/// Feature 012, I6, T906 (FR-040, FR-086; contracts/api.md §27 vista <c>abc</c>): <see cref="ClasificacionAbc"/> clasifica por
/// participación acumulada del valor con los umbrales de <c>Informes.UmbralesAbc</c> (<c>80/15/5</c>): un producto es A si el
/// acumulado <b>con él</b> no pasa del 80 %, B si no pasa del 95 % y C si no —el que cruza un umbral pasa a la clase siguiente, salvo
/// el primero, que siempre es A—, los empates van juntos a la clase de su grupo, un valor cero es C, y un texto de umbrales que no suma 100 es <c>Parameters.ValueNotAllowed</c>.
/// </summary>
public class ClasificacionAbcTests
{
    private static readonly UmbralesAbc Estandar = new(80m, 15m, 5m);

    private static IReadOnlyList<FilaAbc<string>> Clasificar(UmbralesAbc umbrales, params (string Producto, decimal Valor)[] valores) =>
        ClasificacionAbc.Clasificar(valores.Select(v => new ValorParaAbc<string>(v.Producto, v.Valor)).ToList(), umbrales);

    [Fact]
    public void Clasifica_por_participacion_acumulada()
    {
        // Total 1.000: P1 500 (50 %, acum. 50), P2 300 (30 %, 80), P3 100 (10 %, 90), P4 60 (6 %, 96), P5 40 (4 %, 100).
        var filas = Clasificar(Estandar, ("P3", 100m), ("P1", 500m), ("P5", 40m), ("P2", 300m), ("P4", 60m));

        filas.Select(f => (f.Clave, f.Clase)).Should().Equal(
            ("P1", ClaseAbc.A), ("P2", ClaseAbc.A), ("P3", ClaseAbc.B), ("P4", ClaseAbc.C), ("P5", ClaseAbc.C));
        filas.Select(f => f.Participacion).Should().Equal(50m, 30m, 10m, 6m, 4m);
        filas.Select(f => f.Acumulado).Should().Equal(50m, 80m, 90m, 96m, 100m);
    }

    [Fact]
    public void El_que_cruza_el_umbral_pasa_a_la_clase_siguiente()
    {
        // P1 70 (acum. 70) → A; P2 20 cruza el 80 (acum. 90) → B; P3 10 cruza el 95 (acum. 100) → C.
        var filas = Clasificar(Estandar, ("P1", 70m), ("P2", 20m), ("P3", 10m));

        filas.Select(f => f.Clase).Should().Equal(ClaseAbc.A, ClaseAbc.B, ClaseAbc.C);
    }

    [Fact]
    public void El_primero_siempre_es_A_aunque_pese_mas_que_el_umbral()
    {
        Clasificar(Estandar, ("P1", 950m), ("P2", 50m)).Select(f => f.Clase).Should().Equal(new[] { ClaseAbc.A, ClaseAbc.C },
            "el primero es A aunque con él el acumulado sea 95 %; el segundo llega al 100 %");
    }

    [Fact]
    public void Los_empates_van_juntos_a_la_clase_de_su_grupo_y_se_ordenan_por_clave()
    {
        // Total 100. P1 65 → A (acum. 65). P2, P3 y P4 empatan en 10: el grupo se clasifica con el acumulado de su primer miembro
        // (75) → los tres A aunque P4 llegue al 95. P5 5 lleva el acumulado a 100 → C.
        var filas = Clasificar(Estandar, ("P3", 10m), ("P1", 65m), ("P2", 10m), ("P4", 10m), ("P5", 5m));

        filas.Select(f => (f.Clave, f.Clase)).Should().Equal(
            ("P1", ClaseAbc.A), ("P2", ClaseAbc.A), ("P3", ClaseAbc.A), ("P4", ClaseAbc.A), ("P5", ClaseAbc.C));
    }

    [Fact]
    public void Un_valor_cero_es_C()
    {
        var filas = Clasificar(Estandar, ("P1", 100m), ("P2", 0m), ("P3", -3m));

        filas.Select(f => (f.Clave, f.Clase)).Should().Equal(("P1", ClaseAbc.A), ("P2", ClaseAbc.C), ("P3", ClaseAbc.C));
        filas.Single(f => f.Clave == "P3").Participacion.Should().Be(0m, "un valor negativo (devoluciones netas) no participa");
    }

    [Fact]
    public void Si_todo_vale_cero_todo_es_C()
    {
        Clasificar(Estandar, ("P1", 0m), ("P2", 0m)).Should().OnlyContain(f => f.Clase == ClaseAbc.C && f.Participacion == 0m);
    }

    [Fact]
    public void Sin_productos_no_hay_filas()
    {
        Clasificar(Estandar).Should().BeEmpty();
    }

    [Fact]
    public void Los_porcentajes_van_a_dos_decimales_y_el_ultimo_acumulado_es_100()
    {
        var filas = Clasificar(Estandar, ("P1", 1m), ("P2", 1m), ("P3", 1m));

        filas.Select(f => f.Participacion).Should().Equal(33.33m, 33.33m, 33.33m);
        filas[^1].Acumulado.Should().Be(100m);
    }

    [Fact]
    public void Otros_umbrales_cambian_la_clasificacion()
    {
        var umbrales = new UmbralesAbc(50m, 30m, 20m);
        Clasificar(umbrales, ("P1", 50m), ("P2", 30m), ("P3", 20m)).Select(f => f.Clase).Should().Equal(ClaseAbc.A, ClaseAbc.B, ClaseAbc.C);
    }

    [Theory]
    [InlineData("80/15/5", 80, 15, 5)]
    [InlineData("70/20/10", 70, 20, 10)]
    [InlineData(" 80 / 15 / 5 ", 80, 15, 5)]
    public void Los_umbrales_se_leen_del_texto_del_parametro(string texto, int a, int b, int c)
    {
        var r = UmbralesAbc.Interpretar(texto);

        r.Admitido.Should().BeTrue();
        r.Umbrales.Should().Be(new UmbralesAbc(a, b, c));
    }

    [Theory]
    [InlineData("80/15/10")]
    [InlineData("80/10/5")]
    [InlineData("0/50/50")]
    [InlineData("80/15")]
    [InlineData("A/B/C")]
    [InlineData("")]
    public void Un_texto_de_umbrales_que_no_suma_100_o_no_tiene_la_forma_no_se_admite(string texto)
    {
        var r = UmbralesAbc.Interpretar(texto);

        r.Admitido.Should().BeFalse();
        r.Codigo.Should().Be("Parameters.ValueNotAllowed");
        r.Mensaje.Should().NotBeNullOrWhiteSpace();
    }
}
