using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T186 (US7, escenarios 1 y 2 de la spec; contracts/api.md §3 y §4): el ahorro contractual por HTTP en una cooperativa aislada con captaciones
/// activadas (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>), con el ciclo diario corrido a mano (<see cref="CausacionE2E"/>) y el reloj de la
/// suite llevado a cada fecha. La línea: 12 cuotas mensuales de 200.000 al 6 % EA en 30/360, bonificada al 8 %, castigo al 3 %, una cuota tolerada.
/// <list type="number">
/// <item>Escenario 1 (US7-1): con todas las cuotas pagadas, al vencer se liquida con la tasa bonificada —la bonificación de 21.471 de la investigación
/// (86.777 − 65.306)— y el saldo se traslada a la cuenta a la vista según la instrucción; el contrato queda «cumplido» y la cuenta cancelada.</item>
/// <item>Escenario 2 (US7-2): con más cuotas atrasadas que las toleradas, pasados los días de gracia, queda «incumplido»: se reliquida a la tasa de castigo
/// (la penalidad sale del capital), se sella la versión 2 de condiciones a la tasa de castigo y el contrato sigue hasta su vencimiento.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class AhorroContractualTests(CentralIdentityApiFixture fx)
{
    private const string Cuentas = "/api/lending/accounts";
    private const string Lineas = "/api/lending/lines";
    private const string Doble = "/api/lending/test-doubles/collector/accounts";
    private const decimal Cuota = 200_000m;

    private sealed record Escenario(CaptacionesE2E.CooperativaDeCaptaciones Coop, Guid Oficina, Guid Vista, Guid Titular, DateOnly Hoy);

    private async Task<Escenario> ArmarAsync(string nombre)
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, $"contrato_{nombre}");
        using var http = fx.CreateClient();
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var hoy = CarteraE2E.Hoy(fx);
        var ahorro = await MovimientosTests.LineaALaVistaAsync(http, coop.TokenAdmin, hoy, $"AHK{nombre[..Math.Min(4, nombre.Length)].ToUpperInvariant()}");
        var titular = await MovimientosTests.AsociadoAsync(http, coop.TokenAdmin, $"Contrato{nombre}");
        var vista = await MovimientosTests.CuentaAsync(http, coop.TokenAdmin, usuarios.O1.AgenciaPublicId, ahorro, titular);
        return new Escenario(coop, usuarios.O1.AgenciaPublicId, vista, titular, hoy);
    }

    /// <summary>La línea contractual del escenario: cuota mensual desde 100.000, de 6 a 36 meses, 6 % EA en 30/360, bonificada al 8 %, castigo al 3 %.</summary>
    private static async Task<Guid> LineaContractualAsync(HttpClient http, string admin, DateOnly hoy, string codigo, int tolerancia = 1,
        string efecto = "BreachRate", string destinos = "All")
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = codigo, name = $"Ahorro contractual {codigo}", depositClass = "Contractual", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Reglamento del ahorro contractual (e2e)", allowsPartialWithdrawals = false,
                rate = new { kind = "Fixed", effectiveAnnual = 0.06m }, balanceMethod = "DailyBalance",
                minimums = new { toOpen = 0m, toMaintain = 0m, toEarnInterest = 0m }, dayCount = "Thirty360", rateEquivalence = "Compound",
                rounding = new { unit = 1m, mode = "AwayFromZero", moment = "PerTotal" }, settlementPeriodicity = "Monthly", tax = new { gmfApplies = false },
                contractual = new
                {
                    installmentPeriodicity = "Monthly", minInstallment = 100_000m, termMonthsFrom = 6, termMonthsTo = 36, bonusRateEa = 0.08m,
                    breachRateEa = 0.03m, toleranceInstallments = tolerancia, breachEffect = efecto, bonusRecognition = "AtFulfillment", maturityDestinations = destinos,
                },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea contractual: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    private static async Task<Guid> ContratoAsync(HttpClient http, Escenario e, Guid linea, bool conInstruccion = true)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, Cuentas, new
        {
            linePublicId = linea, holderPersonPublicId = e.Titular, officePublicId = e.Oficina, handlingMode = "Indistinct", parties = Array.Empty<object>(),
            beneficiaries = Array.Empty<object>(),
            contractual = new
            {
                installment = Cuota, termMonths = 12,
                maturityInstruction = conInstruccion ? new { kind = "TransferToAccount", targetAccountPublicId = (Guid?)e.Vista } : null,
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir el contractual: «{await resp.Content.ReadAsStringAsync()}»");
        var cuenta = await CarteraE2E.LeerAsync(resp);
        cuenta.GetProperty("contractual").GetProperty("installments").GetInt32().Should().Be(12);
        return cuenta.GetProperty("publicId").GetGuid();
    }

    private static async Task PagarCuotaAsync(HttpClient http, Escenario e, Guid cuenta, DateOnly fecha)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, $"{Doble}/deposits", new
        {
            deposits = new[]
            {
                new
                {
                    idempotencyKey = $"TST:{Guid.NewGuid():N}", accountPublicId = cuenta, modality = "Installments", amount = Cuota, valueDate = fecha,
                    meansClass = "Transfer", depositorPublicId = e.Titular, receipt = MovimientosTests.Recibo(fecha),
                },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(resp)).GetProperty("deposits")[0].GetProperty("result").GetInt32().Should().Be(1, $"la cuota del {fecha:yyyy-MM-dd}");
    }

    /// <summary>El ciclo de cada mes desde el día siguiente a hoy hasta <paramref name="hasta"/>, pagando la cuota del día si <paramref name="paga"/> lo dice.</summary>
    private async Task MesesAsync(HttpClient http, Escenario e, Guid cuenta, DateOnly hasta, Func<DateOnly, bool> paga)
    {
        for (var mes = 1; e.Hoy.AddMonths(mes) <= hasta; mes++)
        {
            var dia = e.Hoy.AddMonths(mes);
            using var reloj = CausacionE2E.RelojEn(fx, dia);
            if (paga(dia)) await PagarCuotaAsync(http, e, cuenta, dia);
            await CausacionE2E.CicloDeHoyAsync(fx, http, e.Coop.TokenAdmin, e.Coop.TenantPublicId);
        }
    }

    [Fact]
    public async Task US7_1_cumplido_al_vencer_abona_la_bonificacion_y_lo_traslada_a_la_vista()
    {
        var e = await ArmarAsync("cumple");
        using var http = fx.CreateClient();
        var linea = await LineaContractualAsync(http, e.Coop.TokenAdmin, e.Hoy, "CONTRAT1");
        var cuenta = await ContratoAsync(http, e, linea);
        var vence = e.Hoy.AddMonths(12);

        var contrato = await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cuentas}/{cuenta}/contractual");
        contrato.GetProperty("terms").GetProperty("projectedFinalAmount").GetDecimal().Should().Be(2_486_777m, "investigación §3.4, cumpliendo al 8 %");

        await MesesAsync(http, e, cuenta, vence, _ => true);

        var final = await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cuentas}/{cuenta}");
        final.GetProperty("status").GetInt32().Should().Be((int)MemberAccountStatus.Closed);
        final.GetProperty("contractual").GetProperty("status").GetInt32().Should().Be((int)ContractualStatus.Fulfilled);
        var bonificacion = await CausacionE2E.EnLaBaseAsync(fx, e.Coop.TenantPublicId, db => db.MemberAccountOperations.AsNoTracking()
            .Where(o => o.Kind == MemberAccountOperationKind.ContractualMaturity).Select(o => o.Amount).SingleAsync());
        bonificacion.Should().Be(21_471m, "86.777 − 65.306 sobre los doce aportes");
        var vista = await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cuentas}/{e.Vista}");
        vista.GetProperty("balances").GetProperty("balance").GetDecimal().Should().BeInRange(2_486_777m, 2_486_777m + 400m,
            "lo ahorrado con el interés base que causó el libro y la bonificación (research §19)");
        var hechos = await CausacionE2E.EnLaBaseAsync(fx, e.Coop.TenantPublicId, db => db.IntegrationMessages.AsNoTracking()
            .CountAsync(m => m.Type == IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.BonificacionReconocidaV1.Type));
        hechos.Should().Be(1);
    }

    [Fact]
    public async Task US7_2_mas_atrasos_que_los_tolerados_lo_dejan_incumplido_a_la_tasa_de_castigo()
    {
        var e = await ArmarAsync("incumple");
        using var http = fx.CreateClient();
        var linea = await LineaContractualAsync(http, e.Coop.TokenAdmin, e.Hoy, "CONTRAT2");
        var cuenta = await ContratoAsync(http, e, linea);

        // Paga sólo la primera; la segunda queda atrasada dentro de la tolerancia y la tercera la supera.
        await MesesAsync(http, e, cuenta, e.Hoy.AddMonths(3), dia => dia == e.Hoy.AddMonths(1));
        var atrasado = await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cuentas}/{cuenta}/contractual");
        atrasado.GetProperty("status").GetInt32().Should().Be((int)ContractualStatus.Current, "los días de gracia de la tercera no han pasado");

        var pasadaLaGracia = e.Hoy.AddMonths(3).AddDays(6);
        using (CausacionE2E.RelojEn(fx, pasadaLaGracia))
            await CausacionE2E.CicloDeHoyAsync(fx, http, e.Coop.TokenAdmin, e.Coop.TenantPublicId);

        var incumplido = await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cuentas}/{cuenta}/contractual");
        incumplido.GetProperty("status").GetInt32().Should().Be((int)ContractualStatus.Breached);
        incumplido.GetProperty("accountStatus").GetInt32().Should().Be((int)MemberAccountStatus.Active, "con el efecto BreachRate sigue hasta su vencimiento");
        var condiciones = await CarteraE2E.GetAsync(http, e.Coop.TokenAdmin, $"{Cuentas}/{cuenta}/terms");
        condiciones.EnumerateArray().Should().HaveCount(2);
        condiciones.EnumerateArray().Should().Contain(v => v.GetProperty("cause").GetInt32() == (int)AccountTermsCause.ContractualBreach
                                                           && v.GetProperty("appliedRate").GetDecimal() == 0.03m);
        var penalidad = await CausacionE2E.EnLaBaseAsync(fx, e.Coop.TenantPublicId, db => db.MemberAccountOperations.AsNoTracking()
            .Where(o => o.Kind == MemberAccountOperationKind.ContractualBreach).Select(o => o.Amount).SingleAsync());
        penalidad.Should().BePositive("lo que la tasa base dio de más sobre el aporte pagado");
        var hechos = await CausacionE2E.EnLaBaseAsync(fx, e.Coop.TenantPublicId, db => db.IntegrationMessages.AsNoTracking()
            .CountAsync(m => m.Type == IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.PenalidadAplicadaV1.Type));
        hechos.Should().Be(1);
    }

    [Fact]
    public async Task E248_la_linea_admite_el_traslado_y_lo_decide_cada_asociado_en_su_contrato()
    {
        var e = await ArmarAsync("destino");
        using var http = fx.CreateClient();
        var admin = e.Coop.TokenAdmin;
        var vence = e.Hoy.AddMonths(12);

        // Una línea que sólo admite el giro no recibe una instrucción de traslado: se rechaza con su código, antes de abrir la cuenta.
        var soloGiro = await LineaContractualAsync(http, admin, e.Hoy, "CONTRAT4", destinos: "DisbursementOrder");
        var rechazado = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Cuentas, new
        {
            linePublicId = soloGiro, holderPersonPublicId = e.Titular, officePublicId = e.Oficina, handlingMode = "Indistinct", parties = Array.Empty<object>(),
            beneficiaries = Array.Empty<object>(),
            contractual = new { installment = Cuota, termMonths = 12, maturityInstruction = new { kind = "TransferToAccount", targetAccountPublicId = e.Vista } },
        });
        (await CarteraE2E.CodigoDeErrorAsync(rechazado)).Should().Be("Lending.Contractual.DestinationNotAllowedByLine");

        // La misma línea (admite ambos): un asociado deja la instrucción de trasladar a su cuenta a la vista y el otro no deja ninguna.
        var linea = await LineaContractualAsync(http, admin, e.Hoy, "CONTRAT3");
        var consulta = await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}");
        consulta.GetProperty("depositVersion").GetProperty("contractual").GetProperty("maturityDestinations").GetInt32().Should().Be(3);
        var otro = e with { Titular = await MovimientosTests.AsociadoAsync(http, admin, "ContratoSinInstruccion") };
        var conTraslado = await ContratoAsync(http, e, linea);
        var sinInstruccion = await ContratoAsync(http, otro, linea, conInstruccion: false);
        (await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{sinInstruccion}/contractual")).GetProperty("allowedMaturityDestinations").GetInt32().Should().Be(3);

        // Los dos pagan sus doce cuotas, mes a mes, en la misma corrida del ciclo diario (el reloj no vuelve atrás).
        for (var mes = 1; e.Hoy.AddMonths(mes) <= vence; mes++)
        {
            var dia = e.Hoy.AddMonths(mes);
            using var reloj = CausacionE2E.RelojEn(fx, dia);
            await PagarCuotaAsync(http, e, conTraslado, dia);
            await PagarCuotaAsync(http, otro, sinInstruccion, dia);
            await CausacionE2E.CicloDeHoyAsync(fx, http, admin, e.Coop.TenantPublicId);
        }

        var trasladado = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{conTraslado}");
        trasladado.GetProperty("status").GetInt32().Should().Be((int)MemberAccountStatus.Closed);
        (await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{e.Vista}")).GetProperty("balances").GetProperty("balance").GetDecimal()
            .Should().BeGreaterThan(2_400_000m, "el traslado a la vista de quien lo pidió");
        var aDisposicion = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{sinInstruccion}");
        aDisposicion.GetProperty("status").GetInt32().Should().Be((int)MemberAccountStatus.Matured, "quien no lo pidió queda a disposición: no se movió dinero");
        aDisposicion.GetProperty("balances").GetProperty("balance").GetDecimal().Should().BeGreaterThan(2_400_000m);
        var traslados = await CausacionE2E.EnLaBaseAsync(fx, e.Coop.TenantPublicId, db => db.IntegrationMessages.AsNoTracking()
            .CountAsync(m => m.Type == IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.TrasladoEntreCuentasAplicadoV1.Type));
        traslados.Should().Be(1, "sólo el contrato con instrucción generó el traslado");
    }

    [Fact]
    public async Task E263_el_asociado_retira_la_instruccion_y_el_contrato_vence_a_disposicion()
    {
        var e = await ArmarAsync("retira");
        using var http = fx.CreateClient();
        var admin = e.Coop.TokenAdmin;
        var linea = await LineaContractualAsync(http, admin, e.Hoy, "CONTRAT5");
        var cuenta = await ContratoAsync(http, e, linea);
        var vence = e.Hoy.AddMonths(12);
        (await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/contractual")).GetProperty("instruction").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);

        var retiro = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/maturity-instruction/withdraw",
            new { reason = "El asociado prefiere decidir al vencer" });
        retiro.StatusCode.Should().Be(HttpStatusCode.OK, await retiro.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(retiro)).GetProperty("instruction").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        var repetido = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/maturity-instruction/withdraw",
            new { reason = "El asociado prefiere decidir al vencer" });
        repetido.StatusCode.Should().Be(HttpStatusCode.OK, "retirar lo ya retirado no falla");

        await MesesAsync(http, e, cuenta, vence, _ => true);

        var final = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}");
        final.GetProperty("status").GetInt32().Should().Be((int)MemberAccountStatus.Matured, "a disposición del titular: no se movió dinero");
        final.GetProperty("balances").GetProperty("balance").GetDecimal().Should().BeGreaterThan(2_400_000m);
        (await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{e.Vista}")).GetProperty("balances").GetProperty("balance").GetDecimal().Should().Be(0m);
        var alertas = await CausacionE2E.EnLaBaseAsync(fx, e.Coop.TenantPublicId, db => db.Alerts.AsNoTracking().CountAsync(a => a.TypeCode == "Cartera.ContractualADisposicion"));
        alertas.Should().BeGreaterThan(0, "se levanta la alerta Cartera.ContractualADisposicion");

        var tarde = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/maturity-instruction/withdraw", new { reason = "Ya venció" });
        tarde.StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await CarteraE2E.CodigoDeErrorAsync(tarde)).Should().Be("Lending.Contractual.NotCurrent");
    }
}
