using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T125 (US4, escenarios 1 a 6 de la spec; quickstart §2; contracts/api.md §4, §18.4 y contrato-de-cuentas §3.7–§3.11): los movimientos de
/// una cuenta de ahorro a la vista por HTTP, en una cooperativa aislada con aportes y captaciones activados
/// (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>), con el recaudador de prueba sobre el contrato de cuentas y la pantalla de la Cartera:
/// <list type="number">
/// <item>Escenario 1: en una cuenta conjunta de manejo conjunto, la simulación con la firma de un solo titular se rechaza con las firmas que faltan.</item>
/// <item>Escenario 2: un retiro mayor que el disponible se rechaza mostrando saldo, bloqueos, embargos y disponible.</item>
/// <item>Escenario 3 y prueba independiente: consignar 1.000.000 y retirar 400.000 de una cuenta no marcada cobra 1.600 de GMF enlazado; de la marcada,
/// cero hasta el tope.</item>
/// <item>Escenario 4: repetir la consignación con la misma clave devuelve el comprobante original.</item>
/// <item>Escenario 5: el retiro por orden de giro sobre la atribución queda en espera de un aprobador (202) sin mover saldo.</item>
/// <item>Escenario 6: reversar desde la pantalla una consignación ya usada se rechaza con el retiro que lo impide.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class MovimientosTests(CentralIdentityApiFixture fx)
{
    private const string Cuentas = "/api/lending/accounts";
    private const string Lineas = "/api/lending/lines";
    private const string Doble = "/api/lending/test-doubles/collector/accounts";

    private static int _documento;

    private sealed record Escenario(CaptacionesE2E.CooperativaDeCaptaciones Coop, UsuariosDeCartera Usuarios, Guid Oficina, Guid LineaALaVista, DateOnly Hoy)
    {
        public string Admin => Coop.TokenAdmin;
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();

    private Task<Escenario> EscenarioAsync(string nombre)
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = ArmarAsync(nombre);
                Escenarios[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    private async Task<Escenario> ArmarAsync(string nombre)
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, nombre);
        using var http = fx.CreateClient();
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var linea = await LineaALaVistaAsync(http, coop.TokenAdmin, hoy, "AHVISTA");
        return new Escenario(coop, usuarios, usuarios.O1.AgenciaPublicId, linea, hoy);
    }

    internal static async Task<Guid> LineaALaVistaAsync(HttpClient http, string admin, DateOnly hoy, string codigo, object[]? cargos = null)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = codigo, name = $"Ahorro {codigo}", depositClass = "Demand", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Tasa aprobada por el consejo (e2e)", allowsPartialWithdrawals = true, allowsCoHolders = true,
                rate = new { kind = "Fixed", effectiveAnnual = 0.01m }, balanceMethod = "DailyBalance",
                minimums = new { toOpen = 0m, toMaintain = 0m, toEarnInterest = 0m }, dayCount = "Actual365", rateEquivalence = "Compound",
                rounding = new { unit = 1m, mode = "AwayFromZero", moment = "PerTotal" }, settlementPeriodicity = "Monthly", tax = new { gmfApplies = true },
                charges = cargos ?? [],
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    internal static async Task<Guid> AsociadoAsync(HttpClient http, string admin, string nombre)
    {
        var documento = (6_800_000_000L + Interlocked.Increment(ref _documento) * 10L).ToString(CultureInfo.InvariantCulture);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new
            {
                idType = "C", taxId = documento, firstName = nombre, lastName = "Movimientos", personType = "01", dateOfBirth = new DateOnly(1984, 5, 20),
                email = $"{nombre.ToLowerInvariant()}.{documento}@movimientos.test",
            },
            associate = new { joinDate = new DateOnly(2023, 2, 1), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var persona = (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "GeneralTreatment");
        return persona;
    }

    internal static async Task<Guid> CuentaAsync(HttpClient http, string admin, Guid oficina, Guid linea, Guid titular, bool exenta = false,
        string manejo = "Indistinct", object[]? partes = null)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Cuentas, new
        {
            linePublicId = linea, holderPersonPublicId = titular, officePublicId = oficina, handlingMode = manejo, parties = partes ?? [],
            beneficiaries = Array.Empty<object>(), markGmfExempt = exenta,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la cuenta: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    internal static object Recibo(DateOnly fecha) =>
        new { number = "REC-E2E", date = fecha, officePublicId = (Guid?)null, meansReference = (string?)null, documentKind = "RC" };

    internal static async Task<JsonElement> ConsignarAsync(HttpClient http, string admin, Guid cuenta, Guid depositante, decimal valor, DateOnly fecha,
        string? clave = null, string medio = "Cash")
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Doble}/deposits", new
        {
            deposits = new[]
            {
                new
                {
                    idempotencyKey = clave ?? $"TST:{Guid.NewGuid():N}", accountPublicId = cuenta, modality = "Free", amount = valor, valueDate = fecha,
                    meansClass = medio, depositorPublicId = depositante, receipt = Recibo(fecha),
                },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("deposits")[0];
    }

    internal static Task<HttpResponseMessage> RetirarAsync(HttpClient http, string admin, Guid cuenta, Guid titular, decimal valor, DateOnly fecha,
        IReadOnlyList<Guid>? firmas = null, string? clave = null) =>
        CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Doble}/withdrawals", new
        {
            idempotencyKey = clave ?? $"TST:{Guid.NewGuid():N}", accountPublicId = cuenta, amount = valor, valueDate = fecha, meansClass = "Cash",
            signerPersonPublicIds = firmas ?? [titular], receiverPersonPublicId = titular, receipt = Recibo(fecha),
        });

    private static async Task<JsonElement> SimularAsync(HttpClient http, string admin, Guid cuenta, decimal valor, DateOnly fecha, IReadOnlyList<Guid> firmas)
    {
        var resp = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Doble}/simulations", new
        {
            operation = "Withdrawal", accountPublicId = cuenta, amount = valor, valueDate = fecha, meansClass = "Cash", signerPersonPublicIds = firmas,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("value");
    }

    // ---------------------------------------------------------------------------------------------------- escenarios --

    [Fact]
    public async Task US4_1_en_manejo_conjunto_la_firma_de_un_solo_titular_no_alcanza()
    {
        var esc = await EscenarioAsync("movimientos");
        using var http = fx.CreateClient();
        var ana = await AsociadoAsync(http, esc.Admin, "Ana");
        var beto = await AsociadoAsync(http, esc.Admin, "Beto");
        var cuenta = await CuentaAsync(http, esc.Admin, esc.Oficina, esc.LineaALaVista, ana, manejo: "Joint",
            partes: [new { personPublicId = beto, role = "CoHolder", validFrom = esc.Hoy, signatureLimit = (decimal?)null }]);
        await ConsignarAsync(http, esc.Admin, cuenta, ana, 500_000m, esc.Hoy);

        var simulada = await SimularAsync(http, esc.Admin, cuenta, 100_000m, esc.Hoy, [ana]);

        simulada.GetProperty("allowed").GetBoolean().Should().BeFalse();
        simulada.GetProperty("missingSignatures").EnumerateArray().Select(f => f.GetGuid()).Should().Contain(beto);
        var retiro = await RetirarAsync(http, esc.Admin, cuenta, ana, 100_000m, esc.Hoy, [ana]);
        (await CarteraE2E.LeerAsync(retiro)).GetProperty("code").GetString().Should().Be("Lending.Savings.SignaturesMissing");
    }

    [Fact]
    public async Task US4_2_un_retiro_mayor_que_el_disponible_se_rechaza_con_el_desglose()
    {
        var esc = await EscenarioAsync("movimientos");
        using var http = fx.CreateClient();
        var carla = await AsociadoAsync(http, esc.Admin, "Carla");
        var cuenta = await CuentaAsync(http, esc.Admin, esc.Oficina, esc.LineaALaVista, carla);
        await ConsignarAsync(http, esc.Admin, cuenta, carla, 200_000m, esc.Hoy);

        var resp = await RetirarAsync(http, esc.Admin, cuenta, carla, 250_000m, esc.Hoy);

        var cuerpo = await CarteraE2E.LeerAsync(resp);
        cuerpo.GetProperty("code").GetString().Should().Be("Lending.Savings.InsufficientAvailable");
        cuerpo.GetProperty("message").GetString().Should().Contain("saldo 200,000.00").And.Contain("bloqueado").And.Contain("embargado")
            .And.Contain("disponible");
    }

    [Fact]
    public async Task US4_3_retirar_400000_de_una_cuenta_no_marcada_cobra_1600_de_GMF_y_de_la_marcada_cero()
    {
        var esc = await EscenarioAsync("movimientos");
        using var http = fx.CreateClient();
        var dario = await AsociadoAsync(http, esc.Admin, "Dario");
        var noMarcada = await CuentaAsync(http, esc.Admin, esc.Oficina, esc.LineaALaVista, dario);
        var elena = await AsociadoAsync(http, esc.Admin, "Elena");
        var marcada = await CuentaAsync(http, esc.Admin, esc.Oficina, esc.LineaALaVista, elena, exenta: true);
        await ConsignarAsync(http, esc.Admin, noMarcada, dario, 1_000_000m, esc.Hoy);
        await ConsignarAsync(http, esc.Admin, marcada, elena, 1_000_000m, esc.Hoy);

        var gravado = await RetirarAsync(http, esc.Admin, noMarcada, dario, 400_000m, esc.Hoy);
        var exento = await RetirarAsync(http, esc.Admin, marcada, elena, 400_000m, esc.Hoy);

        gravado.StatusCode.Should().Be(HttpStatusCode.OK, await gravado.Content.ReadAsStringAsync());
        var operacion = (await CarteraE2E.LeerAsync(gravado)).GetProperty("value").GetProperty("operationPublicId").GetGuid();
        var comprobante = await CarteraE2E.GetAsync(http, esc.Admin, $"{Cuentas}/{noMarcada}/operations/{operacion}");
        comprobante.GetProperty("gmf").GetProperty("amount").GetDecimal().Should().Be(1_600m);
        comprobante.GetProperty("transactions").EnumerateArray().Count(t => t.GetProperty("kind").GetInt32() == (int)MemberAccountTransactionKind.GmfCharged).Should().Be(2);
        exento.StatusCode.Should().Be(HttpStatusCode.OK, await exento.Content.ReadAsStringAsync());

        var uso = await CarteraE2E.GetAsync(http, esc.Admin, $"/api/lending/gmf-exemptions/usage?personPublicId={elena}");
        uso.GetProperty("usedExempt").GetDecimal().Should().Be(400_000m);
        uso.GetProperty("gmfCharged").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task US4_4_repetir_la_consignacion_con_la_misma_clave_devuelve_el_comprobante_original()
    {
        var esc = await EscenarioAsync("movimientos");
        using var http = fx.CreateClient();
        var fabio = await AsociadoAsync(http, esc.Admin, "Fabio");
        var cuenta = await CuentaAsync(http, esc.Admin, esc.Oficina, esc.LineaALaVista, fabio);
        var clave = $"TST:{Guid.NewGuid():N}";

        var primera = await ConsignarAsync(http, esc.Admin, cuenta, fabio, 300_000m, esc.Hoy, clave);
        var segunda = await ConsignarAsync(http, esc.Admin, cuenta, fabio, 300_000m, esc.Hoy, clave);

        segunda.GetProperty("value").GetProperty("operationPublicId").GetGuid()
            .Should().Be(primera.GetProperty("value").GetProperty("operationPublicId").GetGuid());
        var operaciones = await CarteraE2E.GetAsync(http, esc.Admin, $"{Cuentas}/{cuenta}/operations");
        operaciones.GetProperty("items").GetArrayLength().Should().Be(1, "no se consigna dos veces");
    }

    [Fact]
    public async Task US4_5_el_retiro_por_giro_sobre_la_atribucion_queda_en_espera_sin_mover_saldo()
    {
        var esc = await EscenarioAsync("movimientos_atribucion");
        using var http = fx.CreateClient();
        await CarteraE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/lending/amount-limits", new
        {
            rolePublicId = esc.Usuarios.RolesSugeridos["cartera.administrador"], permissionCode = "Lending.Accounts.Withdraw", maxAmount = 100_000m,
            validFrom = esc.Hoy, reason = "Atribución de retiro del ensayo",
        });
        var gina = await AsociadoAsync(http, esc.Admin, "Gina");
        var cuenta = await CuentaAsync(http, esc.Admin, esc.Oficina, esc.LineaALaVista, gina);
        await ConsignarAsync(http, esc.Admin, cuenta, gina, 1_000_000m, esc.Hoy);
        var cajero = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);

        var resp = await CarteraE2E.ConClaveAsync(http, cajero, HttpMethod.Post, $"{Cuentas}/{cuenta}/operations/withdrawals", new
        {
            amount = 300_000m, payee = new { personPublicId = gina }, signatures = new[] { gina }, reason = "Retiro por giro (e2e)",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Accepted, await resp.Content.ReadAsStringAsync());
        var cuerpo = await CarteraE2E.LeerAsync(resp);
        cuerpo.GetProperty("status").GetString().Should().Be("PendingApproval");
        cuerpo.GetProperty("approvalPublicId").GetGuid().Should().NotBeEmpty();
        var detalle = await CarteraE2E.GetAsync(http, esc.Admin, $"{Cuentas}/{cuenta}");
        detalle.GetProperty("balances").GetProperty("balance").GetDecimal().Should().Be(1_000_000m, "la espera no mueve saldo");
    }

    [Fact]
    public async Task US4_6_reversar_desde_la_pantalla_una_consignacion_ya_usada_se_rechaza_con_el_retiro_que_lo_impide()
    {
        var esc = await EscenarioAsync("movimientos");
        using var http = fx.CreateClient();
        var hugo = await AsociadoAsync(http, esc.Admin, "Hugo");
        var cuenta = await CuentaAsync(http, esc.Admin, esc.Oficina, esc.LineaALaVista, hugo);
        var consignada = await ConsignarAsync(http, esc.Admin, cuenta, hugo, 500_000m, esc.Hoy);
        var retiro = await RetirarAsync(http, esc.Admin, cuenta, hugo, 400_000m, esc.Hoy);
        retiro.StatusCode.Should().Be(HttpStatusCode.OK, await retiro.Content.ReadAsStringAsync());

        var resp = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post,
            $"{Cuentas}/{cuenta}/operations/{consignada.GetProperty("value").GetProperty("operationPublicId").GetGuid()}/reverse",
            new { reason = "Error de digitación (e2e)" });

        var cuerpo = await CarteraE2E.LeerAsync(resp);
        cuerpo.GetProperty("code").GetString().Should().Be("Lending.Savings.ReversalWouldOverdraw");
        cuerpo.GetProperty("data").GetProperty("blockingOperation").GetProperty("number").GetString().Should().NotBeNullOrEmpty();
    }
}
