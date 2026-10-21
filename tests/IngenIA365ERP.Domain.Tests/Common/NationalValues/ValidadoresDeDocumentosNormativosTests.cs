using FluentAssertions;
using IngenIA365ERP.Domain.Common.NationalValues;

namespace IngenIA365ERP.Domain.Tests.Common.NationalValues;

/// <summary>
/// Feature 013, T0021 (T44, T45, R59; data-model parte 3 §3.2, §12.4): los lectores tipados y los validadores de los
/// documentos normativos que la plataforma valida al registrarlos (<c>IValidadorDeDocumentoNormativo</c>). Un documento
/// inválido no se guarda: el validador devuelve cada error con la ruta del JSON (<c>NationalValues.Document.Invalid</c>,
/// <c>data.errors[] { path, message }</c>). Los documentos de prueba están en <c>Documentos/</c>.
/// </summary>
public class ValidadoresDeDocumentosNormativosTests
{
    private static string Documento(string archivo) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Common", "NationalValues", "Documentos", archivo));

    private static CatalogoDeModalidades Catalogo()
    {
        var r = CatalogoDeModalidades.Leer(Documento("catalogo-de-modalidades.json"), CatalogoDeModalidades.VersionDeEsquemaVigente);
        r.Errores.Should().BeEmpty();
        return r.Documento!;
    }

    // ---------- Catálogo de modalidades ----------

    [Fact]
    public void El_catalogo_trae_las_nueve_modalidades()
    {
        Catalogo().Modalidades.Select(m => m.Code).Should().BeEquivalentTo(
            "CONSUMO", "COMERCIAL", "VIVIENDA", "MICROCREDITO", "POPULAR_PRODUCTIVO_RURAL", "POPULAR_PRODUCTIVO_URBANO",
            "PRODUCTIVO_RURAL", "PRODUCTIVO_URBANO", "PRODUCTIVO_MAYOR_MONTO");
    }

    [Fact]
    public void El_microcredito_se_divide_en_inmobiliario_y_empresarial()
    {
        Catalogo().Modalidades.Single(m => m.Code == "MICROCREDITO").Combinaciones.Select(c => c.SubsegmentCode)
            .Should().BeEquivalentTo("Inmobiliario", "Empresarial");
    }

    [Fact]
    public void Trae_todos_los_subsegmentos_fijados()
    {
        Catalogo().Modalidades.SelectMany(m => m.Combinaciones).Select(c => c.SubsegmentCode).Where(s => s is not null).Distinct()
            .Should().BeEquivalentTo("ConLibranza", "SinLibranza", "PersonaNatural", "PersonaJuridica", "Empleados", "Inmobiliario", "Empresarial");
    }

    [Fact]
    public void Cada_combinacion_tiene_segmento_SARC_y_clave_de_usura_o_tope_de_vivienda()
    {
        foreach (var c in Catalogo().Modalidades.SelectMany(m => m.Combinaciones))
        {
            CatalogoDeModalidades.SegmentosSarc.Should().Contain(c.SarcSegment);
            (c.UsuryKey is not null ^ c.HousingRateCap).Should().BeTrue($"{c.ModalityCode}/{c.SubsegmentCode}: una clave de usura o el tope de vivienda, no los dos");
            if (c.ExpectedLossSegment is not null)
                CatalogoDeModalidades.SegmentosDePerdidaEsperada.Should().Contain(c.ExpectedLossSegment);
        }
    }

    [Fact]
    public void Busca_una_combinacion_por_modalidad_y_subsegmento()
    {
        var catalogo = Catalogo();

        var conLibranza = catalogo.Buscar("CONSUMO", "ConLibranza")!;
        conLibranza.SarcSegment.Should().Be("CONSUMO");
        conLibranza.ExpectedLossSegment.Should().Be("CONSUMO_LIBRANZA");
        conLibranza.UsuryKey.Should().Be("CONSUMO_Y_ORDINARIO");

        var vivienda = catalogo.Buscar("VIVIENDA", null)!;
        vivienda.HousingRateCap.Should().BeTrue();
        vivienda.UsuryKey.Should().BeNull();
        vivienda.AdmitsUvr.Should().BeTrue();

        catalogo.Buscar("COMERCIAL", "PersonaJuridica")!.SarcSegment.Should().Be("COMERCIAL_PJ");
        catalogo.Buscar("CONSUMO", "Inmobiliario").Should().BeNull("el sub-segmento no es de esa modalidad");
        catalogo.Buscar("CONSUMO", null).Should().BeNull("consumo exige sub-segmento");
        catalogo.Buscar("INEXISTENTE", null).Should().BeNull();
    }

