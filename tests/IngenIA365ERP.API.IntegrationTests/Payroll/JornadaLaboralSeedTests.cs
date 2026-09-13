using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// La semilla base 2026 lleva ya la norma de julio de 2026 (decisión del 2026-09-13, plataforma
/// en pruebas): Ley 2101 de 2021 → 210 h/mes, Ley 2466 de 2025 → recargo dominical 90 % y extras
/// dominicales 2,15 / 2,65, en una sola vigencia abierta desde el 01/01/2026. El mecanismo de
/// revisiones de mitad de año sigue existiendo (vacío) para el próximo escalón de la ley.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class JornadaLaboralSeedTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Horas_del_mes_y_recargo_dominical_tienen_las_vigencias_de_la_ley()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var horas = (await NominaE2E.GetAsync(http, admin, "/api/payroll/legal-parameters/HORAS_MES/versions")).EnumerateArray()
            .Select(v => (Desde: v.GetProperty("validFrom").GetDateTime().Date, Hasta: v.TryGetProperty("validTo", out var h) && h.ValueKind != System.Text.Json.JsonValueKind.Null ? h.GetDateTime().Date : (DateTime?)null, Valor: v.GetProperty("value").GetDecimal()))
            .OrderBy(v => v.Desde).ToList();
        horas.Should().HaveCount(1);
        horas[0].Desde.Should().Be(new DateTime(2026, 1, 1)); horas[0].Hasta.Should().BeNull(); horas[0].Valor.Should().Be(210m);

        var recargo = (await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions/RECARGO_DOMINICAL/versions")).EnumerateArray()
            .Select(v => (Desde: v.GetProperty("validFrom").GetDateTime().Date, Factor: v.GetProperty("unitFactor").GetDecimal()))
            .OrderBy(v => v.Desde).ToList();
        recargo.Should().HaveCount(1);
        recargo[0].Desde.Should().Be(new DateTime(2026, 1, 1)); recargo[0].Factor.Should().Be(0.90m);

        var extraDom = (await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions/HEX_DOM_NOCTURNA/versions")).EnumerateArray()
            .Select(v => v.GetProperty("unitFactor").GetDecimal()).OrderBy(x => x).ToList();
        extraDom.Should().Equal(2.65m);
    }
}
