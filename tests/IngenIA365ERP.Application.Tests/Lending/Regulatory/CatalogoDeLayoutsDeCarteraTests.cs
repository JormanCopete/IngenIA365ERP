using FluentAssertions;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Tests.Lending.Regulatory;

/// <summary>
/// T0988 (feature 013, K5, US16; T79, C72; adaptadores-y-archivos.md §7.2–§7.3): los layouts embebidos cargan y cumplen el esquema; uno mal
/// formado hace fallar la carga (como <c>PilaLayoutCatalog</c>); el vigente se elige por clase, central y fecha de fin del período; dos
/// vigentes cruzados de la misma clase y central son un error de carga; los campos sin cotejar se cuentan y no le quitan la vigencia al layout.
/// </summary>
public class CatalogoDeLayoutsDeCarteraTests
{
    private static readonly DateOnly FinDeDiciembre2026 = new(2026, 12, 31);

    [Fact]
    public void Los_layouts_embebidos_cargan_y_cumplen_el_esquema()
    {
        var catalogo = CatalogoDeLayoutsDeCartera.Embebido;
        // Feature 014, US17 (T220): el catálogo suma los formatos de captaciones; éstos los revisa FormatosDeCaptacionesTests.
        var deCartera = catalogo.Todos.Where(l => l.Kind is RegulatoryFileKind.Sicses or RegulatoryFileKind.CreditBureau).ToList();
        deCartera.Select(l => l.Code).Should().BeEquivalentTo("SICSES-CARTERA-v1", "CENTRAL-DATACREDITO-v1", "CENTRAL-TRANSUNION-v1");
        catalogo.Centrales.Should().Equal("DATACREDITO", "TRANSUNION");
        foreach (var l in deCartera)
        {
            l.Layout.Source.Should().Contain("por cotejar", $"{l.Code}: la fuente dice que falta cotejar (pregunta I7)");
            l.Notes.Should().NotBeEmpty($"{l.Code} lleva sus notas");
            l.Layout.TotalFields.Should().BePositive();
            l.Layout.UnverifiedFields.Should().Be(l.Layout.TotalFields, $"{l.Code}: ningún campo está cotejado todavía (I7)");
            l.Layout.ValidFrom.Should().Be(new DateOnly(2026, 12, 1));
            l.Layout.Records.Should().Contain(r => r.Code == "Detail");
            foreach (var campo in l.Layout.Records.SelectMany(r => r.Fields).Where(f => f.Required))
                (campo.Path ?? campo.Constant).Should().NotBeNullOrWhiteSpace($"{l.Code}, campo {campo.Number}: obligatorio con origen");
        }
    }

    [Fact]
    public void Elige_el_vigente_por_clase_central_y_fecha_de_fin_del_periodo()
    {
        var catalogo = CatalogoDeLayoutsDeCartera.Embebido;
        catalogo.Vigente(RegulatoryFileKind.Sicses, null, FinDeDiciembre2026)!.Code.Should().Be("SICSES-CARTERA-v1");
        catalogo.Vigente(RegulatoryFileKind.CreditBureau, "DATACREDITO", FinDeDiciembre2026)!.Code.Should().Be("CENTRAL-DATACREDITO-v1");
        catalogo.Vigente(RegulatoryFileKind.CreditBureau, "transunion", FinDeDiciembre2026)!.Code.Should().Be("CENTRAL-TRANSUNION-v1");
        catalogo.Vigente(RegulatoryFileKind.Sicses, null, new DateOnly(2026, 11, 30)).Should().BeNull("ningún layout cubre noviembre de 2026");
        catalogo.Vigente(RegulatoryFileKind.CreditBureau, "OTRA", FinDeDiciembre2026).Should().BeNull();
        catalogo.Vigente(RegulatoryFileKind.CreditBureau, null, FinDeDiciembre2026).Should().BeNull("una central sin código no existe");
        catalogo.ConoceLaCentral("DATACREDITO").Should().BeTrue();
        catalogo.ConoceLaCentral("OTRA").Should().BeFalse();
    }

