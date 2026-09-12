using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// La semilla refleja la ley vigente por vigencias, no por valor único: Ley 2101 de 2021
/// (42 h desde el 15/07/2026 → 210 h/mes, 220 antes) y Ley 2466 de 2025 (recargo dominical
/// 90 % desde el 01/07/2026, 80 % antes). Las revisiones se aplican a cooperativas nuevas y
/// existentes por igual (la de esta colección se creó antes de que existieran en el código,
/// así que la semilla las inserta sobre una base ya sembrada) y cierran la versión anterior.
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
        horas.Should().HaveCount(2);
        horas[0].Desde.Should().Be(new DateTime(2026, 1, 1)); horas[0].Hasta.Should().Be(new DateTime(2026, 7, 14)); horas[0].Valor.Should().Be(220m);
        horas[1].Desde.Should().Be(new DateTime(2026, 7, 15)); horas[1].Hasta.Should().BeNull(); horas[1].Valor.Should().Be(210m);

        var recargo = (await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions/RECARGO_DOMINICAL/versions")).EnumerateArray()
            .Select(v => (Desde: v.GetProperty("validFrom").GetDateTime().Date, Factor: v.GetProperty("unitFactor").GetDecimal()))
            .OrderBy(v => v.Desde).ToList();
        recargo.Should().HaveCount(2);
        recargo[0].Desde.Should().Be(new DateTime(2026, 1, 1)); recargo[0].Factor.Should().Be(0.80m);
        recargo[1].Desde.Should().Be(new DateTime(2026, 7, 1)); recargo[1].Factor.Should().Be(0.90m);

        var extraDom = (await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions/HEX_DOM_NOCTURNA/versions")).EnumerateArray()
            .Select(v => v.GetProperty("unitFactor").GetDecimal()).OrderBy(x => x).ToList();
        extraDom.Should().Equal(2.55m, 2.65m);
    }
}