    [Fact]
    public void Las_claves_de_usura_son_las_modalidades_certificadas()
    {
        Catalogo().ClavesDeUsura.Should().BeEquivalentTo(
            "CONSUMO_Y_ORDINARIO", "MICROCREDITO", "POPULAR_PRODUCTIVO_RURAL", "POPULAR_PRODUCTIVO_URBANO",
            "PRODUCTIVO_RURAL", "PRODUCTIVO_URBANO", "PRODUCTIVO_MAYOR_MONTO");
    }

    [Fact]
    public void Una_combinacion_sin_segmento_es_error_con_la_ruta_del_JSON()
    {
        const string json = """
            { "modalities": [ { "code": "CONSUMO", "name": "Consumo",
                "combinations": [ { "subsegment": "ConLibranza", "usuryKey": "CONSUMO_Y_ORDINARIO" } ] } ] }
            """;

        var errores = new ValidadorDeCatalogoDeModalidades().Validar(json, 1);

        errores.Should().ContainSingle(e => e.Ruta == "$.modalities[0].combinations[0].sarcSegment");
    }

    [Fact]
    public void Un_segmento_SARC_desconocido_es_error()
    {
        const string json = """
            { "modalities": [ { "code": "CONSUMO", "name": "Consumo",
                "combinations": [ { "subsegment": "ConLibranza", "sarcSegment": "INVENTADO", "usuryKey": "X" } ] } ] }
            """;

        new ValidadorDeCatalogoDeModalidades().Validar(json, 1)
            .Should().ContainSingle(e => e.Ruta == "$.modalities[0].combinations[0].sarcSegment");
    }

    [Fact]
    public void Clave_de_usura_y_tope_de_vivienda_a_la_vez_es_error()
    {
        const string json = """
            { "modalities": [ { "code": "VIVIENDA", "name": "Vivienda",
                "combinations": [ { "sarcSegment": "VIVIENDA", "usuryKey": "X", "housingRateCap": true } ] } ] }
            """;

        new ValidadorDeCatalogoDeModalidades().Validar(json, 1)
            .Should().ContainSingle(e => e.Ruta == "$.modalities[0].combinations[0]");
    }

    [Fact]
    public void Una_modalidad_repetida_es_error()
    {
        const string json = """
            { "modalities": [
                { "code": "VIVIENDA", "name": "Vivienda", "combinations": [ { "sarcSegment": "VIVIENDA", "housingRateCap": true } ] },
                { "code": "VIVIENDA", "name": "Otra", "combinations": [ { "sarcSegment": "VIVIENDA", "housingRateCap": true } ] } ] }
            """;

        new ValidadorDeCatalogoDeModalidades().Validar(json, 1).Should().ContainSingle(e => e.Ruta == "$.modalities[1].code");
    }

    [Fact]
    public void Un_subsegmento_repetido_o_mezclado_con_uno_vacio_es_error()
    {
        const string json = """
            { "modalities": [ { "code": "CONSUMO", "name": "Consumo", "combinations": [
                { "subsegment": "ConLibranza", "sarcSegment": "CONSUMO", "usuryKey": "X" },
                { "subsegment": "ConLibranza", "sarcSegment": "CONSUMO", "usuryKey": "X" },
                { "sarcSegment": "CONSUMO", "usuryKey": "X" } ] } ] }
            """;

        var errores = new ValidadorDeCatalogoDeModalidades().Validar(json, 1);
        errores.Select(e => e.Ruta).Should().BeEquivalentTo("$.modalities[0].combinations[1].subsegment", "$.modalities[0].combinations[2].subsegment");
    }

