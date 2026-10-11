using FluentAssertions;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, T0243 (data-model §0.13, M15, §2.8): el SHA-256 canónico de <see cref="HuellaDeCartera"/>. El JSON canónico lleva
/// las claves en orden ordinal fijo (no el de la llamada), los decimales con punto, las fechas e instantes ISO y sale sin espacios;
/// la huella es hexadecimal en minúscula de 64. El texto y la huella esperados se calcularon fuera del programa (<c>sha256sum</c>
/// sobre el texto UTF-8 sin salto final), así que la prueba fija la estabilidad entre corridas y plataformas: no depende de la
/// cultura, del orden de inserción ni del motor de serialización.
/// </summary>
public class HuellaDeCarteraTests
{
    private const string CanonicoEsperado =
        "{\"activo\":true,\"desde\":\"2026-10-08\",\"fuente\":null,\"niveles\":[{\"codigo\":\"B\",\"rango\":20},{\"codigo\":\"A\",\"rango\":10}]," +
        "\"nombre\":\"Comité de crédito \\\"central\\\"\",\"registrado\":\"2026-10-08T14:30:05\",\"tasa\":0.1000,\"techo\":5000000,\"version\":3}";

    private const string HuellaEsperada = "20e4674312a3cdc9ad9332e2e02f7da25b3f4d54fe14ca958ce9fd79dc0d0f2c";

    private static HuellaDeCartera Ejemplo() => new HuellaDeCartera()
        .Entero("version", 3)
        .Texto("nombre", "Comité de crédito \"central\"")
        .Decimal("techo", 5000000.00m)
        .Decimal("tasa", 0.1m, decimales: 4)
        .Fecha("desde", new DateOnly(2026, 10, 8))
        .Instante("registrado", new DateTime(2026, 10, 8, 14, 30, 5))
        .Logico("activo", true)
        .Texto("fuente", null)
        // Las listas conservan el orden que les da quien llama: el orden de los elementos es dato.
        .Lista("niveles", new[] { ("B", 20), ("A", 10) }, (h, n) => h.Texto("codigo", n.Item1).Entero("rango", n.Item2));

    [Fact]
    public void El_texto_canonico_ordena_las_claves_y_no_depende_del_orden_de_insercion()
    {
        Ejemplo().Canonico().Should().Be(CanonicoEsperado);

        var alReves = new HuellaDeCartera()
            .Lista("niveles", new[] { ("B", 20), ("A", 10) }, (h, n) => h.Entero("rango", n.Item2).Texto("codigo", n.Item1))
            .Texto("fuente", null)
            .Logico("activo", true)
            .Instante("registrado", new DateTime(2026, 10, 8, 14, 30, 5))
            .Fecha("desde", new DateOnly(2026, 10, 8))
            .Decimal("tasa", 0.1000m, decimales: 4)
            .Decimal("techo", 5000000m)
            .Texto("nombre", "Comité de crédito \"central\"")
            .Entero("version", 3);
        alReves.Canonico().Should().Be(CanonicoEsperado);
    }

    [Fact]
    public void La_huella_es_la_calculada_fuera_del_programa_y_es_estable_entre_corridas()
    {
        var huella = Ejemplo().Calcular();
        huella.Should().Be(HuellaEsperada);
        huella.Should().MatchRegex("^[0-9a-f]{64}$");
        Enumerable.Range(0, 5).Select(_ => Ejemplo().Calcular()).Should().AllBe(HuellaEsperada);
        HuellaDeCartera.DeTexto(CanonicoEsperado).Should().Be(HuellaEsperada);
    }

