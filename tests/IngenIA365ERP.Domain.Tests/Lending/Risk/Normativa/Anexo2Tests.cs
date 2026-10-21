using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk.Normativa;

/// <summary>
/// Feature 013, K6a (T1016; T45, R54; data-model §23.10): el documento tipado <see cref="Anexo2"/>. El JSON semilla (<c>sarc-anexo-2.json</c>,
/// CE 68 de 2024 transcrita sin cotejar, E8) se lee sin errores; las variables tienen tipo cerrado; los cortes crecen; la PI va por tipo de
/// organización y admite la clave VOLUNTARIA; la PDI va por tipo de garantía y tramos de días; la homologación cubre todas las
/// calificaciones; y la política del consejo sólo endurece celdas del Anexo 2 (<see cref="PoliticaSarcEfectiva.CombinarAnexo2"/>).
/// </summary>
public class Anexo2Tests
{
    private static readonly string Semilla = PlatformNationalValuesSeeder.Contenido("sarc-anexo-2.json");

    private static Anexo2 Sembrado() => Anexo2.Leer(Semilla, 1).Documento!;

    /// <summary>El Anexo 2 de prueba (<c>Normas/anexo2-prueba.json</c>), con la PI VOLUNTARIA inventada.</summary>
    private static Anexo2 Prueba() =>
        Anexo2.Leer(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Lending", "Risk", "Normas", "anexo2-prueba.json")), 1).Documento!;

    private static IReadOnlyList<string> Errores(string contenido) => Anexo2.Leer(contenido, 1).Errores.Select(e => $"{e.Ruta} {e.Mensaje}").ToList();

    private static string Reemplazar(string original, string viejo, string nuevo)
    {
        original.Should().Contain(viejo);
        var i = original.IndexOf(viejo, StringComparison.Ordinal);
        return original[..i] + nuevo + original[(i + viejo.Length)..];
    }

    [Fact]
    public void La_semilla_se_lee_y_trae_los_tres_segmentos_con_todo_pendiente_de_validar()
    {
        var r = Anexo2.Leer(Semilla, 1);

        r.Errores.Should().BeEmpty();
        var a = r.Documento!;
        a.Segmentos.Select(s => s.Codigo).Should().Equal(ReglaDePerdidaEsperada.SegmentosConocidos);
        a.Segmentos.Select(s => s.SegmentoSarc).Should().Equal("CONSUMO", "CONSUMO", "COMERCIAL_PN");
        a.DecimalesDelPuntaje.Should().Be(4);
        a.Procedencia.Should().NotBeEmpty().And.OnlyContain(p => !p.Verificada && p.Fuente.Length > 0, "E8: nada se cotejó todavía");
        a.TablasSinCotejar.Should().Contain(["probabilityOfDefault", "lossGivenDefault", "homologation"]);
        a.SegmentoObligatorio("CONSUMO_LIBRANZA").Terminos.Should().HaveCount(9);
        a.SegmentoObligatorio("COMERCIAL_PN").DiasDeMoraDeIncumplimiento.Should().Be(120);
    }

    [Fact]
    public void Las_variables_tienen_tipo_cerrado_y_sus_parametros()
    {
        var a = Sembrado();

        a.Variable("EA").Tipo.Should().Be(TipoDeVariableDelModelo.ActiveAssociate);
        a.Variable("VALCUOTA").Should().Match<VariableDelModelo>(v => v.Tipo == TipoDeVariableDelModelo.InstallmentAtMostSmmlvFraction
            && v.Fraccion == 0.10m && v.SoloFondoDeEmpleados);
        a.Variable("FONDPLAZO").Meses.Should().Be(6);
        a.Variable("MORTRIM").Condiciones.Should().ContainSingle().Which.Medida.Should().Be(MedidaDeMora.AnyMonth);
        a.Variable("MORA2430N").Condiciones.Should().HaveCount(2);

        Errores(Reemplazar(Semilla, "\"kind\": \"ActiveAssociate\"", "\"kind\": \"Edad\"")).Should().Contain(e => e.Contains("kind"));
        Errores(Reemplazar(Semilla, "\"kind\": \"ActiveAssociate\"", "\"kind\": \"1\"")).Should().Contain(e => e.Contains("kind"),
            "el tipo va por su nombre, nunca por número");
        Errores(Reemplazar(Semilla, "{ \"variable\": \"EA\", \"coefficient\": -2.081 }", "{ \"variable\": \"EDAD\", \"coefficient\": -2.081 }"))
            .Should().Contain(e => e.Contains("EDAD no está definida"));
        Errores(Reemplazar(Semilla, "\"measure\": \"AnyMonth\"", "\"measure\": \"Promedio\"")).Should().Contain(e => e.Contains("measure"));
    }

    [Fact]
    public void Los_cortes_van_de_A_a_E_crecientes_y_terminan_en_1()
    {
        var a = Sembrado();

        a.CalificacionPorPuntaje("CONSUMO_LIBRANZA", 0.1648m).Should().Be(RatingCategory.A, "el corte es el límite superior, inclusive");
        a.CalificacionPorPuntaje("CONSUMO_LIBRANZA", 0.1649m).Should().Be(RatingCategory.B);
        a.CalificacionPorPuntaje("COMERCIAL_PN", 0.99m).Should().Be(RatingCategory.E);

        Errores(Reemplazar(Semilla, "{ \"rating\": \"B\", \"upTo\": 0.2481 }", "{ \"rating\": \"B\", \"upTo\": 0.1 }")).Should().Contain(e => e.Contains("crecientes"));
        Errores(Reemplazar(Semilla, "{ \"rating\": \"E\", \"upTo\": 1 }", "{ \"rating\": \"E\", \"upTo\": 0.9 }"))
            .Should().Contain(e => e.Contains("El corte de E es 1"));
        Errores(Reemplazar(Semilla, "{ \"rating\": \"A\", \"upTo\": 0.1648 }, ", string.Empty)).Should().Contain(e => e.Contains("A, B, C, D, E"));
    }

    [Fact]
    public void La_PI_va_por_segmento_y_tipo_de_organizacion_y_admite_la_clave_voluntaria()
    {
        var a = Sembrado();

        a.TablaDePi("CONSUMO_LIBRANZA", "COOP_AHORRO_CREDITO")!.De(RatingCategory.A).Should().Be(0.005m);
        a.TablaDePi("CONSUMO_SIN_LIBRANZA", "FONDO_EMPLEADOS")!.De(RatingCategory.E).Should().Be(0.5014m);
        a.TablaDePi("COMERCIAL_PN", "ASOCIACION_MUTUAL")!.Incumplido.Should().Be(1m);
        a.ProbabilidadesDeIncumplimiento.Should().HaveCount(22, "8 tipos en libranza, 7 sin libranza y 7 en comercial persona natural");
        a.TienePiVoluntaria("CONSUMO_LIBRANZA").Should().BeFalse("la PI voluntaria de la CE 100 no se transcribió: falta el texto");
        a.TablaDePi("CONSUMO_LIBRANZA", "COOP_INTEGRAL_SECCION_AC").Should().BeNull("las tablas de la CE 68 no traen integrales con sección de ahorro");

        var prueba = Prueba();
        prueba.TienePiVoluntaria("COMERCIAL_PN").Should().BeTrue();
        prueba.TablaDePi("COMERCIAL_PN", Anexo2.ClaveVoluntaria)!.De(RatingCategory.C).Should().Be(0.2m);

        Errores(Reemplazar(Semilla, "{ \"rating\": \"A\", \"rate\": 0.0067 }", "{ \"rating\": \"A\", \"rate\": 0.67 }")).Should().BeEmpty("0,67 cabe en 0..1");
        Errores(Reemplazar(Semilla, "{ \"rating\": \"A\", \"rate\": 0.0067 }", "{ \"rating\": \"A\", \"rate\": 67 }")).Should().Contain(e => e.Contains("fracción"));
        Errores(Reemplazar(Semilla, "\"organizationType\": \"COOP_TRABAJO_ASOCIADO\"", "\"organizationType\": \"ASOCIACION_MUTUAL\""))
            .Should().Contain(e => e.Contains("repetida"));
        Errores(Reemplazar(Semilla, "{ \"segment\": \"COMERCIAL_PN\", \"organizationType\": \"ASOCIACION_MUTUAL\"", "{ \"segment\": \"VIVIENDA\", \"organizationType\": \"ASOCIACION_MUTUAL\""))
            .Should().Contain(e => e.Contains("VIVIENDA no está en segments"));
    }

    [Fact]
    public void La_PDI_va_por_tipo_de_garantia_y_tramos_de_dias_en_incumplimiento()
    {
        var a = Sembrado();

        var sinGarantia = a.Pdi("SIN_GARANTIA")!;
        sinGarantia.Idonea.Should().BeFalse();
        sinGarantia.EnIncumplimiento(0).Should().Be(0.75m);
        sinGarantia.EnIncumplimiento(29).Should().Be(0.75m);
        sinGarantia.EnIncumplimiento(30).Should().Be(0.85m);
        sinGarantia.EnIncumplimiento(90).Should().Be(1m);
        a.Pdi("HIPOTECARIA")!.EnIncumplimiento(719).Should().Be(0.70m);
        a.Pdi("HIPOTECARIA")!.EnIncumplimiento(720).Should().Be(1m);
        a.Pdi("FONDOS_GARANTIAS")!.Tramos.Should().ContainSingle().Which.Fraccion.Should().Be(0.12m);
        a.SegmentoObligatorio("CONSUMO_LIBRANZA").PdiSinIncumplimiento.Single(p => p.TipoDeGarantia == "SIN_GARANTIA").Fraccion.Should().Be(0.45m);
        a.SegmentoObligatorio("CONSUMO_SIN_LIBRANZA").PdiSinIncumplimiento.Single(p => p.TipoDeGarantia == "SIN_GARANTIA").Fraccion.Should().Be(0.50m);

        Errores(Reemplazar(Semilla, "{ \"fromDaysAfterDefault\": 0, \"rate\": 0.40 }", "{ \"fromDaysAfterDefault\": 10, \"rate\": 0.40 }"))
            .Should().Contain(e => e.Contains("empieza en 0 días"));
        // La primera aparición es la de derechos de cobro (0, 360, 720): 100 después de 360 no crece.
        Errores(Reemplazar(Semilla, "{ \"fromDaysAfterDefault\": 720, \"rate\": 1 }", "{ \"fromDaysAfterDefault\": 100, \"rate\": 1 }"))
            .Should().Contain(e => e.Contains("días crecientes"));
        Errores(Reemplazar(Semilla, "\"performingLossGivenDefault\": [ { \"collateralType\": \"NO_IDONEA\", \"rate\": 0.45 }, { \"collateralType\": \"SIN_GARANTIA\", \"rate\": 0.45 } ]",
                "\"performingLossGivenDefault\": [ { \"collateralType\": \"AVAL\", \"rate\": 0.45 } ]"))
            .Should().Contain(e => e.Contains("AVAL no está en la tabla de PDI"));
    }

    [Fact]
    public void La_homologacion_cubre_todas_las_calificaciones_sin_huecos()
    {
        var a = Sembrado();
        var consumo = a.HomologacionDe("CONSUMO_SIN_LIBRANZA");

        consumo.Segmentos.Should().Equal("CONSUMO_LIBRANZA", "CONSUMO_SIN_LIBRANZA");
        foreach (var c in Enum.GetValues<RatingCategory>())
            for (var mora = 0; mora <= 400; mora += 5)
                consumo.Filas.Should().ContainSingle(f => f.CalificacionDelModelo == c && f.Contiene(mora), $"{c} con {mora} días");
        consumo.Filas.Where(f => f.CalificacionDelModelo is null).Select(f => f.MoraDesde).Should().Equal(91, 181);
        a.HomologacionDe("COMERCIAL_PN").Filas.Where(f => f.CalificacionDelModelo is null).Select(f => f.MoraDesde).Should().Equal(121, 151);

        Errores(Reemplazar(Semilla, "{ \"modelRating\": \"B\", \"fromCurrentDaysPastDue\": 31, \"accountingRating\": \"B\" },",
                "{ \"modelRating\": \"B\", \"fromCurrentDaysPastDue\": 40, \"accountingRating\": \"B\" },")).Should().Contain(e => e.Contains("hueco"));
        Errores(Reemplazar(Semilla, "{ \"modelRating\": \"D\", \"fromCurrentDaysPastDue\": 0, \"accountingRating\": \"C\" },", string.Empty))
            .Should().Contain(e => e.Contains("Falta la homologación de D"));
        Errores(Reemplazar(Semilla, "{ \"modelRating\": \"Default\", \"fromCurrentDaysPastDue\": 91,", "{ \"modelRating\": \"Default\", \"fromCurrentDaysPastDue\": 61,"))
            .Should().Contain(e => e.Contains("umbral de incumplimiento"));
        Errores(Reemplazar(Semilla, "\"segments\": [ \"COMERCIAL_PN\" ],", "\"segments\": [ \"CONSUMO_LIBRANZA\" ],"))
            .Should().Contain(e => e.Contains("ya tiene homologación")).And.Contain(e => e.Contains("COMERCIAL_PN no tiene homologación"));
    }

    [Fact]
    public void El_deterioro_general_va_por_tipo_de_organizacion_con_el_comodin()
    {
        Sembrado().DeterioroGeneralAplica("COOP_AHORRO_CREDITO").Should().BeTrue();
        var prueba = Prueba();
        prueba.DeterioroGeneralAplica("COOP_MULTIACTIVA").Should().BeFalse("la fila propia manda sobre «*»");
        prueba.DeterioroGeneralAplica("FONDO_EMPLEADOS").Should().BeTrue();
    }

    [Fact]
    public void La_version_de_esquema_y_el_documento_vacio_se_rechazan()
    {
        Anexo2.Leer(Semilla, 2).Errores.Should().ContainSingle(e => e.Ruta == "schemaVersion");
        Anexo2.Leer("{}", 1).Documento.Should().BeNull();
        Anexo2.Leer("", 1).Errores.Should().ContainSingle(e => e.Mensaje.Contains("vacío"));
    }

    [Fact]
    public void La_politica_solo_endurece_celdas_del_Anexo_2()
    {
        var norma = Sembrado();
        var politica = PoliticaSarc.Leer("""
            { "expectedLoss": {
                "probabilityOfDefault": [ { "segment": "CONSUMO_LIBRANZA", "organizationType": "COOP_AHORRO_CREDITO", "rating": "A", "rate": 0.01 },
                                          { "segment": "CONSUMO_LIBRANZA", "organizationType": "COOP_AHORRO_CREDITO", "rating": "B", "rate": 0.001 } ],
                "lossGivenDefault": [ { "collateralType": "HIPOTECARIA", "fromDaysAfterDefault": 0, "rate": 0.45 } ],
                "performingLossGivenDefault": [ { "segment": "CONSUMO_SIN_LIBRANZA", "collateralType": "SIN_GARANTIA", "rate": 0.40 } ] } }
            """, 1).Documento!;

        var r = PoliticaSarcEfectiva.CombinarAnexo2(norma, politica);

        r.Errores.Should().BeEmpty();
        r.Efectiva.TablaDePi("CONSUMO_LIBRANZA", "COOP_AHORRO_CREDITO")!.De(RatingCategory.A).Should().Be(0.01m, "más alta es más estricta");
        r.Efectiva.TablaDePi("CONSUMO_LIBRANZA", "COOP_AHORRO_CREDITO")!.De(RatingCategory.B).Should().Be(0.006m, "menos estricta: rige la norma");
        r.Efectiva.Pdi("HIPOTECARIA")!.EnIncumplimiento(0).Should().Be(0.45m);
        r.Efectiva.SegmentoObligatorio("CONSUMO_SIN_LIBRANZA").PdiSinIncumplimiento.Single(p => p.TipoDeGarantia == "SIN_GARANTIA").Fraccion.Should().Be(0.50m);
        r.MenosEstrictas.Select(c => c.Ruta).Should().BeEquivalentTo(
            "expectedLoss.probabilityOfDefault.CONSUMO_LIBRANZA.COOP_AHORRO_CREDITO.B", "expectedLoss.performingLossGivenDefault.CONSUMO_SIN_LIBRANZA.SIN_GARANTIA");
        r.Celdas.Should().HaveCount(4);
        r.Efectiva.SegmentoObligatorio("CONSUMO_LIBRANZA").Cortes.Should().Equal(norma.SegmentoObligatorio("CONSUMO_LIBRANZA").Cortes,
            "los cortes y los coeficientes son el modelo: la política no los toca");

        var ajena = PoliticaSarc.Leer("""{ "expectedLoss": { "probabilityOfDefault": [ { "segment": "CONSUMO_LIBRANZA", "organizationType": "COOP_INTEGRAL_SECCION_AC", "rating": "A", "rate": 0.5 } ] } }""", 1).Documento!;
        PoliticaSarcEfectiva.CombinarAnexo2(norma, ajena).Errores.Should().ContainSingle(e => e.Ruta.Contains("COOP_INTEGRAL_SECCION_AC"));
        PoliticaSarcEfectiva.CombinarAnexo2(norma, null).Celdas.Should().BeEmpty();
    }
}
