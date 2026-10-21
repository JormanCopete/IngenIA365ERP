using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0985 (feature 013, K5, US15; contracts/api.md §17): el cliente del tablero arma su query con la oficina, la línea y el corte y lee las fichas
/// con su enlace; el registro de vistas trae la excepción de datos del deudor de la concentración, y la pantalla de informes ofrece exportar la
/// concentración por línea a quien no exporta datos personales. (nuevo)
/// </summary>
public class TableroDeCarteraTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void La_query_del_tablero_lleva_oficina_linea_y_corte()
    {
        var oficina = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var linea = Guid.Parse("66666666-7777-8888-9999-000000000000");

        TableroDeCarteraClient.Query(null, null, null).Should().BeEmpty();
        TableroDeCarteraClient.Query(oficina, linea, new DateOnly(2026, 12, 1))
            .Should().Be($"branch={oficina}&line={linea}&asOf=2026-12-01");
    }

    [Fact]
    public void Lee_las_fichas_con_su_enlace_y_su_nota()
    {
        const string json = """
            { "asOf": "2026-12-01", "scope": { "branches": [ { "publicId": "11111111-2222-3333-4444-555555555555", "code": "", "name": "Norte" } ], "lines": [] },
              "tiles": [ { "key": "calidad", "label": "Cartera vencida sobre bruta", "value": 12.5, "previousValue": 10, "unit": "Percent",
                           "severity": null, "link": { "page": "/cartera/informes", "query": { "vista": "quality", "asOf": "2026-12-01" } },
                           "note": "Vencida: más de 30 días de mora." } ] }
            """;

        var t = JsonSerializer.Deserialize<TableroDeCarteraDto>(json, Web)!;

        t.Scope.Branches.Should().ContainSingle().Which.Name.Should().Be("Norte");
        var ficha = t.Tiles.Should().ContainSingle().Subject;
        ficha.Value.Should().Be(12.5m);
        ficha.Note.Should().Contain("30 días");
        ficha.Link.Ruta.Should().Be("/cartera/informes?vista=quality&asOf=2026-12-01");
    }

    [Fact]
    public void La_concentracion_por_linea_no_trae_datos_del_deudor()
    {
        const string json = """
            { "key": "concentration", "name": "Concentración", "description": "", "fileName": "concentracion", "filters": [], "ownFilters": ["by"],
              "personalData": true, "availableFrom": 5, "requiredPermission": "Lending.Loans.Read",
              "personalDataExceptWhen": { "filter": "by", "values": ["line", "sector", "payer"] } }
            """;

        var vista = JsonSerializer.Deserialize<VistaDeInformeDeCarteraDto>(json, Web)!;

        vista.TraeDatosDelDeudor(new Dictionary<string, string>()).Should().BeTrue("por defecto es por deudor");
        vista.TraeDatosDelDeudor(new Dictionary<string, string> { ["by"] = "group" }).Should().BeTrue();
        vista.TraeDatosDelDeudor(new Dictionary<string, string> { ["by"] = "Line" }).Should().BeFalse();
        (vista with { PersonalDataExceptWhen = null }).TraeDatosDelDeudor(new Dictionary<string, string> { ["by"] = "line" }).Should().BeTrue();
    }

    [Fact]
    public void La_pantalla_del_tablero_carga_por_zona_y_el_menu_la_enlaza_con_su_permiso()
    {
        var raiz = Helpers.Repositorio.Raiz();
        var pantalla = File.ReadAllText(Path.Combine(raiz, "src", "Presentation", "IngenIA365ERP.Shared", "Pages", "Cartera", "Tablero.razor"));
        pantalla.Should().Contain("@page \"/cartera/tablero\"").And.Contain("<IndicadorDeCarga").And.Contain("kpi-card")
            .And.Contain("Required=\"Lending.Dashboard.View\"");
        var menu = File.ReadAllText(Path.Combine(raiz, "src", "Presentation", "IngenIA365ERP.Shared", "Layout", "NavMenu.razor"));
        System.Text.RegularExpressions.Regex.IsMatch(menu, @"<PermissionGate Required=""Lending\.Dashboard\.View"">\s*<NavLink href=""/cartera/tablero""")
            .Should().BeTrue("el enlace del menú va dentro de su PermissionGate");
    }
}