    [Fact]
    public void La_huella_no_depende_de_la_cultura_del_hilo()
    {
        var cultura = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // es-CO escribe la coma decimal y el punto de miles: la huella no se entera.
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("es-CO");
            Ejemplo().Calcular().Should().Be(HuellaEsperada);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = cultura;
        }
    }

    [Fact]
    public void Sin_precision_declarada_cero_punto_diez_y_cero_punto_uno_son_lo_mismo()
    {
        // decimal guarda la escala (0.10m y 0.1m se escriben distinto con ToString): sin precisión declarada se normaliza.
        var conCero = new HuellaDeCartera().Decimal("v", 0.10m);
        var sinCero = new HuellaDeCartera().Decimal("v", 0.1m);
        conCero.Canonico().Should().Be("{\"v\":0.1}");
        conCero.Calcular().Should().Be(sinCero.Calcular());
        new HuellaDeCartera().Decimal("v", 100m).Canonico().Should().Be("{\"v\":100}");
        new HuellaDeCartera().Decimal("v", -2.50m).Canonico().Should().Be("{\"v\":-2.5}");
    }

    [Fact]
    public void Con_precision_declarada_el_valor_se_escribe_con_esos_decimales_y_eso_distingue_la_huella()
    {
        var aDos = new HuellaDeCartera().Decimal("v", 0.1m, decimales: 2);
        aDos.Canonico().Should().Be("{\"v\":0.10}");
        aDos.Calcular().Should().Be(new HuellaDeCartera().Decimal("v", 0.10m, decimales: 2).Calcular());

        // La precisión es parte del dato: 0,1 a dos decimales no es 0,1 sin precisión ni 0,1 a cuatro.
        aDos.Calcular().Should().NotBe(new HuellaDeCartera().Decimal("v", 0.1m).Calcular());
        aDos.Calcular().Should().NotBe(new HuellaDeCartera().Decimal("v", 0.1m, decimales: 4).Calcular());
    }

    [Fact]
    public void Un_valor_con_mas_decimales_que_los_declarados_se_rechaza_en_vez_de_redondearse()
    {
        var accion = () => new HuellaDeCartera().Decimal("v", 0.105m, decimales: 2);
        accion.Should().Throw<ArgumentException>().WithMessage("*0.105*2*");
    }

    [Fact]
    public void Una_clave_repetida_se_rechaza()
    {
        var accion = () => new HuellaDeCartera().Texto("a", "x").Entero("a", 1);
        accion.Should().Throw<ArgumentException>().WithMessage("*a*");
    }

    [Fact]
    public void Un_cambio_de_un_solo_dato_cambia_la_huella()
    {
        var otra = new HuellaDeCartera()
            .Entero("version", 3)
            .Texto("nombre", "Comité de crédito \"central\"")
            .Decimal("techo", 5000001m)
            .Decimal("tasa", 0.1m, decimales: 4)
            .Fecha("desde", new DateOnly(2026, 10, 8))
            .Instante("registrado", new DateTime(2026, 10, 8, 14, 30, 5))
            .Logico("activo", true)
            .Texto("fuente", null)
            .Lista("niveles", new[] { ("B", 20), ("A", 10) }, (h, n) => h.Texto("codigo", n.Item1).Entero("rango", n.Item2));
        otra.Calcular().Should().NotBe(HuellaEsperada);
    }

    /// <summary>
    /// US4 (T0467; T24, M15): la huella de una liquidación a (D, R) lleva el prefijo de la versión de reglas, no depende del orden en
    /// que el cargador (EF) entregue las operaciones y cambia si cambia la fecha o lo que se liquida.
    /// </summary>
    [Fact]
    public void La_huella_de_una_liquidacion_lleva_la_version_de_reglas_y_no_depende_del_orden_de_EF()
    {
        var entrada = Replay.CreditosDePrueba.Simple() with { };
        var tramo = new Domain.Lending.Replay.EntradaDeOperacion("T2", IngenIA365ERP.Domain.Enums.Lending.LoanOperationKind.Disbursement, new DateOnly(2027, 3, 1),
            Replay.CreditosDePrueba.Instante(new DateOnly(2027, 3, 1)), 2) { Monto = 1_000_000m };
        var conTramo = entrada with { Operaciones = [.. entrada.Operaciones, tramo] };
        var alReves = entrada with { Operaciones = [tramo, .. entrada.Operaciones] };
        var d = new DateOnly(2027, 4, 20);

        var estado = MotorDeCartera.Reconstruir(conTramo, d);
        estado.TextoCanonico.Should().StartWith("LND-2026.1|{\"cuotaMasAntigua\":");
        estado.Huella.Should().MatchRegex("^[0-9a-f]{64}$").And.Be(HuellaDeCartera.DeTexto(estado.TextoCanonico));
        MotorDeCartera.Reconstruir(alReves, d).Huella.Should().Be(estado.Huella, "el orden de llegada no es dato");
        MotorDeCartera.Reconstruir(conTramo, d).Huella.Should().Be(estado.Huella, "repetida, la misma");
        MotorDeCartera.Reconstruir(conTramo, d.AddDays(1)).Huella.Should().NotBe(estado.Huella, "otra D, otra liquidación");
        MotorDeCartera.Reconstruir(entrada, d).Huella.Should().NotBe(estado.Huella, "sin el tramo, otra liquidación");
    }

    [Fact]
    public void Los_identificadores_y_los_textos_con_control_se_escriben_de_forma_unica()
    {
        var id = Guid.Parse("6F9619FF-8B86-D011-B42D-00C04FC964FF");
        new HuellaDeCartera().Identificador("id", id).Texto("t", "a\nb\\c").Canonico()
            .Should().Be("{\"id\":\"6f9619ff-8b86-d011-b42d-00c04fc964ff\",\"t\":\"a\\u000ab\\\\c\"}");
        new HuellaDeCartera().Textos("l", ["x", null]).Canonico().Should().Be("{\"l\":[\"x\",null]}");
        new HuellaDeCartera().Canonico().Should().Be("{}");
        new HuellaDeCartera().Calcular().Should().Be("44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a");
    }
}
