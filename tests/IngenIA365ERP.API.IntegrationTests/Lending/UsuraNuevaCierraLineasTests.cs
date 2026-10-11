using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.NationalValues;
using static IngenIA365ERP.API.IntegrationTests.Lending.LineasE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0344 (feature 013, US1-3 y US1-5; FR-021; quickstart §1.7, colección «Valores nacionales e2e» porque registra en la central, que es una
/// por host, §7.6), en el motor de <c>DB_PROVIDER</c>:
/// <list type="number">
/// <item>con la usura del mes registrada y replicada, una línea de microcrédito al 40 % EA se registra;</item>
/// <item>el maestro registra para el mes siguiente una usura menor (35 %), la réplica la trae y la línea queda bloqueada desde ese día
/// (<c>usuryBlockedSince</c>, no admite créditos nuevos) con la alerta <c>Cartera.LineaCerradaPorUsura</c>;</item>
/// <item>una versión nueva desde ese día bajo la usura limpia el bloqueo;</item>
/// <item>un día sin usura registrada (el mes que sigue, porque las dos usuras tienen fin) hace que crear una versión desde ese día responda
/// <c>NationalValues.Missing</c> y alerte <c>Cartera.ValorNacionalFaltante</c>, aunque el comando falle; US2 (T0385): simular un desembolso
/// ese día responde lo mismo y también alerta.</item>
/// </list>
/// Usa la usura de <b>microcrédito</b> y no la de consumo: la de consumo del mes la registra <c>ValoresNacionalesYReplicaTests</c> en este
/// mismo host y espera registrarla él (201), y <c>Los_faltantes_listan…</c> espera la de consumo del mes siguiente sin registrar. (nuevo)
/// </summary>
[Collection(ValoresNacionalesCollection.Nombre)]
public sealed class UsuraNuevaCierraLineasTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Una_usura_nueva_menor_bloquea_la_linea_una_version_bajo_ella_la_limpia_y_un_dia_sin_usura_alerta()
    {
        using var http = fx.CreateClient();
        var maestro = await ValoresNacionalesE2E.TokenMaestroAsync(fx, http);
        var coop = await ValoresNacionalesE2E.CooperativaAisladaAsync(fx, "usura_nueva");
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var esteMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var siguiente = esteMes.AddMonths(1);
        var sinUsura = siguiente.AddMonths(1);

        // (1) La usura de microcrédito del mes, con fin, y la línea al 40 % EA.
        await RegistrarAsync(http, maestro, new
        {
            code = "USURA", key = UsuraDeMicrocredito, validFrom = esteMes, validTo = siguiente.AddDays(-1), value = 0.55m, unit = "EA",
            source = "Superintendencia Financiera, certificación de prueba (e2e)",
        });
        await ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
        var linea = await CrearExitoAsync(http, admin, "MICRO", "Microcrédito empresarial",
            VersionFija(hoy, "MICROCREDITO", "Empresarial", 0.40m));
        (await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}")).GetProperty("usuryBlockedSince").ValueKind
            .Should().Be(System.Text.Json.JsonValueKind.Null);

        // (2) Una usura menor desde el mes siguiente: la réplica bloquea la línea desde ese día y alerta.
        var menor = await RegistrarAsync(http, maestro, new
        {
            code = "USURA", key = UsuraDeMicrocredito, validFrom = siguiente, validTo = sinUsura.AddDays(-1), value = 0.35m, unit = "EA",
            source = "Superintendencia Financiera, certificación de prueba del mes siguiente (e2e)",
        });
        menor.Should().NotBeNull();
        var replica = await ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
        replica.Valores.Should().BeGreaterThanOrEqualTo(1);

        var bloqueada = await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}");
        bloqueada.GetProperty("usuryBlockedSince").GetString().Should().Be(siguiente.ToString("yyyy-MM-dd"), "bloqueada desde el día de la usura nueva");
        bloqueada.GetProperty("admitsNewCredits").GetBoolean().Should().BeFalse();
        bloqueada.GetProperty("status").GetInt32().Should().Be(2, "el bloqueo no es un cierre: la línea sigue activa");
        (await AlertasAsync(coop, "Cartera.LineaCerradaPorUsura")).Should().Be(1, "una alerta por línea");
        var lista = (await CarteraE2E.GetAsync(http, admin, Lineas)).EnumerateArray().Single(l => l.GetProperty("code").GetString() == "MICRO");
        lista.GetProperty("usuryBlockedSince").GetString().Should().Be(siguiente.ToString("yyyy-MM-dd"), "la lista muestra la marca");

        // Repetir la réplica no vuelve a alertar.
        (await ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId)).Valores.Should().Be(0);
        (await AlertasAsync(coop, "Cartera.LineaCerradaPorUsura")).Should().Be(1);

        // (3) Una versión desde ese día bajo la usura nueva limpia el bloqueo.
        var bajo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/{linea}/versions",
            VersionFija(siguiente, "MICROCREDITO", "Empresarial", 0.30m, "Tasa bajo la usura del mes siguiente"));
        bajo.StatusCode.Should().Be(HttpStatusCode.Created, await bajo.Content.ReadAsStringAsync());
        var limpia = await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}");
        limpia.GetProperty("usuryBlockedSince").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null, "la versión bajo la usura limpia el bloqueo");
        limpia.GetProperty("admitsNewCredits").GetBoolean().Should().BeTrue();

        // (4) Un día sin usura registrada: la versión desde ese día no se registra y el faltante queda alertado aunque el comando falle.
        var faltantesAntes = await AlertasAsync(coop, "Cartera.ValorNacionalFaltante");
        var sinDato = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/{linea}/versions",
            VersionFija(sinUsura, "MICROCREDITO", "Empresarial", 0.30m, "Versión de un mes sin usura certificada"));
        sinDato.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await sinDato.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(sinDato)).Should().Be("NationalValues.Missing");
        (await AlertasAsync(coop, "Cartera.ValorNacionalFaltante")).Should().BeGreaterThan(faltantesAntes,
            "la alerta del faltante sobrevive al rollback del comando (US1-3)");
        (await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}")).GetProperty("versions").GetArrayLength().Should().Be(2, "no se guardó la versión");

        // (5) US2 (T0385): del lado de la originación, simular un desembolso en un día sin usura tampoco se puede y alerta a quien la registra
        // (otro día: la alerta es una por código, clave y fecha).
        var faltantesDeLaSimulacion = await AlertasAsync(coop, "Cartera.ValorNacionalFaltante");
        var simulada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/simulations/schedule", new
        {
            creditLinePublicId = linea, amount = 2_000_000m, installments = 12, periodicity = "Monthly", disbursementDate = sinUsura.AddDays(3),
        });
        simulada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await simulada.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(simulada)).Should().Be("NationalValues.Missing");
        (await AlertasAsync(coop, "Cartera.ValorNacionalFaltante")).Should().BeGreaterThan(faltantesDeLaSimulacion,
            "simular sin usura del día alerta a quien la registra (US1-3)");
    }

    /// <summary>Las alertas de ese tipo en la base de la cooperativa (cualquier estado).</summary>
    private async Task<int> AlertasAsync(CarteraE2E.CooperativaAislada coop, string tipo)
    {
        var inv = new InventarioE2E.CooperativaAislada { TokenAdmin = coop.TokenAdmin, TenantPublicId = coop.TenantPublicId, SucursalPrincipal = coop.SucursalPrincipal };
        return Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, inv,
            $"""SELECT COUNT(*) FROM dbo."COR_Alerts" WHERE "TypeCode" = '{tipo}'""",
            $"SELECT COUNT(*) FROM [dbo].[COR_Alerts] WHERE [TypeCode] = N'{tipo}'"));
    }
}
