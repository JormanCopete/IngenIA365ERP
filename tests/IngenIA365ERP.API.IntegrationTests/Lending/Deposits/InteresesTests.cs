using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Rates;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T140 (US5, escenarios 1 a 5 de la spec; contracts/api.md §4): la causación y la liquidación de intereses por HTTP, en una cooperativa aislada
/// con captaciones activadas (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>), con el ciclo diario ordenado por la ruta y corrido a mano
/// (<see cref="CausacionE2E"/>) y el reloj de la suite llevado al mes siguiente para que el período quede entero:
/// <list type="number">
/// <item>Escenario 1: con saldos de 2.000.000, 5.000.000 y 1.500.000 en el mes, el interés liquidado es la suma de los intereses diarios al 3 % EA real/365.</item>
/// <item>Escenario 2: con el mínimo para interés en 2.500.000, los días bajo el mínimo causan cero y la simulación lo explica.</item>
/// <item>Escenario 3: liquidar otra vez el mismo período responde 200 con la existente.</item>
/// <item>Escenario 4: con 400.000.000 algún día alcanza el umbral de 0,055 UVT y se retiene el 7 % del total antes de abonar.</item>
/// <item>Escenario 5: con días sin correr el proceso, la corrida siguiente pone al día la causación desde el último día causado.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class InteresesTests(CentralIdentityApiFixture fx)
{
    private const string Cuentas = "/api/lending/accounts";
    private const string Lineas = "/api/lending/lines";

    private static readonly decimal TasaDiaria = EquivalenciaDeTasas.TasaDiaria(0.03m, DayCountConvention.Actual365, RateEquivalence.Compound,
        RepaymentPeriodicity.Monthly);

    private async Task<(CaptacionesE2E.CooperativaDeCaptaciones Coop, Guid Oficina)> CooperativaAsync()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "intereses");
        using var http = fx.CreateClient();
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        return (coop, usuarios.O1.AgenciaPublicId);
    }

    private static async Task<Guid> LineaAlTresAsync(HttpClient http, string admin, DateOnly desde, string codigo, decimal minimoParaInteres = 0m)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = codigo, name = $"Ahorro {codigo}", depositClass = "Demand", active = true,
            firstVersion = new
            {
                validFrom = desde, reason = "Tasa del 3 % (e2e de intereses)", allowsPartialWithdrawals = true,
                rate = new { kind = "Fixed", effectiveAnnual = 0.03m }, balanceMethod = "DailyBalance",
                minimums = new { toOpen = 0m, toMaintain = 0m, toEarnInterest = minimoParaInteres }, dayCount = "Actual365", rateEquivalence = "Compound",
                rounding = new { unit = 1m, mode = "AwayFromZero", moment = "PerTotal" }, settlementPeriodicity = "Monthly", tax = new { gmfApplies = true },
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    /// <summary>El primer día del mes siguiente al de hoy y el fin de ese mes: un período de liquidación entero por delante.</summary>
    private DateOnly InicioDelMesSiguiente()
    {
        var hoy = CarteraE2E.Hoy(fx);
        return new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(1);
    }

    private static DateOnly FinDeMes(DateOnly d) => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));

    /// <summary>
    /// Abre la cuenta el día <paramref name="inicio"/> con 2.000.000, consigna 3.000.000 el día +10 y retira 3.500.000 el día +20, y corre el ciclo al fin del
    /// mes. Devuelve la cuenta.
    /// </summary>
    private async Task<Guid> MesAsync(HttpClient http, CaptacionesE2E.CooperativaDeCaptaciones coop, Guid oficina, string codigo, decimal minimo = 0m,
        decimal inicial = 2_000_000m)
    {
        var inicio = InicioDelMesSiguiente();
        var admin = coop.TokenAdmin;
        Guid cuenta;
        Guid titular;
        using (CausacionE2E.RelojEn(fx, inicio))
        {
            var linea = await LineaAlTresAsync(http, admin, inicio, codigo, minimo);
            titular = await MovimientosTests.AsociadoAsync(http, admin, $"Interes{codigo}");
            cuenta = await MovimientosTests.CuentaAsync(http, admin, oficina, linea, titular, exenta: true);
            await MovimientosTests.ConsignarAsync(http, admin, cuenta, titular, inicial, inicio, medio: "Transfer");
        }
        if (inicial == 2_000_000m)
        {
            using (CausacionE2E.RelojEn(fx, inicio.AddDays(10)))
                await MovimientosTests.ConsignarAsync(http, admin, cuenta, titular, 3_000_000m, inicio.AddDays(10), medio: "Transfer");
            using (CausacionE2E.RelojEn(fx, inicio.AddDays(20)))
                (await MovimientosTests.RetirarAsync(http, admin, cuenta, titular, 3_500_000m, inicio.AddDays(20))).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        using (CausacionE2E.RelojEn(fx, FinDeMes(inicio)))
            await CausacionE2E.CicloDeHoyAsync(fx, http, admin, coop.TenantPublicId);
        return cuenta;
    }

    private static async Task<JsonElement> LiquidacionesAsync(HttpClient http, string admin, Guid cuenta) =>
        await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/settlements");

    [Fact]
    public async Task US5_1_el_interes_liquidado_es_la_suma_de_los_intereses_de_cada_saldo_diario()
    {
        var (coop, oficina) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var cuenta = await MesAsync(http, coop, oficina, "AHINT1");

        var inicio = InicioDelMesSiguiente();
        var dias = FinDeMes(inicio).DayNumber - inicio.DayNumber;
        var esperado = InteresDeCaptacion.Seis(TasaDiaria * (2_000_000m * 9 + 5_000_000m * 10 + 1_500_000m * (dias - 19)));
        var lista = (await LiquidacionesAsync(http, coop.TokenAdmin, cuenta)).GetProperty("items");
        lista.GetArrayLength().Should().Be(1);
        var l = lista[0];
        l.GetProperty("accrued").GetDecimal().Should().Be(esperado);
        l.GetProperty("settled").GetDecimal().Should().Be(Math.Round(esperado, 0, MidpointRounding.AwayFromZero));
        l.GetProperty("withholding").GetDecimal().Should().Be(0m, "ningún día alcanza el umbral diario");
    }

    [Fact]
    public async Task US5_2_un_dia_bajo_el_minimo_para_interes_causa_cero_y_la_simulacion_lo_explica()
    {
        var (coop, oficina) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var inicio = InicioDelMesSiguiente();
        Guid cuenta;
        using (CausacionE2E.RelojEn(fx, inicio))
        {
            var linea = await LineaAlTresAsync(http, coop.TokenAdmin, inicio, "AHINT2", minimoParaInteres: 2_500_000m);
            var titular = await MovimientosTests.AsociadoAsync(http, coop.TokenAdmin, "Minimo");
            cuenta = await MovimientosTests.CuentaAsync(http, coop.TokenAdmin, oficina, linea, titular, exenta: true);
            await MovimientosTests.ConsignarAsync(http, coop.TokenAdmin, cuenta, titular, 2_000_000m, inicio, medio: "Transfer");
            var resp = await CarteraE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{Cuentas}/{cuenta}/settlements/simulate", new { date = inicio.AddDays(5) });
            resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
            var simulada = await CarteraE2E.LeerAsync(resp);
            simulada.GetProperty("accrued").GetDecimal().Should().Be(0m);
            simulada.GetProperty("explanation").EnumerateArray().Should().OnlyContain(d => d.GetProperty("belowMinimum").GetBoolean());
        }
    }

    [Fact]
    public async Task US5_3_liquidar_otra_vez_el_mismo_periodo_responde_la_existente()
    {
        var (coop, oficina) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var cuenta = await MesAsync(http, coop, oficina, "AHINT3");
        var fin = FinDeMes(InicioDelMesSiguiente());

        using (CausacionE2E.RelojEn(fx, fin.AddDays(1)))
        {
            var resp = await CarteraE2E.ConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{Cuentas}/{cuenta}/settlements",
                new { periodTo = fin, reason = "Liquidación manual repetida (e2e)" });
            resp.StatusCode.Should().Be(HttpStatusCode.OK, "SC-003: idempotente por cuenta y período");
            (await CarteraE2E.LeerAsync(resp)).GetProperty("alreadyExisted").GetBoolean().Should().BeTrue();
        }
        (await LiquidacionesAsync(http, coop.TokenAdmin, cuenta)).GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task US5_4_sobre_el_umbral_diario_se_retiene_el_siete_por_ciento_del_total()
    {
        var (coop, oficina) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var cuenta = await MesAsync(http, coop, oficina, "AHINT4", inicial: 400_000_000m);

        var l = (await LiquidacionesAsync(http, coop.TokenAdmin, cuenta)).GetProperty("items")[0];
        var bruto = l.GetProperty("settled").GetDecimal();
        l.GetProperty("thresholdReached").GetBoolean().Should().BeTrue();
        l.GetProperty("withholding").GetDecimal().Should().Be(Math.Round(bruto * 0.07m, 0, MidpointRounding.AwayFromZero),
            "la persona de prueba no tiene componente inflacionario publicado en la central de la suite");
        l.GetProperty("net").GetDecimal().Should().Be(bruto - l.GetProperty("withholding").GetDecimal());
    }

    [Fact]
    public async Task US5_5_con_dias_sin_proceso_la_corrida_siguiente_pone_al_dia_la_causacion()
    {
        var (coop, oficina) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var inicio = InicioDelMesSiguiente();
        Guid cuenta;
        using (CausacionE2E.RelojEn(fx, inicio))
        {
            var linea = await LineaAlTresAsync(http, coop.TokenAdmin, inicio, "AHINT5");
            var titular = await MovimientosTests.AsociadoAsync(http, coop.TokenAdmin, "Atraso");
            cuenta = await MovimientosTests.CuentaAsync(http, coop.TokenAdmin, oficina, linea, titular, exenta: true);
            await MovimientosTests.ConsignarAsync(http, coop.TokenAdmin, cuenta, titular, 1_000_000m, inicio, medio: "Transfer");
        }
        using (CausacionE2E.RelojEn(fx, inicio.AddDays(6)))
            await CausacionE2E.CicloDeHoyAsync(fx, http, coop.TokenAdmin, coop.TenantPublicId);

        var marca = await CausacionE2E.EnLaBaseAsync(fx, coop.TenantPublicId, db => (from c in db.MemberAccountControls.AsNoTracking()
                                                                                     join a in db.MemberAccounts.AsNoTracking() on c.MemberAccountId equals a.Id
                                                                                     where a.PublicId == cuenta
                                                                                     select c.LastAccruedDate).SingleAsync());
        marca.Should().Be(inicio.AddDays(6));
        var filas = await CausacionE2E.EnLaBaseAsync(fx, coop.TenantPublicId, db => (from t in db.MemberAccountTransactions.AsNoTracking()
                                                                                     join a in db.MemberAccounts.AsNoTracking() on t.MemberAccountId equals a.Id
                                                                                     where a.PublicId == cuenta && t.Kind == MemberAccountTransactionKind.InterestAccrued
                                                                                     select t.ValueDate).ToListAsync());
        filas.Should().BeEquivalentTo(Enumerable.Range(1, 6).Select(i => inicio.AddDays(i)), "un día de causación por cada día que faltaba, en orden");
    }
}
