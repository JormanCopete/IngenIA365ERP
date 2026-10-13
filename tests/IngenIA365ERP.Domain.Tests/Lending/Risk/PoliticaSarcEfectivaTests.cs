using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9 (T0673; T45, R54; FR-099, FR-103, FR-104): <see cref="PoliticaSarcEfectiva.Combinar"/> da lo más estricto por
/// celda entre la norma vigente y la política validada; si la norma se endurece después, gana la norma; una celda menos estricta se
/// detecta con su ruta (es <c>Lending.RiskPolicy.LessStrict</c> al registrar) y una que la norma no trae, también. Y
/// <see cref="PoliticaSarc.Leer"/> valida el esquema parcial.
/// </summary>
public class PoliticaSarcEfectivaTests
{
    private static readonly Anexo1 Norma = CasoDoradoDeRiesgo.LeerNorma("anexo1-prueba");

    private static PoliticaSarc Politica(string json)
    {
        var r = PoliticaSarc.Leer(json, 1);
        r.Errores.Should().BeEmpty();
        return r.Documento!;
    }

    [Fact]
    public void Sin_politica_rige_la_norma_tal_cual()
    {
        var r = PoliticaSarcEfectiva.Combinar(Norma, null);
        r.Celdas.Should().BeEmpty();
        r.Errores.Should().BeEmpty();
        r.DeterioroGeneralAdicional.Should().Be(0m);
        r.Efectiva.Porcentaje("CONSUMO", RatingCategory.C, RatingSubcategory.None, 75, null).Fraccion.Should().Be(0.10m);
        r.Efectiva.CategoriaPorMora("CONSUMO", 31).Should().Be(RatingCategory.B);
    }

    [Fact]
    public void Gana_lo_mas_estricto_de_cada_celda()
    {
        var r = PoliticaSarcEfectiva.Combinar(Norma, Politica("""
            { "segments": [ { "code": "CONSUMO", "categories": [ { "category": "B", "minDaysPastDue": 21 } ],
                              "impairmentRates": [ { "category": "C", "rate": 0.15 }, { "category": "E", "subcategory": "E1", "rate": 0.6 } ] } ],
              "collateral": [ { "nature": "Mortgage", "brackets": [ { "fromMonths": 18, "admissibleFraction": 0.4 } ] } ],
              "generalImpairment": { "minimumRate": 0.02, "additionalRate": 0.003, "additionalCompensatesIndividual": true } }
            """));

        r.Errores.Should().BeEmpty();
        r.MenosEstrictas.Should().BeEmpty();
        var e = r.Efectiva;
        e.CategoriaPorMora("CONSUMO", 20).Should().Be(RatingCategory.A);
        e.CategoriaPorMora("CONSUMO", 21).Should().Be(RatingCategory.B, "B empieza en 21 y A termina en 20");
        e.Segmento("CONSUMO")!.Categorias[0].DiasHasta.Should().Be(20);
        e.CategoriaPorMora("COMERCIAL_PN", 21).Should().Be(RatingCategory.A, "la política sólo toca el segmento que nombra");
        e.Porcentaje("CONSUMO", RatingCategory.C, RatingSubcategory.None, 75, null).Fraccion.Should().Be(0.15m);
        e.Porcentaje("CONSUMO", RatingCategory.E, RatingSubcategory.E1, 200, null).Fraccion.Should().Be(0.6m);
        e.FraccionAdmisible(NaturalezaDeGarantia.Mortgage, 20).Should().Be(0.4m);
        e.FraccionAdmisible(NaturalezaDeGarantia.Mortgage, 10).Should().Be(0.70m);
        e.DeterioroGeneralDe(null)!.FraccionMinima.Should().Be(0.02m);
        r.DeterioroGeneralAdicional.Should().Be(0.003m);
        r.AdicionalCompensaIndividual.Should().BeTrue();
        r.Celdas.Should().Contain(c => c.Ruta == "segments.CONSUMO.impairmentRates.E.E1.from181" && c.GanaLaPolitica);

        Norma.Porcentaje("CONSUMO", RatingCategory.C, RatingSubcategory.None, 75, null).Fraccion.Should().Be(0.10m, "combinar no toca la norma");
    }

    [Fact]
    public void La_celda_menos_estricta_se_detecta_con_su_ruta_y_rige_la_norma()
    {
        var r = PoliticaSarcEfectiva.Combinar(Norma, Politica("""
            { "segments": [ { "code": "MICROCREDITO", "categories": [ { "category": "C", "minDaysPastDue": 70 } ],
                              "impairmentRates": [ { "category": "D", "rate": 0.3 } ] } ],
              "collateral": [ { "nature": "NonMortgage", "brackets": [ { "fromMonths": 0, "admissibleFraction": 0.8 } ] } ],
              "generalImpairment": { "minimumRate": 0.005 } }
            """));

        r.MenosEstrictas.Select(c => (c.Ruta, c.Norma, c.Propuesta)).Should().BeEquivalentTo(new[]
        {
            ("segments.MICROCREDITO.categories.C.minDaysPastDue", 61m, 70m),
            ("segments.MICROCREDITO.impairmentRates.D", 0.50m, 0.3m),
            ("collateral.NonMortgage.brackets.0.admissibleFraction", 0.70m, 0.8m),
            ("generalImpairment.minimumRate", 0.01m, 0.005m),
        });
        r.Efectiva.CategoriaPorMora("MICROCREDITO", 65).Should().Be(RatingCategory.C);
        r.Efectiva.Porcentaje("MICROCREDITO", RatingCategory.D, RatingSubcategory.None, 100, null).Fraccion.Should().Be(0.50m);
    }

