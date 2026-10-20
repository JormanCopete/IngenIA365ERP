using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T180 (US6, escenarios 1 a 6 de la spec; contracts/api.md §5 y §6): el CDAT por HTTP en una cooperativa aislada con captaciones activadas
/// (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>), con el ciclo diario corrido a mano (<see cref="CausacionE2E"/>) y el reloj de la suite
/// llevado a cada fecha:
/// <list type="number">
/// <item>Escenario 1: un plazo fuera del rango de la línea se rechaza con el rango vigente.</item>
/// <item>Escenario 2: con pago mensual de intereses a una cuenta de ahorro, en la fecha del primer pago se abona el interés neto de retención (79.741
/// menos el 4 %) y el plan lo muestra pagado.</item>
/// <item>Escenario 3: vencido sin instrucción, pasados los días de gracia se prorroga: versión 2 de condiciones, título con sufijo «-2».</item>
/// <item>Escenario 4: una línea sin redención anticipada la rechaza; la que la admite simula la penalidad antes de confirmar.</item>
/// <item>Escenario 5: un CDAT embargado no se paga al vencer: queda vencido, a disposición.</item>
/// <item>Escenario 6: 1,2 puntos negociados con una regla de 1,5 y sin la atribución de quien los pide quedan en espera del aprobador; aprobado, se sella;
/// 2 puntos se rechazan con el máximo de la regla.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class CdatTests(CentralIdentityApiFixture fx)
{
    private const string Cdat = "/api/lending/term-deposits";
    private const string Lineas = "/api/lending/lines";
    private const string Doble = "/api/lending/test-doubles/collector/accounts";

    private sealed record Escenario(CaptacionesE2E.CooperativaDeCaptaciones Coop, UsuariosDeCartera Usuarios, Guid Oficina, Guid Ahorro, Guid Titular);

    private async Task<Escenario> ArmarAsync(string nombre)
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, $"cdat_{nombre}");
        using var http = fx.CreateClient();
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var hoy = CarteraE2E.Hoy(fx);
        var ahorro = await MovimientosTests.LineaALaVistaAsync(http, coop.TokenAdmin, hoy, $"AHC{nombre[..Math.Min(4, nombre.Length)].ToUpperInvariant()}");
        var titular = await MovimientosTests.AsociadoAsync(http, coop.TokenAdmin, $"Cdat{nombre}");
        var cuenta = await MovimientosTests.CuentaAsync(http, coop.TokenAdmin, usuarios.O1.AgenciaPublicId, ahorro, titular);
        await MovimientosTests.ConsignarAsync(http, coop.TokenAdmin, cuenta, titular, 300_000_000m, hoy, medio: "Transfer");
        return new Escenario(coop, usuarios, usuarios.O1.AgenciaPublicId, cuenta, titular);
    }

    /// <summary>Una línea de CDAT al 10 % EA en base 360, de 30 a 720 días, al vencimiento o mensual, con la redención y la negociación que se pidan.</summary>
    private static async Task<Guid> LineaDeCdatAsync(HttpClient http, string admin, DateOnly hoy, string codigo, bool redimible = false, bool negocia = false,
        int gracia = 3)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = codigo, name = $"CDAT {codigo}", depositClass = "TermDeposit", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Reglamento de CDAT (e2e)", allowsPartialWithdrawals = false, negotiatedRateAllowed = negocia,
                rate = new { kind = "Fixed", effectiveAnnual = 0.10m }, balanceMethod = "DailyBalance",
                minimums = new { toOpen = 1_000_000m, toMaintain = 0m, toEarnInterest = 0m }, dayCount = "Actual360", rateEquivalence = "Compound",
                rounding = new { unit = 1m, mode = "AwayFromZero", moment = "PerTotal" }, settlementPeriodicity = "SinglePayment", tax = new { gmfApplies = false },
                termDeposit = new
                {
                    minTermDays = 30, maxTermDays = 720, interestPeriodicities = new[] { "SinglePayment", "Monthly" }, timings = new[] { "Arrears" },
                    rolloverAutomatic = true, rolloverRateRule = "CurrentTableRate", rolloverCapitalizes = false, graceBusinessDays = gracia, graceAccrues = false,
                    afterMaturity = "StopAccruing",
                    earlyRedemption = redimible
                        ? new { allowed = true, penaltyMode = (string?)"PenaltyRate", penaltyRateEa = (decimal?)0.02m, penaltyDiscount = (decimal?)null, minDaysBeforeRedemption = (int?)null }
                        : new { allowed = false, penaltyMode = (string?)null, penaltyRateEa = (decimal?)null, penaltyDiscount = (decimal?)null, minDaysBeforeRedemption = (int?)null },
                },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea de CDAT: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    private static object Pedido(Escenario e, Guid linea, decimal monto = 10_000_000m, int plazo = 180, string periodicidad = "SinglePayment",
        string fondeo = "TransferFromAccount", object? negociacion = null) => new
    {
        linePublicId = linea, holderPersonPublicId = e.Titular, officePublicId = e.Oficina, handlingMode = "Indistinct", parties = Array.Empty<object>(),
        beneficiaries = Array.Empty<object>(), amount = monto, termDays = plazo, interestPeriodicity = periodicidad, timing = "Arrears",
        paymentDestination = new { kind = "Account", accountPublicId = e.Ahorro }, maturityInstruction = "Rollover",
        funding = fondeo == "TransferFromAccount"
            ? new { kind = fondeo, sourceAccountPublicId = (Guid?)e.Ahorro, signers = new[] { e.Titular } }
            : new { kind = fondeo, sourceAccountPublicId = (Guid?)null, signers = Array.Empty<Guid>() },
        negotiation = negociacion,
    };

    private static async Task<JsonElement> ConstituidoAsync(HttpClient http, string token, object pedido)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Cdat, pedido);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"constituir: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    private static Guid Id(JsonElement cdat) => cdat.GetProperty("account").GetProperty("publicId").GetGuid();

    [Fact]
    public async Task US6_1_un_plazo_fuera_del_rango_se_rechaza_con_el_rango()
    {
        var e = await ArmarAsync("rango");
        using var http = fx.CreateClient();
        var linea = await LineaDeCdatAsync(http, e.Coop.TokenAdmin, CarteraE2E.Hoy(fx), "CDATRNG");

        var resp = await CarteraE2E.ConClaveAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, Cdat, Pedido(e, linea, plazo: 20));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be("Lending.TermDeposit.TermOutOfRange");
        (await resp.Content.ReadAsStringAsync()).Should().Contain("30").And.Contain("720");
    }

    [Fact]
    public async Task US6_2_en_la_fecha_del_pago_se_abona_el_interes_neto_de_retencion_en_la_cuenta()
    {
        var e = await ArmarAsync("mensual");
        using var http = fx.CreateClient();
        var hoy = CarteraE2E.Hoy(fx);
        var linea = await LineaDeCdatAsync(http, e.Coop.TokenAdmin, hoy, "CDATMEN");

        var simulada = await CarteraE2E.EnviarAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, $"{Cdat}/simulate", new
        {
            linePublicId = linea, amount = 10_000_000m, termDays = 180, interestPeriodicity = "Monthly", timing = "Arrears", startDate = hoy,
        });
        simulada.StatusCode.Should().Be(HttpStatusCode.OK, await simulada.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(simulada)).GetProperty("schedule").EnumerateArray().Should().HaveCount(6)
            .And.OnlyContain(p => p.GetProperty("interest").GetDecimal() == 79_741m, "investigación §3.2: seis pagos de 79.741");

        var cdat = await ConstituidoAsync(http, e.Coop.TokenAdmin, Pedido(e, linea, periodicidad: "Monthly"));
        var primerPago = DateOnly.Parse((await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cdat}/{Id(cdat)}/schedule"))[0].GetProperty("paymentDate").GetString()!);
        using (CausacionE2E.RelojEn(fx, primerPago))
            await CausacionE2E.CicloDeHoyAsync(fx, http, e.Coop.TokenAdmin, e.Coop.TenantPublicId);

        var plan = await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cdat}/{Id(cdat)}/schedule");
        plan[0].GetProperty("paidOn").ValueKind.Should().NotBe(JsonValueKind.Null, "el ciclo pagó el primer período");
        plan[0].GetProperty("net").GetDecimal().Should().Be(78_319m,
            "79.741 menos la retención del CDAT: la persona de prueba es natural, así que se descuenta el componente inflacionario del 2025 sembrado en la central "
            + "(55,43 % → 44.200; base 35.541) y el 4 % de esa base es 1.422");
        var pagos = await CausacionE2E.EnLaBaseAsync(fx, e.Coop.TenantPublicId, db => db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.Kind == MemberAccountTransactionKind.InterestPaid).CountAsync());
        pagos.Should().Be(1);
    }

    [Fact]
    public async Task US6_3_vencido_sin_instruccion_pasada_la_gracia_se_prorroga_con_titulo_nuevo()
    {
        var e = await ArmarAsync("prorroga");
        using var http = fx.CreateClient();
        var linea = await LineaDeCdatAsync(http, e.Coop.TokenAdmin, CarteraE2E.Hoy(fx), "CDATPRO");
        var cdat = await ConstituidoAsync(http, e.Coop.TokenAdmin, Pedido(e, linea, plazo: 30));
        var vence = DateOnly.Parse(cdat.GetProperty("currentTerms").GetProperty("maturityDate").GetString()!);
        var gracia = DateOnly.Parse(cdat.GetProperty("currentTerms").GetProperty("graceUntil").GetString()!);
        gracia.Should().BeAfter(vence);

        using (CausacionE2E.RelojEn(fx, gracia))
            await CausacionE2E.CicloDeHoyAsync(fx, http, e.Coop.TokenAdmin, e.Coop.TenantPublicId);

        var prorrogado = await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cdat}/{Id(cdat)}");
        var vigente = prorrogado.GetProperty("currentTerms");
        vigente.GetProperty("version").GetInt32().Should().Be(2);
        vigente.GetProperty("rolloverCount").GetInt32().Should().Be(1);
        vigente.GetProperty("titleNumber").GetString().Should().EndWith("-2", "el título nuevo enlazado lleva el sufijo de la versión (R11)");
        prorrogado.GetProperty("termsHistory").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task US6_4_la_redencion_anticipada_se_rechaza_si_la_linea_no_la_admite_y_si_la_admite_se_simula_la_penalidad()
    {
        var e = await ArmarAsync("redencion");
        using var http = fx.CreateClient();
        var hoy = CarteraE2E.Hoy(fx);
        var sinRedencion = await LineaDeCdatAsync(http, e.Coop.TokenAdmin, hoy, "CDATNOR");
        var conRedencion = await LineaDeCdatAsync(http, e.Coop.TokenAdmin, hoy, "CDATRED", redimible: true);
        var noRedimible = await ConstituidoAsync(http, e.Coop.TokenAdmin, Pedido(e, sinRedencion));
        var redimible = await ConstituidoAsync(http, e.Coop.TokenAdmin, Pedido(e, conRedencion));

        using (CausacionE2E.RelojEn(fx, hoy.AddDays(90)))
        {
            await CausacionE2E.CicloDeHoyAsync(fx, http, e.Coop.TokenAdmin, e.Coop.TenantPublicId);
            var rechazada = await CarteraE2E.EnviarAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, $"{Cdat}/{Id(noRedimible)}/redeem/simulate", new { date = hoy.AddDays(90) });
            rechazada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CarteraE2E.CodigoDeErrorAsync(rechazada)).Should().Be("Lending.TermDeposit.EarlyRedemptionNotAllowed");

            var simulada = await CarteraE2E.EnviarAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, $"{Cdat}/{Id(redimible)}/redeem/simulate", new { date = hoy.AddDays(90) });
            simulada.StatusCode.Should().Be(HttpStatusCode.OK, await simulada.Content.ReadAsStringAsync());
            var previa = await CarteraE2E.LeerAsync(simulada);
            previa.GetProperty("isEarly").GetBoolean().Should().BeTrue();
            previa.GetProperty("penalty").GetProperty("amount").GetDecimal().Should().BePositive("se reliquida al 2 % EA");
            previa.GetProperty("interest").GetDecimal().Should().Be(49_629m, "10.000.000 × (1,02^(90/360) − 1), como el caso dorado cdat-06");

            var redimido = await CarteraE2E.ConClaveAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, $"{Cdat}/{Id(redimible)}/redeem", new
            {
                destination = new { kind = "Account", targetAccountPublicId = e.Ahorro }, expectedPreviewSha256 = previa.GetProperty("fingerprint").GetString(),
                reason = "Redención anticipada pedida por el titular (e2e)",
            });
            redimido.StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.Accepted], await redimido.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task US6_5_un_CDAT_embargado_no_se_paga_al_vencer_y_queda_a_disposicion()
    {
        var e = await ArmarAsync("embargo");
        using var http = fx.CreateClient();
        var linea = await LineaDeCdatAsync(http, e.Coop.TokenAdmin, CarteraE2E.Hoy(fx), "CDATEMB");
        var cdat = await ConstituidoAsync(http, e.Coop.TokenAdmin, Pedido(e, linea, plazo: 30));
        var gracia = DateOnly.Parse(cdat.GetProperty("currentTerms").GetProperty("graceUntil").GetString()!);
        // Los embargos llegan con US11: la marca la escribe aquí la prueba, como lo hará el registro de embargos.
        await CausacionE2E.EnLaBaseAsync(fx, e.Coop.TenantPublicId, async db =>
        {
            var cuenta = await db.MemberAccounts.SingleAsync(a => a.PublicId == Id(cdat));
            cuenta.IsGarnished = true;
            return await db.SaveChangesAsync();
        });

        using (CausacionE2E.RelojEn(fx, gracia))
            await CausacionE2E.CicloDeHoyAsync(fx, http, e.Coop.TokenAdmin, e.Coop.TenantPublicId);

        var vencido = await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cdat}/{Id(cdat)}");
        vencido.GetProperty("status").GetInt32().Should().Be((int)MemberAccountStatus.Matured);
        vencido.GetProperty("currentTerms").GetProperty("version").GetInt32().Should().Be(1, "no se prorroga");
        var pagos = await CausacionE2E.EnLaBaseAsync(fx, e.Coop.TenantPublicId, db => db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.Kind == MemberAccountTransactionKind.InterestPaid).CountAsync());
        pagos.Should().Be(0, "el CDAT embargado no se paga al vencimiento (FR-072)");
    }

    [Fact]
    public async Task US6_6_la_tasa_negociada_fuera_de_la_atribucion_espera_al_aprobador_y_sobre_la_regla_se_rechaza()
    {
        var e = await ArmarAsync("negociada");
        using var http = fx.CreateClient();
        var hoy = CarteraE2E.Hoy(fx);
        var admin = e.Coop.TokenAdmin;
        var linea = await LineaDeCdatAsync(http, admin, hoy, "CDATNEG", negocia: true);
        var regla = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/{linea}/negotiated-rate-rules", new
        {
            code = "GRANDES", priority = 1, criteria = new { amountFrom = 100_000_000m, termDaysFrom = 360, termDaysTo = 360 }, maxMarginEa = 0.015m,
            validFrom = hoy, reason = "Depositantes de más de cien millones a un año (e2e)",
        });
        regla.StatusCode.Should().Be(HttpStatusCode.Created, await regla.Content.ReadAsStringAsync());

        // El asesor de O1 pide y no aprueba; el jefe de captaciones aprueba.
        var asesor = e.Usuarios.AsesorO1;
        var jefe = e.Usuarios.Jefe;
        foreach (var (usuario, plantilla) in new[] { (asesor, "cartera.asesor-captaciones"), (jefe, "cartera.jefe-captaciones") })
        {
            var rol = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario.PublicId}/roles",
                new { rolePublicId = e.Usuarios.RolesSugeridos[plantilla] });
            if (!rol.IsSuccessStatusCode)
                (await CarteraE2E.CodigoDeErrorAsync(rol)).Should().Be("Security.Users.AlreadyAssignedRole", await rol.Content.ReadAsStringAsync());
            await UsuariosDeCartera.FijarLineasAsync(http, admin, usuario, linea);
        }
        var tokenAsesor = await UsuariosDeCartera.IngresarAsync(http, asesor);
        var tokenJefe = await UsuariosDeCartera.IngresarAsync(http, jefe);

        var sobre = await CarteraE2E.ConClaveAsync(http, tokenAsesor, HttpMethod.Post, Cdat, Pedido(e, linea, 200_000_000m, 360, fondeo: "Collector",
            negociacion: new { requestedMarginEa = 0.02m, reason = "Pide dos puntos" }));
        sobre.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(sobre)).Should().Be("Lending.NegotiatedRate.MarginAboveRule");
        (await CarteraE2E.LeerAsync(sobre)).GetProperty("data").GetProperty("maxMarginEa").GetDecimal().Should().Be(0.015m);

        var pedido = await ConstituidoAsync(http, tokenAsesor, Pedido(e, linea, 200_000_000m, 360, fondeo: "Collector",
            negociacion: new { requestedMarginEa = 0.012m, reason = "Asociado con 200 millones en captaciones" }));
        pedido.GetProperty("status").GetInt32().Should().Be((int)MemberAccountStatus.Opening);
        var solicitud = pedido.GetProperty("pendingNegotiation");
        solicitud.GetProperty("status").GetInt32().Should().Be((int)NegotiatedRateRequestStatus.PendingDecision, "la atribución del asesor no alcanza");

        var porElAsesor = await CarteraE2E.ConClaveAsync(http, tokenAsesor, HttpMethod.Post,
            $"/api/lending/negotiated-rate-requests/{solicitud.GetProperty("publicId").GetGuid()}/approve", new { reason = "Se la aprueba él mismo" });
        porElAsesor.IsSuccessStatusCode.Should().BeFalse("quien la pide no la decide");

        var aprobada = await CarteraE2E.ConClaveAsync(http, tokenJefe, HttpMethod.Post,
            $"/api/lending/negotiated-rate-requests/{solicitud.GetProperty("publicId").GetGuid()}/approve", new { reason = "Aprobada por el jefe de captaciones" });
        aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());

        var fondeo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Doble}/deposits", new
        {
            deposits = new[]
            {
                new
                {
                    idempotencyKey = $"TST:{Guid.NewGuid():N}", accountPublicId = Id(pedido), modality = "TermDepositFunding", amount = 200_000_000m,
                    valueDate = hoy, meansClass = "Transfer", depositorPublicId = e.Titular, receipt = MovimientosTests.Recibo(hoy),
                },
            },
        });
        fondeo.StatusCode.Should().Be(HttpStatusCode.OK, await fondeo.Content.ReadAsStringAsync());

        var sellado = (await CarteraE2E.GetAsync(http, admin, $"{Cdat}/{Id(pedido)}")).GetProperty("currentTerms");
        sellado.GetProperty("tableRate").GetDecimal().Should().Be(0.10m);
        sellado.GetProperty("effectiveAnnual").GetDecimal().Should().Be(0.112m);
        sellado.GetProperty("negotiated").GetProperty("marginEa").GetDecimal().Should().Be(0.012m);
    }
}
