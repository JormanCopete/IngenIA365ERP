using FluentAssertions;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Application.Tests.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, T0141 (T45, R2, R54; quickstart §1.5): el validador de Cartera registrado para <c>SarcAnexo1</c> y
/// <c>ReglaDePerdidaEsperada</c> acepta los JSON semilla de la central y rechaza con <c>NationalValues.Document.Invalid</c> un
/// contenido que no se lee como <see cref="Anexo1"/> o <see cref="ReglaDePerdidaEsperada"/>, o que rompe sus reglas de forma (rangos
/// cruzados, porcentajes fuera de 0..1, segmento desconocido). <c>SarcAnexo2</c> lo agrega K6a.
/// </summary>
public class ValidadorDeDocumentoNormativoDeCarteraTests
{
    private static readonly string Anexo1Semilla = PlatformNationalValuesSeeder.Contenido("sarc-anexo-1.json");
    private static readonly string ReglaSemilla = PlatformNationalValuesSeeder.Contenido("regla-de-perdida-esperada.json");
    private static readonly DateOnly Desde = new(2026, 1, 1);

    [Fact]
    public void Hay_un_validador_por_clase_y_el_del_Anexo_2_rechaza_hasta_K6a()
    {
        // US9 (T0691): el del Anexo 2 existe y rechaza todo contenido mientras la entrega desplegada no sea K6a.
        ValidadorDeDocumentoNormativoDeCartera.Todos.Select(v => v.Clase)
            .Should().BeEquivalentTo([RegulatoryDocumentKind.SarcAnexo1, RegulatoryDocumentKind.ReglaDePerdidaEsperada, RegulatoryDocumentKind.SarcAnexo2]);
        ValidadorDeDocumentoNormativoDeCartera.Anexo2.Validar("{}", 1).Should().ContainSingle(e => e.Mensaje.Contains("K6a"));
    }

    [Fact]
    public void La_semilla_del_Anexo_1_trae_las_reglas_de_US9_y_todo_pendiente_de_validar()
    {
        // US9 (T0691; E8): crédito nuevo, insolvencia y la procedencia de cada tabla, ninguna cotejada todavía.
        var anexo = Anexo1.Leer(Anexo1Semilla, 1).Documento!;
        anexo.CreditoNuevo.Should().NotBeNull();
        anexo.Insolvencia.Should().NotBeNull();
        anexo.Procedencia.Should().NotBeEmpty().And.OnlyContain(p => !p.Verificada && p.Fuente.Length > 0);
        anexo.Procedencia.Select(p => p.Tabla).Should().Contain(anexo.Segmentos.Select(s => "segments." + s.Codigo));
        anexo.TablasSinCotejar.Should().HaveCount(anexo.Procedencia.Count);
        ReglaDePerdidaEsperada.Leer(ReglaSemilla, 1).Documento!.Procedencia.Should().OnlyContain(p => !p.Verificada);
    }

    [Fact]
    public void Las_semillas_se_leen_sin_errores()
    {
        ValidadorDeDocumentoNormativoDeCartera.Anexo1.Validar(Anexo1Semilla, 1).Should().BeEmpty();
        ValidadorDeDocumentoNormativoDeCartera.ReglaDePerdidaEsperada.Validar(ReglaSemilla, 1).Should().BeEmpty();

        var anexo = Anexo1.Leer(Anexo1Semilla, 1).Documento!;
        anexo.Segmentos.Select(s => s.Codigo).Should().BeEquivalentTo(Anexo1.SegmentosConocidos);
        anexo.Segmento("CONSUMO")!.Categorias.Select(c => c.Categoria).Should().Equal(Enum.GetValues<RatingCategory>());
        anexo.DeterioroGeneral.Should().NotBeEmpty();

        var regla = ReglaDePerdidaEsperada.Leer(ReglaSemilla, 1).Documento!;
        regla.SegmentosCubiertos.Should().BeEquivalentTo(ReglaDePerdidaEsperada.SegmentosConocidos);
        regla.MesesDeHistorial.Should().BePositive();
    }