    [Fact]
    public void Un_json_mal_formado_hace_fallar_la_carga()
    {
        var cargar = () => CatalogoDeLayoutsDeCartera.Cargar([("sicses-cartera-1-2026-12-01.json", "{ \"code\": ")]);
        cargar.Should().Throw<FormatException>().WithMessage("*sicses-cartera-1-2026-12-01.json*JSON*");
    }

    [Fact]
    public void Una_definicion_invalida_hace_fallar_la_carga()
    {
        var malo = Sicses("SICSES-CARTERA-v9", "2027-01-01", null, campos: """{ "number": 1, "name": "A", "order": 1, "length": 0, "kind": "A", "format": "Text", "path": "X" }""");
        var cargar = () => CatalogoDeLayoutsDeCartera.Cargar([("sicses-cartera-9-2027-01-01.json", malo)]);
        cargar.Should().Throw<FormatException>().WithMessage("*largo inválido*");
    }

    [Fact]
    public void Dos_vigentes_cruzados_de_la_misma_clase_y_central_son_un_error()
    {
        var cargar = () => CatalogoDeLayoutsDeCartera.Cargar([
            ("central-X-1-2027-01-01.json", Central("X", "CENTRAL-X-v1", "2027-01-01", null)),
            ("central-X-2-2027-06-01.json", Central("X", "CENTRAL-X-v2", "2027-06-01", null)),
        ]);
        cargar.Should().Throw<FormatException>().WithMessage("*CENTRAL-X-v1 y CENTRAL-X-v2*se cruzan*");
    }

    [Fact]
    public void Versiones_encadenadas_eligen_por_fecha_y_las_centrales_no_compiten_entre_si()
    {
        var catalogo = CatalogoDeLayoutsDeCartera.Cargar([
            ("central-X-1-2027-01-01.json", Central("X", "CENTRAL-X-v1", "2027-01-01", "2027-05-31")),
            ("central-X-2-2027-06-01.json", Central("X", "CENTRAL-X-v2", "2027-06-01", null)),
            ("central-Y-1-2027-01-01.json", Central("Y", "CENTRAL-Y-v1", "2027-01-01", null)),
            ("sicses-cartera-1-2027-01-01.json", Sicses("SICSES-CARTERA-v1", "2027-01-01", null)),
        ]);
        catalogo.Vigente(RegulatoryFileKind.CreditBureau, "X", new DateOnly(2027, 5, 31))!.Code.Should().Be("CENTRAL-X-v1");
        catalogo.Vigente(RegulatoryFileKind.CreditBureau, "X", new DateOnly(2027, 6, 30))!.Code.Should().Be("CENTRAL-X-v2");
        catalogo.Vigente(RegulatoryFileKind.CreditBureau, "Y", new DateOnly(2027, 6, 30))!.Code.Should().Be("CENTRAL-Y-v1");
        catalogo.Vigente(RegulatoryFileKind.Sicses, null, new DateOnly(2027, 6, 30))!.Code.Should().Be("SICSES-CARTERA-v1");
    }

    [Fact]
    public void Cuenta_los_campos_sin_cotejar_y_el_layout_sin_cotejar_sigue_vigente()
    {
        var campos = """
            { "number": 1, "name": "A", "order": 1, "length": 5, "kind": "A", "format": "Text", "path": "X", "verified": true },
            { "number": 2, "name": "B", "order": 2, "length": 5, "kind": "A", "format": "Text", "path": "Y" },
            { "number": 3, "name": "C", "order": 3, "length": 5, "kind": "A", "format": "Text", "path": "Z", "verified": false }
            """;
        var catalogo = CatalogoDeLayoutsDeCartera.Cargar([("sicses-cartera-1-2027-01-01.json", Sicses("SICSES-CARTERA-v1", "2027-01-01", null, campos))]);
        var l = catalogo.Vigente(RegulatoryFileKind.Sicses, null, new DateOnly(2027, 1, 31));
        l.Should().NotBeNull("un layout con campos sin cotejar es vigente (precedente D-43 de la PILA)");
        l!.Layout.TotalFields.Should().Be(3);
        l.Layout.VerifiedFields.Should().Be(1);
        l.Layout.UnverifiedFields.Should().Be(2);
        l.Layout.HasUnverifiedFields.Should().BeTrue();
    }