    [Fact]
    public void Si_la_norma_se_endurece_despues_gana_la_norma()
    {
        var politica = Politica("""{ "segments": [ { "code": "CONSUMO", "impairmentRates": [ { "category": "C", "rate": 0.15 } ] } ] }""");

        PoliticaSarcEfectiva.Combinar(Norma, politica).MenosEstrictas.Should().BeEmpty();
        var despues = PoliticaSarcEfectiva.Combinar(CasoDoradoDeRiesgo.LeerNorma("anexo1-endurecido"), politica);
        despues.Efectiva.Porcentaje("CONSUMO", RatingCategory.C, RatingSubcategory.None, 75, null).Fraccion.Should().Be(0.25m);
        despues.MenosEstrictas.Should().ContainSingle(c => c.Ruta == "segments.CONSUMO.impairmentRates.C" && c.Efectiva == 0.25m);
    }

    [Fact]
    public void Una_celda_que_la_norma_no_trae_es_error_con_su_ruta()
    {
        var r = PoliticaSarcEfectiva.Combinar(Norma, Politica("""
            { "segments": [ { "code": "VIVIENDA", "impairmentRates": [ { "category": "E", "subcategory": "E2", "rate": 0.9 },
                                                                       { "category": "B", "subcategory": "E1", "rate": 0.9 } ] } ],
              "collateral": [ { "nature": "NonMortgage", "brackets": [ { "fromMonths": 6, "admissibleFraction": 0.5 } ] } ] }
            """));

        r.Errores.Select(e => e.Ruta).Should().BeEquivalentTo(
            "segments.VIVIENDA.impairmentRates.E.E2", "segments.VIVIENDA.impairmentRates.B.E1", "collateral.NonMortgage.brackets.6.admissibleFraction");
        r.Errores.Should().Contain(e => e.Mensaje.Contains("conditionFrom"), "vivienda tiene dos E2: la celda dice desde cuándo");

        var conDesde = PoliticaSarcEfectiva.Combinar(Norma, Politica(
            """{ "segments": [ { "code": "VIVIENDA", "impairmentRates": [ { "category": "E", "subcategory": "E2", "conditionFrom": 7, "rate": 0.9 } ] } ] }"""));
        conDesde.Errores.Should().BeEmpty();
        conDesde.Celdas.Should().ContainSingle(c => c.Ruta == "segments.VIVIENDA.impairmentRates.E.E2.from7" && c.Efectiva == 0.9m);
        conDesde.Efectiva.Porcentaje("VIVIENDA", RatingCategory.E, RatingSubcategory.E2, 900, 20).Fraccion.Should().Be(1m, "el otro E2 no cambia");
    }

    [Fact]
    public void Si_los_inicios_dejan_de_crecer_rige_la_norma_en_ese_segmento()
    {
        var r = PoliticaSarcEfectiva.Combinar(Norma, Politica(
            """{ "segments": [ { "code": "CONSUMO", "categories": [ { "category": "C", "minDaysPastDue": 25 } ] } ] }"""));

        r.Errores.Should().ContainSingle(e => e.Ruta == "segments.CONSUMO.categories");
        r.Efectiva.CategoriaPorMora("CONSUMO", 30).Should().Be(RatingCategory.A);
        r.Celdas.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{ "segments": [ { "code": "CONSUMO", "categories": [ { "category": "A", "minDaysPastDue": 0 } ] } ] }""", "$.segments[0].categories[0].category")]
    [InlineData("""{ "segments": [ { "code": "CONSUMO", "impairmentRates": [ { "category": "C", "rate": 15 } ] } ] }""", "$.segments[0].impairmentRates[0].rate")]
    [InlineData("""{ "segments": [ { "code": "LEASING" } ] }""", "$.segments[0].code")]
    [InlineData("""{ "generalImpairment": { "additionalCompensatesIndividual": true } }""", "$.generalImpairment.additionalCompensatesIndividual")]
    [InlineData("""{ "collateral": [ { "nature": "Prenda", "brackets": [] } ] }""", "$.collateral[0].nature")]
    public void El_esquema_parcial_rechaza_lo_que_no_es_una_celda(string json, string ruta)
    {
        PoliticaSarc.Leer(json, 1).Errores.Should().Contain(e => e.Ruta == ruta);
    }

    [Fact]
    public void La_politica_vacia_se_lee_y_no_cambia_nada()
    {
        var r = PoliticaSarc.Leer("{}", 1);
        r.EsValido.Should().BeTrue();
        PoliticaSarcEfectiva.Combinar(Norma, r.Documento).Celdas.Should().BeEmpty();
    }
}
