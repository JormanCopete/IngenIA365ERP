using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T125 (US4; quickstart §2; contracts/api.md §4 y §9; FR-090, FR-128): los cargos y el débito automático por HTTP.
/// <list type="bullet">
/// <item>Un cargo periódico de 5.000 en la versión de la línea lo causa el ciclo diario el último día del mes, una sola vez, y se ve en la ficha.</item>
/// <item>Un cargo por evento de pantalla (certificado adicional) lo registra una persona y ella misma no lo condona (segregación).</item>
/// <item>Con una autorización de débito automático, el ciclo diario del vencimiento de la primera cuota debita la cuenta y paga el crédito con la clave
/// <c>CAR:debito-automatico:…</c>; correrlo otra vez no debita dos veces.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class CargosYDebitoAutomaticoTests(CentralIdentityApiFixture fx)
{
    private const string Cuentas = "/api/lending/accounts";

    private static readonly object[] CatalogoDeCargos =
    [
        new
        {
            code = "CUOTAMAN", name = "Cuota de manejo", kind = "MaintenanceFee", trigger = "Periodic", enabled = true, calculation = "FixedAmount",
            fixedAmount = 5_000m, periodicity = "Monthly", uncollectedPolicy = "KeepPending",
        },
        new
        {
            code = "CERTADIC", name = "Certificado adicional", kind = "ExtraDocument", trigger = "ExtraDocument", enabled = true, calculation = "FixedAmount",
            fixedAmount = 3_000m, uncollectedPolicy = "KeepPending",
        },
    ];

    [Fact]
    public async Task El_cargo_periodico_lo_causa_el_ciclo_el_ultimo_dia_del_mes_una_sola_vez()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "cargos");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHCARGO", CatalogoDeCargos);
        var catalogo = await CarteraE2E.GetAsync(http, admin, $"/api/lending/lines/{linea}/charges");
        catalogo.GetArrayLength().Should().Be(2);

        var ivan = await MovimientosTests.AsociadoAsync(http, admin, "Ivan");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, ivan);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, ivan, 100_000m, hoy);
        var fin = new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));
        // El ciclo de un día ya terminado se rechaza (Lending.ProcessRun.AlreadyCompleted): la repetición no existe, y el cargo sale una sola vez.
        using (CausacionE2E.RelojEn(fx, fin)) await CausacionE2E.CicloDeHoyAsync(fx, http, admin, coop.TenantPublicId);

        var cargos = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/charges");
        var items = cargos.GetProperty("items").EnumerateArray().ToList();
        items.Should().ContainSingle(c => c.GetProperty("code").GetString() == "CUOTAMAN");
        var cuota = items.Single(c => c.GetProperty("code").GetString() == "CUOTAMAN");
        cuota.GetProperty("amount").GetDecimal().Should().Be(5_000m);
        cuota.GetProperty("status").GetString().Should().Be("Collected");
    }

    [Fact]
    public async Task El_cargo_por_evento_de_pantalla_no_lo_condona_quien_lo_registro()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "cargos");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHEVENTO", CatalogoDeCargos);
        var juana = await MovimientosTests.AsociadoAsync(http, admin, "Juana");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, juana);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, juana, 50_000m, hoy);

        var alta = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/charges",
            new { chargeCode = "CERTADIC", eventReference = "CERT-77", reason = "Certificado bancario adicional pedido por la titular" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var cargo = (await CarteraE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();

        var condonar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/charges/{cargo}/waive",
            new { reason = "Cortesía comercial (e2e)" });
        (await CarteraE2E.CodigoDeErrorAsync(condonar)).Should().Be("Lending.Segregation.Conflict");

        var ajeno = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/charges",
            new { chargeCode = "NOEXISTE", reason = "Un cargo que la línea no tiene" });
        (await CarteraE2E.CodigoDeErrorAsync(ajeno)).Should().Be("Lending.Savings.ChargeNotInLine");
    }

    [Fact]
    public async Task El_debito_automatico_paga_la_cuota_del_credito_desde_la_cuenta_una_sola_vez()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_debito");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "debito");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId, "es la misma cooperativa aislada (cap_debito)");
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var (_, deudor, desembolso) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, des, "Debito", 2_000_000m);
        var credito = desembolso.GetProperty("loanPublicId").GetGuid();
        await ConFechaDeNacimientoAsync(http, admin, deudor);
        var (_, vence, total) = await PagosE2E.CuotaAsync(http, admin, credito);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, des.Hoy, "AHDEBITO");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, des.Usuarios.O1.AgenciaPublicId, linea, deudor);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, deudor, total + 500_000m, des.Hoy);

        var mandato = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/direct-debits", new
        {
            accountPublicId = cuenta, scope = "AllLoansOfHolder", allowsPartial = false, validFrom = des.Hoy,
            authorizationAttachmentPublicId = Guid.NewGuid(),
        });
        mandato.StatusCode.Should().Be(HttpStatusCode.Created, await mandato.Content.ReadAsStringAsync());

        using (CausacionE2E.RelojEn(fx, vence)) await CausacionE2E.CicloDeHoyAsync(fx, http, admin, des.Coop.TenantPublicId);
        using (CausacionE2E.RelojEn(fx, vence))
            (await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, CausacionE2E.Corridas, new { kind = "DailyCycle" }))
                .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "el ciclo del día ya terminó: no se corre otra vez");

        var debitos = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/operations?kind=DirectDebit");
        debitos.GetProperty("items").GetArrayLength().Should().Be(1, "correr el ciclo otra vez no debita dos veces");
        debitos.GetProperty("items")[0].GetProperty("amount").GetDecimal().Should().Be(total);
        var lista = await CarteraE2E.GetAsync(http, admin, $"/api/lending/direct-debits?accountPublicId={cuenta}");
        lista.EnumerateArray().Single().GetProperty("lastDebits").GetArrayLength().Should().BeGreaterThan(0);
    }

    /// <summary>
    /// La persona del crédito nace sin fecha de nacimiento y abrir una cuenta exige los datos de conocimiento
    /// (<c>Lending.Savings.MemberDataIncomplete</c>): se completan por la misma edición de la persona.
    /// </summary>
    private static async Task ConFechaDeNacimientoAsync(HttpClient http, string admin, Guid persona)
    {
        var actual = await CarteraE2E.GetAsync(http, admin, $"/api/core/people/{persona}");
        var cuerpo = actual.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
        cuerpo["dateOfBirth"] = new DateOnly(1984, 5, 20);
        var resp = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/core/people/{persona}", cuerpo);
        resp.IsSuccessStatusCode.Should().BeTrue($"completar la fecha de nacimiento: «{await resp.Content.ReadAsStringAsync()}»");
    }
}