    [Theory]
    [InlineData("\"maxDaysPastDue\": 60 }", "\"maxDaysPastDue\": 75 }", "rango cruzado de la categoría B de consumo")]
    [InlineData("\"rate\": 0.10 }", "\"rate\": 10 }", "porcentaje fuera de 0..1")]
    [InlineData("\"code\": \"MICROCREDITO\"", "\"code\": \"LEASING\"", "segmento desconocido")]
    [InlineData("\"admissibleFraction\": 0.50 }", "\"admissibleFraction\": 1.5 }", "fracción de garantía fuera de 0..1")]
    // US9 (T0684): huecos entre categorías, subcategorías tipadas y tramos de garantía.
    [InlineData("{ \"category\": \"B\", \"minDaysPastDue\": 31,", "{ \"category\": \"B\", \"minDaysPastDue\": 40,", "hueco entre A (hasta 30) y B")]
    [InlineData("\"subcategory\": \"E1\"", "\"subcategory\": \"E9\"", "subcategoría que no existe")]
    [InlineData("{ \"fromMonths\": 12, \"toMonths\": 24,", "{ \"fromMonths\": 14, \"toMonths\": 24,", "hueco entre tramos de garantía")]
    public void El_Anexo_1_que_rompe_la_forma_se_rechaza(string original, string roto, string porque)
    {
        Anexo1Semilla.Should().Contain(original);
        var contenido = ReemplazarPrimero(Anexo1Semilla, original, roto);

        ValidadorDeDocumentoNormativoDeCartera.Anexo1.Validar(contenido, 1).Should().NotBeEmpty(porque);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("no es json")]
    [InlineData("{ \"segments\": \"CONSUMO\" }")]
    public void Lo_que_no_se_lee_como_Anexo_1_se_rechaza(string contenido)
    {
        ValidadorDeDocumentoNormativoDeCartera.Anexo1.Validar(contenido, 1).Should().NotBeEmpty();
    }

    [Fact]
    public void La_regla_de_perdida_esperada_rechaza_segmentos_desconocidos_y_meses_en_cero()
    {
        var segmento = ReglaSemilla.Replace("\"COMERCIAL_PN\" ]", "\"VIVIENDA\" ]");
        var meses = ReglaSemilla.Replace("\"historyMonths\": 36", "\"historyMonths\": 0");

        ValidadorDeDocumentoNormativoDeCartera.ReglaDePerdidaEsperada.Validar(segmento, 1).Should().ContainSingle(e => e.Ruta.StartsWith("$.coveredSegments"));
        ValidadorDeDocumentoNormativoDeCartera.ReglaDePerdidaEsperada.Validar(meses, 1).Should().Contain(e => e.Ruta == "$.historyMonths");
        ValidadorDeDocumentoNormativoDeCartera.ReglaDePerdidaEsperada.Validar(ReglaSemilla, 2).Should().ContainSingle(e => e.Ruta == "schemaVersion");
    }

    [Fact]
    public async Task El_registro_central_responde_Document_Invalid_y_acepta_la_semilla()
    {
        using var e = new RegistroCentralDePrueba();

        var invalido = await e.RegistrarDocumentoAsync(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.SarcAnexo1),
            "ANEXO1-ROTO", Desde, null, 1, ReemplazarPrimero(Anexo1Semilla, "\"rate\": 0.10 }", "\"rate\": 10 }"), false, "CBCF"));
        invalido.Error.Code.Should().Be(ErroresDeValoresNacionales.DocumentInvalidCode);

        (await e.RegistrarDocumentoAsync(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.SarcAnexo1),
            "ANEXO1-PRUEBA", Desde, null, 1, Anexo1Semilla, false, "CBCF"))).IsSuccess.Should().BeTrue();
        (await e.RegistrarDocumentoAsync(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.ReglaDePerdidaEsperada),
            "CE100-PRUEBA", Desde, null, 1, ReglaSemilla, false, "CE 100"))).IsSuccess.Should().BeTrue();
    }

    private static string ReemplazarPrimero(string texto, string viejo, string nuevo)
    {
        var i = texto.IndexOf(viejo, StringComparison.Ordinal);
        return i < 0 ? texto : texto[..i] + nuevo + texto[(i + viejo.Length)..];
    }
}