    [Fact]
    public void Un_JSON_mal_formado_o_una_version_de_esquema_desconocida_es_error()
    {
        new ValidadorDeCatalogoDeModalidades().Validar("{ no es json", 1).Should().ContainSingle(e => e.Ruta == "$");
        new ValidadorDeCatalogoDeModalidades().Validar(Documento("catalogo-de-modalidades.json"), 99)
            .Should().ContainSingle(e => e.Ruta == "schemaVersion");
        new ValidadorDeCatalogoDeModalidades().Validar("""{ "modalities": [] }""", 1)
            .Should().ContainSingle(e => e.Ruta == "$.modalities");
    }

    [Fact]
    public void El_validador_declara_su_clase()
    {
        new ValidadorDeCatalogoDeModalidades().Clase.Should().Be(RegulatoryDocumentKind.CatalogoDeModalidades);
        new ValidadorDeTiposDeOrganizacion().Clase.Should().Be(RegulatoryDocumentKind.TiposDeOrganizacion);
    }

    // ---------- Tipos de organización ----------

    private static TiposDeOrganizacion Tipos()
    {
        var r = TiposDeOrganizacion.Leer(Documento("tipos-de-organizacion.json"), TiposDeOrganizacion.VersionDeEsquemaVigente);
        r.Errores.Should().BeEmpty();
        return r.Documento!;
    }

    [Fact]
    public void Lee_los_tipos_y_las_categorias_de_fondos_de_empleados()
    {
        var tipos = Tipos();
        tipos.Tipos.Should().HaveCount(6);
        tipos.Buscar("FONDO_EMPLEADOS")!.IsEmployeeFund.Should().BeTrue();
        tipos.Buscar("COOP_AHORRO_CREDITO")!.ExpectedLoss.Should().Be(PerdidaEsperadaDelTipo.Admitted);
        tipos.Buscar("NO_EXISTE").Should().BeNull();
        tipos.CategoriasDeFondoDeEmpleados.Select(c => c.Code).Should().BeEquivalentTo("PLENA", "INTERMEDIA", "BASICA");
    }

    [Fact]
    public void La_categoria_solo_vale_para_un_fondo_de_empleados_y_debe_existir()
    {
        var tipos = Tipos();

        tipos.CategoriaValida("FONDO_EMPLEADOS", "PLENA").Should().BeTrue();
        tipos.CategoriaValida("FONDO_EMPLEADOS", "INVENTADA").Should().BeFalse();
        tipos.CategoriaValida("FONDO_EMPLEADOS", null).Should().BeFalse("un fondo de empleados declara su categoría");
        tipos.CategoriaValida("COOP_AHORRO_CREDITO", "PLENA").Should().BeFalse("una cooperativa no tiene categoría de fondo");
        tipos.CategoriaValida("COOP_AHORRO_CREDITO", null).Should().BeTrue();
        tipos.CategoriaValida("NO_EXISTE", null).Should().BeFalse();
    }

    [Fact]
    public void Un_codigo_de_tipo_repetido_es_error()
    {
        const string json = """
            { "employeeFundCategories": [],
              "organizationTypes": [
                { "code": "COOP_AHORRO_CREDITO", "name": "A", "isEmployeeFund": false, "expectedLoss": "Admitted" },
                { "code": "COOP_AHORRO_CREDITO", "name": "B", "isEmployeeFund": false, "expectedLoss": "Admitted" } ] }
            """;

        new ValidadorDeTiposDeOrganizacion().Validar(json, 1).Should().ContainSingle(e => e.Ruta == "$.organizationTypes[1].code");
    }

    [Fact]
    public void Un_fondo_de_empleados_sin_categorias_declaradas_es_error()
    {
        const string json = """
            { "employeeFundCategories": [],
              "organizationTypes": [ { "code": "FONDO_EMPLEADOS", "name": "F", "isEmployeeFund": true, "expectedLoss": "Admitted" } ] }
            """;

        new ValidadorDeTiposDeOrganizacion().Validar(json, 1).Should().ContainSingle(e => e.Ruta == "$.employeeFundCategories");
    }

    [Fact]
    public void Una_regla_de_perdida_esperada_desconocida_es_error()
    {
        const string json = """
            { "employeeFundCategories": [],
              "organizationTypes": [ { "code": "X", "name": "X", "isEmployeeFund": false, "expectedLoss": "Quizas" } ] }
            """;

        new ValidadorDeTiposDeOrganizacion().Validar(json, 1).Should().ContainSingle(e => e.Ruta == "$.organizationTypes[0].expectedLoss");
    }
}
