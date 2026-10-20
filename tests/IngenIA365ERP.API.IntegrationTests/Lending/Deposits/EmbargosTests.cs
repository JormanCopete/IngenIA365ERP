using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.NationalValues;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T193 (US11, escenarios 1 a 3 de la spec y la prueba independiente; contracts/api.md §8): los embargos por HTTP en una cooperativa aislada
/// con captaciones activadas y el límite inembargable de prueba (50.000.000) registrado en la central y replicado. Ana tiene dos cuentas de ahorro que
/// suman 70.000.000:
/// <list type="number">
/// <item>Prueba independiente: un embargo judicial de 15.000.000 retiene sólo del exceso sobre el límite, en el orden (la cuenta más antigua).</item>
/// <item>US11-1: por alimentos el límite no aplica (retiene 60.000.000 de las dos cuentas).</item>
/// <item>US11-2: el oficio sólo sobre la cuenta de aportes nace rechazado con la respuesta «aportes inembargables» registrada.</item>
/// <item>US11-3: quien registró no levanta (<c>Lending.Segregation.Conflict</c>); el jefe de captaciones sí, y lo retenido se libera con la historia.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class EmbargosTests(CentralIdentityApiFixture fx)
{
    private const string Embargos = "/api/lending/garnishments";
    private const string Lineas = "/api/lending/lines";
    private const decimal Limite = 50_000_000m;

    private sealed record Escenario(CaptacionesE2E.CooperativaDeCaptaciones Coop, UsuariosDeCartera Usuarios, DateOnly Hoy, Guid Ana, Guid Vieja, Guid Nueva,
        Guid Aportes, Guid Juzgado);

    private async Task<Escenario> ArmarAsync(string nombre)
    {
        using var http = fx.CreateClient();
        var maestro = await ValoresNacionalesE2E.TokenMaestroAsync(fx, http);
        var hoy = CarteraE2E.Hoy(fx);
        await LineasE2E.RegistrarAsync(http, maestro, new
        {
            code = "LIMITE_INEMBARGABLE", validFrom = new DateOnly(hoy.Year, 1, 1), value = Limite, unit = "Pesos",
            source = "Carta circular de la Superintendencia Financiera, valor de prueba (e2e)",
        });
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, $"embargo_{nombre}");
        await ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var admin = coop.TokenAdmin;

        var linea1 = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, $"AHE1{nombre[..Math.Min(3, nombre.Length)].ToUpperInvariant()}");
        var linea2 = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, $"AHE2{nombre[..Math.Min(3, nombre.Length)].ToUpperInvariant()}");
        var ana = await MovimientosTests.AsociadoAsync(http, admin, $"Embargada{nombre}");
        var vieja = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea1, ana);
        var nueva = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea2, ana);
        await MovimientosTests.ConsignarAsync(http, admin, vieja, ana, 40_000_000m, hoy);
        await MovimientosTests.ConsignarAsync(http, admin, nueva, ana, 30_000_000m, hoy);

        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/contribution", new
        {
            code = $"APE{nombre[..Math.Min(3, nombre.Length)].ToUpperInvariant()}", name = "Aportes sociales", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Estatutos (e2e)", minimumNonReducible = new { kind = "FixedAmount", amount = 0m },
                installment = new { kind = "FixedAmount", amount = 50_000m, periodicity = "Monthly", dueDay = 5 }, allowsExtraordinary = true, refundTermDays = 60,
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var lineaDeAportes = (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
        var aportes = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, lineaDeAportes, ana);

        var juzgado = await CarteraE2E.PersonaAsync(http, admin, "Juzgado", $"Civil{nombre}");
        return new Escenario(coop, usuarios, hoy, ana, vieja, nueva, aportes, juzgado);
    }

    private static object Oficio(Escenario e, decimal? valor, bool alimentos = false, string alcance = "AllSavings", Guid[]? cuentas = null) => new
    {
        personPublicId = e.Ana, authority = "Court", courtPersonPublicId = e.Juzgado, processNumber = "11001400300120260045600", officeReference = "OF-118",
        officeDate = e.Hoy, claimantName = "Banco Ejemplo S.A.", amount = valor, isAlimony = alimentos, accountsScope = alcance, accountPublicIds = cuentas,
        supportAttachmentPublicId = Guid.NewGuid(),
    };

    private static async Task<System.Text.Json.JsonElement> RegistrarAsync(HttpClient http, Escenario e, object oficio)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, Embargos, oficio);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"registrar el embargo: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    private static decimal RetenidoEn(System.Text.Json.JsonElement embargo, Guid cuenta) =>
        embargo.GetProperty("retained").EnumerateArray().Where(r => r.GetProperty("accountPublicId").GetGuid() == cuenta)
            .Sum(r => r.GetProperty("retained").GetDecimal());

    [Fact]
    public async Task US11_dos_cuentas_sobre_el_limite_retienen_solo_el_exceso_y_el_desembargo_lo_libera()
    {
        var e = await ArmarAsync("exceso");
        using var http = fx.CreateClient();

        var embargo = await RegistrarAsync(http, e, Oficio(e, 15_000_000m));
        embargo.GetProperty("retainedTotal").GetDecimal().Should().Be(15_000_000m);
        embargo.GetProperty("unembargableLimit").GetDecimal().Should().Be(Limite);
        RetenidoEn(embargo, e.Vieja).Should().Be(15_000_000m, "del exceso de 20.000.000, en la cuenta más antigua");
        RetenidoEn(embargo, e.Nueva).Should().Be(0m);
        var id = embargo.GetProperty("publicId").GetGuid();

        // US11-3: quien registró no levanta.
        var mismo = await CarteraE2E.ConClaveAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, $"{Embargos}/{id}/release",
            new { releaseOfficeReference = "OF-200", reason = "Desembargo ordenado por el juzgado" });
        mismo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(mismo)).Should().Be("Lending.Segregation.Conflict");

        var jefe = e.Usuarios.RolesSugeridos["cartera.jefe-captaciones"];
        var rol = await CarteraE2E.EnviarAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, $"/api/admin/users/{e.Usuarios.Analista.PublicId}/roles",
            new { rolePublicId = jefe });
        if (!rol.IsSuccessStatusCode) (await CarteraE2E.CodigoDeErrorAsync(rol)).Should().Be("Security.Users.AlreadyAssignedRole");
        var analista = await UsuariosDeCartera.IngresarAsync(http, e.Usuarios.Analista);
        var levantado = await CarteraE2E.ConClaveAsync(http, analista, HttpMethod.Post, $"{Embargos}/{id}/release",
            new { releaseOfficeReference = "OF-200", reason = "Desembargo ordenado por el juzgado" });
        levantado.StatusCode.Should().Be(HttpStatusCode.OK, await levantado.Content.ReadAsStringAsync());
        var despues = await CarteraE2E.LeerAsync(levantado);
        despues.GetProperty("status").GetInt32().Should().Be(3, "Released");
        despues.GetProperty("retainedTotal").GetDecimal().Should().Be(0m);

        var historia = await CarteraE2E.EnviarAsync(http, e.Coop.TokenAdmin, HttpMethod.Get, $"/api/lending/accounts/{e.Vieja}/holds?includeReleased=true", null);
        historia.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CarteraE2E.LeerAsync(historia)).EnumerateArray().Should().ContainSingle(h => h.GetProperty("releasedAt").ValueKind != System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task US11_1_por_alimentos_el_limite_inembargable_no_aplica()
    {
        var e = await ArmarAsync("alimentos");
        using var http = fx.CreateClient();

        var embargo = await RegistrarAsync(http, e, Oficio(e, 60_000_000m, alimentos: true));

        embargo.GetProperty("retainedTotal").GetDecimal().Should().Be(60_000_000m);
        RetenidoEn(embargo, e.Vieja).Should().Be(40_000_000m);
        RetenidoEn(embargo, e.Nueva).Should().Be(20_000_000m);
    }

    [Fact]
    public async Task US11_2_el_oficio_sobre_aportes_se_rechaza_con_su_respuesta_al_juzgado()
    {
        var e = await ArmarAsync("aportes");
        using var http = fx.CreateClient();

        var embargo = await RegistrarAsync(http, e, Oficio(e, 5_000_000m, alcance: "Specific", cuentas: [e.Aportes]));

        embargo.GetProperty("status").GetInt32().Should().Be(4, "Rejected");
        embargo.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).Should()
            .Contain(w => w!.StartsWith("Lending.Garnishment.ContributionsNotGarnishable", StringComparison.Ordinal));
        embargo.GetProperty("responses").EnumerateArray().Should().ContainSingle().Which.GetProperty("kind").GetInt32().Should().Be(3);
        embargo.GetProperty("retainedTotal").GetDecimal().Should().Be(0m);
    }
}
