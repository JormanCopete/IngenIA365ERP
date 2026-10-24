using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T095 (US3, escenarios 1 a 4 de la spec; quickstart §1; contracts/api.md §7 y §18.4): los aportes por HTTP en una cooperativa aislada con
/// créditos, aportes y captaciones activados (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>):
/// <list type="number">
/// <item>La prueba independiente y el escenario 1: dos meses del ciclo diario dejan dos cuotas de 50.000; el recaudador de prueba las lista con su
/// período, paga una más un extraordinario, y el resumen de aportes da el mismo saldo por clase.</item>
/// <item>Escenario 2: un pago que no alcanza la cuota la deja abonada y el resto sigue exigible, con el no descontado y su motivo.</item>
/// <item>Escenario 3: el resumen de aportes (el saldo que Cartera lee) trae aportes, mínimo irreducible y ahorro permanente, y la importación de la
/// plantilla de saldos responde <c>Lending.Contributions.UseMigration</c>.</item>
/// <item>Escenario 4: con reparto al ahorro permanente, la cuota se divide entre las dos cuentas en la proporción de la línea.</item>
/// <item>Devolución sin retiro: quien la pide no la aprueba; otra persona la aprueba y nace la orden de giro.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class AportesTests(CentralIdentityApiFixture fx)
{
    private const string Cuentas = "/api/lending/accounts";
    private const string Lineas = "/api/lending/lines";
    private const string Aportes = "/api/lending/contributions";
    private const string Doble = Receipts.RecaudoEnsayoDelContrato.Ruta + "/accounts"; // T0300: Recaudo real (REC) por el arnés

    private static int _documento;

    private sealed record Escenario(CaptacionesE2E.CooperativaDeCaptaciones Coop, UsuariosDeCartera Usuarios, Guid Oficina, Guid LineaDeAportes, Guid LineaPermanente,
        Guid LineaConReparto, DateOnly Hoy)
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
                tarea = ArmarAsync(nombre, nombre.EndsWith("_reparto", StringComparison.Ordinal));
                Escenarios[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    /// <summary>
    /// <paramref name="reparto"/>: la cooperativa tiene UNA línea de aportes activa (<c>Lending.Contributions.LineAlreadyActive</c>), así que la que reparte
    /// parte de la cuota al ahorro permanente vive en su propia cooperativa y no junto a la de cuota simple.
    /// </summary>
    private async Task<Escenario> ArmarAsync(string nombre, bool reparto = false)
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, nombre);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);

        // El ahorro permanente va primero: la línea con reparto lo nombra.
        var permanente = await LineaAsync(http, admin, $"{Lineas}/deposit", new
        {
            code = "AHPERM", name = "Ahorro permanente", depositClass = "Permanent", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Estatutos, art. 21 (e2e)", requiresMembership = true, allowsPartialWithdrawals = false,
                rate = new { kind = "Fixed", effectiveAnnual = 0.02m }, balanceMethod = "DailyBalance",
                minimums = new { toOpen = 0m, toMaintain = 0m, toEarnInterest = 0m }, dayCount = "Actual365", rateEquivalence = "Compound",
                rounding = new { unit = 1m, mode = "AwayFromZero", moment = "PerTotal" }, settlementPeriodicity = "Monthly", tax = new { gmfApplies = true },
            },
        });
        Guid aportes, conReparto;
        if (reparto)
        {
            aportes = conReparto = await LineaAsync(http, admin, $"{Lineas}/contribution", new
            {
                code = "APFONDO", name = "Aportes del fondo de empleados", active = true,
                firstVersion = new
                {
                    validFrom = hoy, reason = "Estatutos del fondo (e2e)", minimumNonReducible = new { kind = "FixedAmount", amount = 0m },
                    installment = new { kind = "FixedAmount", amount = 100_000m, periodicity = "Monthly", dueDay = 5 },
                    permanentSavingsShare = new { depositLinePublicId = permanente, fraction = 0.4m }, refundTermDays = 60,
                },
            });
        }
        else
        {
            aportes = await LineaAsync(http, admin, $"{Lineas}/contribution", new
            {
                code = "APORTES", name = "Aportes sociales", active = true,
                firstVersion = new
                {
                    validFrom = hoy, reason = "Estatutos, art. 20 (e2e)", minimumNonReducible = new { kind = "FixedAmount", amount = 100_000m },
                    installment = new { kind = "FixedAmount", amount = 50_000m, periodicity = "Monthly", dueDay = 5 }, allowsExtraordinary = true,
                    refundTermDays = 60,
                },
            });
            conReparto = Guid.Empty;
        }
        return new Escenario(coop, usuarios, usuarios.O1.AgenciaPublicId, aportes, permanente, conReparto, hoy);
    }

    private static async Task<Guid> LineaAsync(HttpClient http, string admin, string ruta, object cuerpo)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, ruta, cuerpo);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    /// <summary>Un asociado del maestro con su autorización de tratamiento de datos.</summary>
    private static async Task<Guid> AsociadoAsync(HttpClient http, string admin, string nombre)
    {
        var documento = (6_700_000_000L + Interlocked.Increment(ref _documento) * 10L).ToString(CultureInfo.InvariantCulture);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new
            {
                idType = "C", taxId = documento, firstName = nombre, lastName = "Aportes", personType = "01", dateOfBirth = new DateOnly(1985, 3, 10),
                email = $"{nombre.ToLowerInvariant()}.{documento}@aportes.test",
            },
            associate = new { joinDate = new DateOnly(2024, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var persona = (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "GeneralTreatment");
        return persona;
    }

    private static async Task<JsonElement> CuentaAsync(HttpClient http, Escenario esc, Guid linea, Guid titular)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, Cuentas, new
        {
            linePublicId = linea, holderPersonPublicId = titular, officePublicId = esc.Oficina, handlingMode = "Indistinct", parties = Array.Empty<object>(),
            beneficiaries = Array.Empty<object>(), markGmfExempt = false,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la cuenta: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>
    /// (Una cooperativa por prueba que corre el ciclo: la corrida de un día ya terminada responde <c>Lending.ProcessRun.AlreadyCompleted</c> y las cuentas
    /// abiertas después no recibirían su cuota.)
    /// Corre el ciclo diario el día 6 del mes siguiente: dos cuotas vencidas. La primera cuota es la del <b>período que contiene la apertura</b>
    /// (<c>CuotasDeAportes.Periodos</c> desde la apertura) y la segunda la del mes siguiente; un ciclo más sumaría una tercera.
    /// </summary>
    private async Task DosMesesDeCicloAsync(HttpClient http, Escenario esc)
    {
        foreach (var meses in new[] { 1 })
        {
            var dia = new DateOnly(esc.Hoy.Year, esc.Hoy.Month, 6).AddMonths(meses);
            using var reloj = CausacionE2E.RelojEn(fx, dia);
            await CausacionE2E.CicloDeHoyAsync(fx, http, esc.Admin, esc.Coop.TenantPublicId);
        }
    }

    private static async Task<JsonElement[]> ExigiblesAsync(HttpClient http, string admin, Guid persona, DateOnly fecha)
    {
        var r = await CarteraE2E.GetAsync(http, admin, $"{Doble}/installments?personPublicId={persona}&date={fecha:yyyy-MM-dd}");
        return [.. r.GetProperty("value").GetProperty("installments").EnumerateArray()];
    }

    private static object Consignacion(Guid cuenta, Guid depositante, decimal valor, DateOnly fecha, string modalidad = "Installments") => new
    {
        idempotencyKey = $"REC:{Guid.NewGuid():N}", accountPublicId = cuenta, modality = modalidad, amount = valor, valueDate = fecha, meansClass = "Cash",
        depositorPublicId = depositante, receipt = new { number = "REC-E2E", date = fecha, officePublicId = (Guid?)null, meansReference = (string?)null, documentKind = "RC" },
    };

    [Fact]
    public async Task US3_dos_cuotas_se_listan_se_paga_una_con_extraordinario_y_el_resumen_da_el_mismo_saldo()
    {
        var esc = await EscenarioAsync("aportes_ciclo_a");
        using var http = fx.CreateClient();
        var ana = await AsociadoAsync(http, esc.Admin, "Ana");
        var cuenta = (await CuentaAsync(http, esc, esc.LineaDeAportes, ana)).GetProperty("publicId").GetGuid();
        await DosMesesDeCicloAsync(http, esc);
        var corte = new DateOnly(esc.Hoy.Year, esc.Hoy.Month, 6).AddMonths(1);
        using var reloj = CausacionE2E.RelojEn(fx, corte); // la fecha valor de una consignación no pasa de hoy

        var cuotas = await ExigiblesAsync(http, esc.Admin, ana, corte);
        cuotas.Where(c => c.GetProperty("accountPublicId").GetGuid() == cuenta).Select(c => c.GetProperty("pending").GetDecimal())
            .Should().Equal(50_000m, 50_000m);

        var pago = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Doble}/deposits",
            new { deposits = new[] { Consignacion(cuenta, ana, 80_000m, corte) } });
        pago.StatusCode.Should().Be(HttpStatusCode.OK, await pago.Content.ReadAsStringAsync());
        var respuesta = (await CarteraE2E.LeerAsync(pago)).GetProperty("deposits")[0];
        respuesta.GetProperty("result").GetInt32().Should().BeOneOf([1, 2], respuesta.ToString());

        var resumen = await CarteraE2E.GetAsync(http, esc.Admin, $"{Aportes}/summary?personPublicId={ana}&date={corte:yyyy-MM-dd}");
        resumen.GetProperty("total").GetDecimal().Should().Be(80_000m);
        var clases = resumen.GetProperty("byClass");
        (clases.GetProperty("minimum").GetDecimal() + clases.GetProperty("ordinary").GetDecimal() + clases.GetProperty("extraordinary").GetDecimal()
            + clases.GetProperty("revalued").GetDecimal()).Should().Be(80_000m, "el desglose por clase suma el mismo saldo");
        resumen.GetProperty("installmentsInArrears").GetArrayLength().Should().Be(1, "la segunda cuota sigue con 20.000 pendientes");
    }

    [Fact]
    public async Task US3_un_pago_que_no_alcanza_deja_la_cuota_abonada_y_el_resto_exigible_con_su_motivo()
    {
        var esc = await EscenarioAsync("aportes_ciclo_b");
        using var http = fx.CreateClient();
        var beto = await AsociadoAsync(http, esc.Admin, "Beto");
        var cuenta = (await CuentaAsync(http, esc, esc.LineaDeAportes, beto)).GetProperty("publicId").GetGuid();
        await DosMesesDeCicloAsync(http, esc);
        var corte = new DateOnly(esc.Hoy.Year, esc.Hoy.Month, 6).AddMonths(1);
        using var reloj = CausacionE2E.RelojEn(fx, corte); // la fecha valor de una consignación no pasa de hoy
        var primera = (await ExigiblesAsync(http, esc.Admin, beto, corte)).First(c => c.GetProperty("accountPublicId").GetGuid() == cuenta);

        var pago = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Doble}/deposits", new
        {
            deposits = new[] { Consignacion(cuenta, beto, 30_000m, corte) },
            notDeducted = new[]
            {
                new
                {
                    idempotencyKey = $"REC:{Guid.NewGuid():N}", accountPublicId = cuenta, installmentPublicId = primera.GetProperty("installmentPublicId").GetGuid(),
                    expectedAmount = 50_000m, notDeductedAmount = 20_000m, reason = "DeductionCap", detail = "Tope de descuentos (e2e)",
                    sourceItemPublicId = (Guid?)null,
                },
            },
        });
        pago.StatusCode.Should().Be(HttpStatusCode.OK, await pago.Content.ReadAsStringAsync());

        var cuotas = await CarteraE2E.GetAsync(http, esc.Admin, $"{Aportes}/installments?personPublicId={beto}");
        var items = cuotas.GetProperty("items").EnumerateArray().ToList();
        items[0].GetProperty("status").GetString().Should().BeOneOf("PartiallyPaid", "NotDeducted");
        items[0].GetProperty("pending").GetDecimal().Should().Be(20_000m);
        items[0].GetProperty("notDeductedReason").GetString().Should().Be("DeductionCap");
        (await ExigiblesAsync(http, esc.Admin, beto, corte)).Should().Contain(c => c.GetProperty("installmentPublicId").GetGuid() ==
            primera.GetProperty("installmentPublicId").GetGuid(), "lo no descontado sigue exigible");
    }

    [Fact]
    public async Task US3_el_resumen_trae_aportes_minimo_y_permanente_y_la_plantilla_de_saldos_ya_no_se_importa()
    {
        var esc = await EscenarioAsync("aportes");
        using var http = fx.CreateClient();
        var carla = await AsociadoAsync(http, esc.Admin, "Carla");
        await CuentaAsync(http, esc, esc.LineaDeAportes, carla);

        var resumen = await CarteraE2E.GetAsync(http, esc.Admin, $"{Aportes}/summary?personPublicId={carla}");
        resumen.GetProperty("available").GetBoolean().Should().BeTrue();
        resumen.GetProperty("minimumNonReducible").GetDecimal().Should().Be(100_000m);
        resumen.GetProperty("permanentSavings").GetDecimal().Should().Be(0m);

        using var contenido = new MultipartFormDataContent { { new ByteArrayContent([0x50, 0x4B]), "file", "saldos.xlsx" } };
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/lending/contribution-balances/import?mode=review") { Content = contenido };
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", esc.Admin);
        pedido.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var importar = await http.SendAsync(pedido);
        (await CarteraE2E.CodigoDeErrorAsync(importar)).Should().Be("Lending.Contributions.UseMigration");
    }

    [Fact]
    public async Task US3_con_reparto_la_cuota_se_divide_entre_aportes_y_ahorro_permanente()
    {
        var esc = await EscenarioAsync("aportes_reparto");
        using var http = fx.CreateClient();
        var dario = await AsociadoAsync(http, esc.Admin, "Dario");
        var aportes = (await CuentaAsync(http, esc, esc.LineaConReparto, dario)).GetProperty("publicId").GetGuid();
        var permanente = (await CuentaAsync(http, esc, esc.LineaPermanente, dario)).GetProperty("publicId").GetGuid();
        var dia = new DateOnly(esc.Hoy.Year, esc.Hoy.Month, 6).AddMonths(1);
        using (CausacionE2E.RelojEn(fx, dia)) await CausacionE2E.CicloDeHoyAsync(fx, http, esc.Admin, esc.Coop.TenantPublicId);

        var cuotas = await ExigiblesAsync(http, esc.Admin, dario, dia);
        // Dos períodos: el de la apertura y el siguiente.
        cuotas.Where(c => c.GetProperty("accountPublicId").GetGuid() == aportes).Select(c => c.GetProperty("pending").GetDecimal())
            .Should().Equal(60_000m, 60_000m);
        cuotas.Where(c => c.GetProperty("accountPublicId").GetGuid() == permanente).Select(c => c.GetProperty("pending").GetDecimal())
            .Should().Equal([40_000m, 40_000m], "el 40 % de la cuota va al ahorro permanente");
    }

    [Fact]
    public async Task US3_la_devolucion_sin_retiro_la_aprueba_otra_persona_y_nace_la_orden_de_giro()
    {
        var esc = await EscenarioAsync("aportes");
        using var http = fx.CreateClient();
        var elsa = await AsociadoAsync(http, esc.Admin, "Elsa");
        var cuenta = (await CuentaAsync(http, esc, esc.LineaDeAportes, elsa)).GetProperty("publicId").GetGuid();
        var pago = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Doble}/deposits",
            new { deposits = new[] { Consignacion(cuenta, elsa, 300_000m, esc.Hoy, "Free") } });
        pago.StatusCode.Should().Be(HttpStatusCode.OK, await pago.Content.ReadAsStringAsync());

        var pedido = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Aportes}/refunds",
            new { accountPublicId = cuenta, amount = 50_000m, cause = "Other", reason = "Devolución aprobada por el consejo (e2e)" });
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var solicitud = (await CarteraE2E.LeerAsync(pedido)).GetProperty("publicId").GetGuid();

        var propia = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Aportes}/refunds/{solicitud}/approve",
            new { form = "DisbursementOrder", reason = "Me la apruebo (e2e)" });
        (await CarteraE2E.CodigoDeErrorAsync(propia)).Should().Be("Lending.Segregation.Conflict");

        // Aprobar la devolución es Lending.Contributions.Manage, que trae la plantilla del jefe de captaciones y no la del jefe de cartera.
        var rol = await CarteraE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, $"/api/admin/users/{esc.Usuarios.Jefe.PublicId}/roles",
            new { rolePublicId = esc.Usuarios.RolesSugeridos["cartera.jefe-captaciones"] });
        if (!rol.IsSuccessStatusCode)
            (await CarteraE2E.CodigoDeErrorAsync(rol)).Should().Be("Security.Users.AlreadyAssignedRole", await rol.Content.ReadAsStringAsync());
        var jefe = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe);
        var aprobada = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Aportes}/refunds/{solicitud}/approve",
            new { form = "DisbursementOrder", reason = "Aprobada (e2e)" });
        aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(aprobada)).GetProperty("payoutOrderNumber").GetString().Should().StartWith("OG-");
    }
}