    [Theory]
    [InlineData("central-X-1-2027-01-01.json", """ "kind": "CreditBureau", """, "necesita «bureauCode»")]
    [InlineData("central-x-1-2027-01-01.json", """ "kind": "CreditBureau", "bureauCode": "x", """, "mayúsculas")]
    [InlineData("central-Y-1-2027-01-01.json", """ "kind": "CreditBureau", "bureauCode": "X", """, "debe llamarse central-X-")]
    [InlineData("otro-1-2027-01-01.json", """ "kind": "Sicses", """, "debe llamarse sicses-cartera-")]
    [InlineData("sicses-cartera-1-2027-01-01.json", """ "kind": "Sicses", "bureauCode": "X", """, "no lleva «bureauCode»")]
    [InlineData("sicses-cartera-1-2027-01-01.json", "", "no dice su «kind»")]
    public void La_clase_la_central_y_el_nombre_del_archivo_corresponden(string archivo, string clase, string motivo)
    {
        var json = Json("CODIGO-v1", clase, "2027-01-01", null, CampoSimple);
        var cargar = () => CatalogoDeLayoutsDeCartera.Cargar([(archivo, json)]);
        cargar.Should().Throw<FormatException>().WithMessage($"*{motivo}*");
    }

    [Fact]
    public void Dos_layouts_con_el_mismo_codigo_son_un_error()
    {
        var cargar = () => CatalogoDeLayoutsDeCartera.Cargar([
            ("central-X-1-2027-01-01.json", Central("X", "MISMO", "2027-01-01", null)),
            ("central-Y-1-2027-01-01.json", Central("Y", "MISMO", "2027-01-01", null)),
        ]);
        cargar.Should().Throw<FormatException>().WithMessage("*código de layout MISMO*");
    }

    [Fact]
    public async Task La_consulta_lista_cada_layout_con_su_estado_de_cotejo()
    {
        var handler = new ListRegulatoryLayoutsQueryHandler(CatalogoDeLayoutsDeCartera.Embebido);
        var r = await handler.Handle(new ListRegulatoryLayoutsQuery(), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        r.Value.Count(x => x.Kind is RegulatoryFileKind.Sicses or RegulatoryFileKind.CreditBureau).Should().Be(3);
        r.Value.Where(x => x.FormatCode is not null).Should().HaveCount(7, "los siete formatos de captaciones (feature 014, US17)");
        var sicses = r.Value.Single(x => x.Kind == RegulatoryFileKind.Sicses);
        sicses.BureauCode.Should().BeNull();
        sicses.FileKind.Should().Be("Delimited");
        sicses.VerifiedFields.Should().Be(0);
        sicses.UnverifiedFields.Should().Be(sicses.TotalFields);
        r.Value.Single(x => x.BureauCode == "DATACREDITO").FileKind.Should().Be("FixedWidth");
    }

    private const string CampoSimple = """{ "number": 1, "name": "A", "order": 1, "length": 5, "kind": "A", "format": "Text", "path": "X" }""";

    private static string Sicses(string codigo, string desde, string? hasta, string campos = CampoSimple) =>
        Json(codigo, """ "kind": "Sicses", """, desde, hasta, campos);

    private static string Central(string central, string codigo, string desde, string? hasta) =>
        Json(codigo, $$""" "kind": "CreditBureau", "bureauCode": "{{central}}", """, desde, hasta, CampoSimple);

    private static string Json(string codigo, string clase, string desde, string? hasta, string campos) => $$"""
        { "code": "{{codigo}}", {{clase}} "version": "1", "source": "prueba (por cotejar)", "validFrom": "{{desde}}", "validTo": {{(hasta is null ? "null" : $"\"{hasta}\"")}},
          "fileKind": "Delimited", "delimiter": "|", "records": [ { "code": "Detail", "fields": [ {{campos}} ] } ], "notes": ["prueba"] }
        """;
}
