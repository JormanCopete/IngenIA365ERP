using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T206 (US12, escenarios 1 y 2 de la spec; contracts/api.md §12; research §22): la asamblea por HTTP, sobre la cooperativa del desembolso de
/// la 013 con aportes y captaciones activados (<see cref="CaptacionesE2E.CooperativaConCaptacionesAsync"/>, nombre <c>cap_asamblea</c>). Ana y Beto aportan
/// hoy; el reloj de la suite pasa al 20 de marzo del año siguiente (la asamblea ordinaria) y la decisión es sobre el ejercicio de hoy.
/// <list type="number">
/// <item>US12-1: revalorizar al 5 % sobre el saldo promedio con el IPC del ejercicio (valor de la central) y un fondo menor que lo calculado: la simulación
/// informa el límite y el prorrateo, y la suma de los abonos es el fondo. Quien registra no aplica; otra persona aplica con el punto del acta cerrada, el
/// acta subida como soporte y la huella de la simulación.</item>
/// <item>US12-2: aplicarla otra vez se rechaza (<c>AlreadyApplied</c>); se reversa entera y se registra otra que la corrige, con la misma acta.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase. Exige <c>EntregaVigente</c> ≥ K10.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class AsambleaTests(CentralIdentityApiFixture fx)
{
    private const string Asamblea = "/api/lending/assembly-distributions";
    private const string Cuentas = "/api/lending/accounts";
    private const string RutaDeLineas = "/api/lending/lines";
    private const string Doble = Receipts.RecaudoEnsayoDelContrato.Ruta + "/accounts"; // T0300: Recaudo real (REC) por el arnés

    private static int _documento;

    [Fact]
    public async Task US12_revalorizacion_limitada_por_el_IPC_y_prorrateada_al_fondo_aplicada_con_acta_reversada_y_corregida()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Ana"), 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Beto"), 2_000_000m);
        var ejercicio = des.Hoy.Year;

        // El IPC del ejercicio en la central, replicado a la cooperativa.
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        await LineasE2E.RegistrarAsync(http, maestro, new
        {
            code = "IPC", key = "ANUAL", validFrom = des.Hoy, referenceDate = des.Hoy, value = 0.04m, unit = "Fraccion", source = "DANE, boletín de prueba (e2e)",
        });
        await ValoresNacionalesE2E.ReplicarAsync(fx, des.Coop.TenantPublicId);

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;

            // Registra el administrador de la cooperativa.
            var registrada = await RegistrarAsync(http, admin, ejercicio, 0.05m, 9_000m, null);
            var decision = registrada.GetProperty("publicId").GetGuid();
            registrada.GetProperty("status").GetInt32().Should().Be((int)AssemblyDistributionStatus.Simulated);

            // US12-1: la simulación se limita al IPC si lo supera y se prorratea al fondo.
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            var ipc = simulacion.GetProperty("limitedByIpc").GetProperty("ipc").GetDecimal();
            simulacion.GetProperty("appliedRate").GetDecimal().Should().Be(Math.Min(0.05m, ipc));
            simulacion.GetProperty("proratedToFund").GetProperty("applied").GetBoolean().Should().BeTrue("lo calculado supera el fondo de 9.000");
            simulacion.GetProperty("totals").GetProperty("applied").GetDecimal().Should().Be(9_000m, "la suma de los abonos es el fondo aprobado");
            simulacion.GetProperty("lines").EnumerateArray().Sum(l => l.GetProperty("appliedAmount").GetDecimal()).Should().Be(9_000m);
            var huella = simulacion.GetProperty("sha256").GetString()!;

            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, decision, asamblea,
                "Decision");
            var soporte = await SoporteAsync(http, admin, decision);

            // Quien registra no aplica (FR-005); otra persona sí.
            var propia = await AplicarAsync(http, admin, decision, punto, huella, soporte);
            (await CarteraE2E.CodigoDeErrorAsync(propia)).Should().Be("Lending.Segregation.Conflict");
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
            var aplicada = await AplicarAsync(http, aplica, decision, punto, huella, soporte);
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
            var dto = await CarteraE2E.LeerAsync(aplicada);
            dto.GetProperty("status").GetInt32().Should().Be((int)AssemblyDistributionStatus.Applied);
            dto.GetProperty("appliedTotal").GetDecimal().Should().Be(9_000m);
            (await PagosE2E.MensajesAsync(fx, des, decision)).Should().Contain("RevalorizacionDeAportesAplicada");

            // US12-2: no se aplica dos veces; se reversa entera y se corrige con otra que hereda el acta.
            var otraVez = await AplicarAsync(http, aplica, decision, punto, huella, soporte);
            (await CarteraE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Lending.AssemblyDistribution.AlreadyApplied");
            var reversada = await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, $"{Asamblea}/{decision}/reverse",
                new { reason = "El fondo del acta era otro (e2e)" });
            reversada.StatusCode.Should().Be(HttpStatusCode.OK, await reversada.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(reversada)).GetProperty("status").GetInt32().Should().Be((int)AssemblyDistributionStatus.Reversed);

            var correccion = await RegistrarAsync(http, admin, ejercicio, 0.03m, 1_000_000m, decision);
            var corrige = correccion.GetProperty("publicId").GetGuid();
            correccion.GetProperty("sessionItemPublicId").GetGuid().Should().Be(punto, "la corrección hereda el acta");
            var otra = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{corrige}/simulate");
            var corregida = await AplicarAsync(http, aplica, corrige, null, otra.GetProperty("sha256").GetString()!, await SoporteAsync(http, admin, corrige));
            corregida.StatusCode.Should().Be(HttpStatusCode.OK, await corregida.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(corregida)).GetProperty("correctsPublicId").GetGuid().Should().Be(decision);
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    /// <summary>
    /// Respuesta 93 del dueño (E257): los excedentes van a ahorro y Beto no tiene cuenta de ahorro: la decisión no se bloquea, a Ana se le abona en su ahorro y
    /// el de Beto se capitaliza en sus aportes, sin abrirle ninguna cuenta.
    /// </summary>
    [Fact]
    public async Task E257_los_excedentes_a_ahorro_de_quien_no_tiene_cuenta_se_capitalizan_en_sus_aportes()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea_93");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea_93");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        var ana = await AsociadoAsync(http, admin, "Ana");
        var beto = await AsociadoAsync(http, admin, "Beto");
        await ConAportesAsync(http, admin, des, linea, ana, 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, beto, 2_000_000m);
        var ahorro = await MovimientosTests.LineaALaVistaAsync(http, admin, des.Hoy, "AHEXCED");
        await MovimientosTests.CuentaAsync(http, admin, des.Usuarios.O1.AgenciaPublicId, ahorro, ana);
        var ejercicio = des.Hoy.Year;

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;
            var registro = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Asamblea, new
            {
                kind = "SurplusDistribution", year = ejercicio, fundAmount = 30_000m, percent = 0.01m, @base = "YearEndBalance", destination = "Savings",
                targetDepositLineCode = "AHEXCED", reason = "Asamblea general ordinaria (e2e de la respuesta 93)",
            });
            registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
            var decision = (await CarteraE2E.LeerAsync(registro)).GetProperty("publicId").GetGuid();
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            simulacion.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("se capitaliza en sus aportes"));
            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, decision, asamblea, "Decision");
            var soporte = await SoporteAsync(http, admin, decision);
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
            var aplicada = await AplicarAsync(http, aplica, decision, punto, simulacion.GetProperty("sha256").GetString()!, soporte);
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
            var lineas = (await CarteraE2E.LeerAsync(aplicada)).GetProperty("lines").EnumerateArray().ToList();
            lineas.Should().HaveCount(2);
            lineas.Count(l => l.GetProperty("destinationAccountNumber").ValueKind == JsonValueKind.Null).Should().Be(1, "Beto no tiene ahorro: capitaliza");
            var cuentasDeBeto = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.MemberAccounts.AsNoTracking()
                .CountAsync(a => a.Nature == LineNature.Deposit && db.People.Any(p => p.PublicId == beto && p.Id == a.HolderPersonId)));
            cuentasDeBeto.Should().Be(0, "no se le abrió ninguna cuenta");
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    /// <summary>
    /// Respuesta 92 del dueño (E258): los excedentes se pagan por orden de giro y Tesorería rechaza la de Ana: no hay saldo que reintegrar (sigue por pagar a su favor),
    /// la orden se regira por HTTP —una sola vez— y el regiro se gira.
    /// </summary>
    [Fact]
    public async Task E258_el_giro_rechazado_de_un_excedente_se_regira_por_HTTP()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea_92");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea_92");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Ana"), 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Beto"), 2_000_000m);
        var ejercicio = des.Hoy.Year;

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;
            var registro = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Asamblea, new
            {
                kind = "SurplusDistribution", year = ejercicio, fundAmount = 30_000m, percent = 0.01m, @base = "YearEndBalance", destination = "Payment",
                reason = "Asamblea general ordinaria (e2e de la respuesta 92)",
            });
            registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
            var decision = (await CarteraE2E.LeerAsync(registro)).GetProperty("publicId").GetGuid();
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, decision, asamblea, "Decision");
            var soporte = await SoporteAsync(http, admin, decision);
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
            var aplicada = await AplicarAsync(http, aplica, decision, punto, simulacion.GetProperty("sha256").GetString()!, soporte);
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());

            var ordenes = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.AccountPayoutOrders.AsNoTracking()
                .Where(o => o.Purpose == PayoutPurpose.SurplusPayment).OrderBy(o => o.Id).Select(o => new { o.PublicId, o.Amount }).ToListAsync());
            ordenes.Should().HaveCount(2);
            var rechazo = await DesembolsoE2E.ResultadoDelGiroAsync(fx, http, des, ordenes[0].PublicId, "Rejected", ordenes[0].Amount);
            await DesembolsoE2E.ConsumirAsync(fx, des, rechazo);

            var ruta = $"{Asamblea}/{decision}/orders/{ordenes[0].PublicId}/reissue";
            var regiro = await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, ruta, new { reason = "Cuenta bancaria corregida (e2e)" });
            regiro.StatusCode.Should().Be(HttpStatusCode.OK, await regiro.Content.ReadAsStringAsync());
            var nueva = await CarteraE2E.LeerAsync(regiro);
            nueva.GetProperty("amount").GetDecimal().Should().Be(ordenes[0].Amount);
            (await CarteraE2E.CodigoDeErrorAsync(await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, ruta, new { reason = "Otra vez (e2e)" })))
                .Should().Be("Lending.PayoutOrder.NotReissuable");
            var sinRechazo = await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, $"{Asamblea}/{decision}/orders/{ordenes[1].PublicId}/reissue",
                new { reason = "No aplica (e2e)" });
            (await CarteraE2E.CodigoDeErrorAsync(sinRechazo)).Should().Be("Lending.PayoutOrder.NotReissuable");

            var giro = await DesembolsoE2E.ResultadoDelGiroAsync(fx, http, des, nueva.GetProperty("orderPublicId").GetGuid(), "Disbursed", ordenes[0].Amount);
            await DesembolsoE2E.ConsumirAsync(fx, des, giro);
            var estado = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.AccountPayoutOrders.AsNoTracking()
                .Where(o => o.PublicId == nueva.GetProperty("orderPublicId").GetGuid()).Select(o => o.Status).SingleAsync());
            estado.Should().Be(DisbursementOrderStatus.Disbursed);
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    /// <summary>
    /// Respuesta 91 B del dueño (E259): Ana se retiró durante el ejercicio (retiro liquidado) y Beto sigue. Los excedentes a aportes la incluyen a prorrata del
    /// tiempo que fue asociada y a ella se le GIRA su parte (orden de giro, nada entra a sus aportes); Beto capitaliza. Lo girado ya salió: no se reversa entera.
    /// </summary>
    [Fact]
    public async Task E259_el_asociado_retirado_en_el_ejercicio_recibe_su_parte_por_giro()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea_91");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea_91");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        var ana = await AsociadoAsync(http, admin, "Ana");
        await ConAportesAsync(http, admin, des, linea, ana, 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Beto"), 2_000_000m);
        var ejercicio = des.Hoy.Year;

        // El retiro de Ana, liquidado, con fecha efectiva dentro del ejercicio.
        var retiro = new DateOnly(ejercicio, 12, 31).AddDays(-1) > des.Hoy ? des.Hoy.AddDays(1) : des.Hoy;
        await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, async db =>
        {
            var persona = await db.People.AsNoTracking().Where(p => p.PublicId == ana).Select(p => p.Id).SingleAsync();
            var cuenta = await db.MemberAccounts.AsNoTracking().Where(a => a.HolderPersonId == persona && a.Nature == LineNature.Contribution).Select(a => a.Id).SingleAsync();
            var usuario = await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
            db.MembershipWithdrawals.Add(new IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawal
            {
                Number = "RTA-E2E91", PersonId = persona, ContributionAccountId = cuenta, Cause = MembershipWithdrawalCause.Voluntary, FiledOn = des.Hoy,
                FiledByUserId = usuario, Status = MembershipWithdrawalStatus.Settled, EffectiveDate = retiro, SettlementVersion = 1,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;
            var registro = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Asamblea, new
            {
                kind = "SurplusDistribution", year = ejercicio, fundAmount = 100_000m, value = 100_000m, @base = "YearEndBalance", destination = "Contributions",
                reason = "Asamblea general ordinaria (e2e de la respuesta 91)",
            });
            registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
            var decision = (await CarteraE2E.LeerAsync(registro)).GetProperty("publicId").GetGuid();
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            simulacion.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("se retiraron durante el ejercicio"));
            var lineasSimuladas = simulacion.GetProperty("lines").EnumerateArray().ToList();
            lineasSimuladas.Should().HaveCount(2);
            lineasSimuladas.Count(l => l.GetProperty("withdrawnDuringYear").GetBoolean()).Should().Be(1);
            simulacion.GetProperty("totals").GetProperty("applied").GetDecimal().Should().Be(100_000m);

            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, decision, asamblea, "Decision");
            var soporte = await SoporteAsync(http, admin, decision);
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
            var aplicada = await AplicarAsync(http, aplica, decision, punto, simulacion.GetProperty("sha256").GetString()!, soporte);
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
            var lineas = (await CarteraE2E.LeerAsync(aplicada)).GetProperty("lines").EnumerateArray().ToList();
            var deAna = lineas.Single(l => l.GetProperty("withdrawnDuringYear").GetBoolean());
            deAna.GetProperty("payoutOrderNumber").ValueKind.Should().Be(JsonValueKind.String, "lo de quien se retiró se gira");
            lineas.Single(l => !l.GetProperty("withdrawnDuringYear").GetBoolean()).GetProperty("payoutOrderNumber").ValueKind.Should().Be(JsonValueKind.Null);

            var ordenes = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.AccountPayoutOrders.AsNoTracking()
                .Where(o => o.Purpose == PayoutPurpose.SurplusPayment).Select(o => o.Amount).ToListAsync());
            ordenes.Should().ContainSingle().Which.Should().Be(deAna.GetProperty("appliedAmount").GetDecimal());
            var enSusAportes = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.MemberAccountTransactions.AsNoTracking()
                .Where(t => t.Kind == MemberAccountTransactionKind.AssemblyCredit
                            && db.MemberAccounts.Any(c => c.Id == t.MemberAccountId && db.People.Any(p => p.PublicId == ana && p.Id == c.HolderPersonId))).CountAsync());
            enSusAportes.Should().Be(0, "nada entró a las cuentas de quien se retiró");

            var reversa = await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, $"{Asamblea}/{decision}/reverse", new { reason = "No debe poder (e2e)" });
            (await CarteraE2E.CodigoDeErrorAsync(reversa)).Should().Be("Lending.AssemblyDistribution.PaymentNotReversible");
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    /// <summary>
    /// Respuesta 89 B del dueño (E260): la cooperativa configura el concepto de retención y la tarifa de los retornos (20 %); los excedentes a aportes de Ana y Beto
    /// se abonan netos (la retención queda por pagar), la decisión muestra lo retenido y el reverso entero devuelve todo. Y los excedentes pagados giran el neto.
    /// </summary>
    [Fact]
    public async Task E260_la_retencion_sobre_los_retornos_se_practica_con_el_concepto_configurado_y_se_reversa()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea_89");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea_89");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        var ana = await AsociadoAsync(http, admin, "Ana");
        var beto = await AsociadoAsync(http, admin, "Beto");
        await ConAportesAsync(http, admin, des, linea, ana, 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, beto, 2_000_000m);
        var ejercicio = des.Hoy.Year;

        foreach (var (clave, valor) in new[] { ("Asamblea.Retencion.Concepto", "RETEFTE-RETORNOS"), ("Asamblea.Retencion.TarifaExcedentes", "0.2") })
            await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/parameters/{clave}/versions",
                new { scopeKind = 0, value = valor, validFrom = des.Hoy, reason = "Retención sobre los retornos (e2e de la respuesta 89)" });

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);

            // 1. A aportes: se abonan 8.000 y 16.000 netos; la retención de 6.000 queda por pagar; el reverso entero devuelve todo.
            var aAportes = await DecisionAsync(http, admin, ejercicio, "Contributions");
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{aAportes}/simulate");
            simulacion.GetProperty("totals").GetProperty("withheld").GetDecimal().Should().Be(6_000m);
            simulacion.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("RETEFTE-RETORNOS"));
            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, aAportes, asamblea, "Decision");
            var aplicada = await AplicarAsync(http, aplica, aAportes, punto, simulacion.GetProperty("sha256").GetString()!, await SoporteAsync(http, admin, aAportes));
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
            var dto = await CarteraE2E.LeerAsync(aplicada);
            dto.GetProperty("appliedTotal").GetDecimal().Should().Be(30_000m);
            dto.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("withheldAmount").GetDecimal()).Order().Should().Equal(2_000m, 4_000m);
            var abonado = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.MemberAccountTransactions.AsNoTracking()
                .Where(t => t.Kind == MemberAccountTransactionKind.AssemblyCredit).SumAsync(t => t.Amount));
            abonado.Should().Be(24_000m, "30.000 aplicados menos 6.000 de retención");
            var retenidas = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.AssemblyDistributionLines.AsNoTracking()
                .Where(l => l.WithheldAmount > 0m).Select(l => l.WithholdingConcept).Distinct().ToListAsync());
            retenidas.Should().Equal("RETEFTE-RETORNOS");

            var reversada = await CarteraE2E.ConClaveAsync(http, aplica, HttpMethod.Post, $"{Asamblea}/{aAportes}/reverse", new { reason = "El fondo era otro (e2e)" });
            reversada.StatusCode.Should().Be(HttpStatusCode.OK, await reversada.Content.ReadAsStringAsync());
            var saldoTrasReverso = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.MemberAccountTransactions.AsNoTracking()
                .Where(t => t.Concept == MemberAccountConcept.ContributionOrdinary).SumAsync(t => t.Amount));
            saldoTrasReverso.Should().Be(0m, "el reverso devuelve lo abonado");
            (await PagosE2E.MensajesAsync(fx, des, aAportes)).Should().Contain("ExcedentesDistribuidos");
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    [Fact]
    public async Task E260_los_excedentes_pagados_giran_el_neto_de_la_retencion_por_HTTP()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea_89b");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea_89b");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Ana"), 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, await AsociadoAsync(http, admin, "Beto"), 2_000_000m);
        var ejercicio = des.Hoy.Year;
        foreach (var (clave, valor) in new[] { ("Asamblea.Retencion.Concepto", "RETEFTE-RETORNOS"), ("Asamblea.Retencion.TarifaExcedentes", "0.2") })
            await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/parameters/{clave}/versions",
                new { scopeKind = 0, value = valor, validFrom = des.Hoy, reason = "Retención sobre los retornos (e2e de la respuesta 89)" });

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;
            var decision = await DecisionAsync(http, admin, ejercicio, "Payment");
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, decision, asamblea, "Decision");
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
            var aplicada = await AplicarAsync(http, aplica, decision, punto, simulacion.GetProperty("sha256").GetString()!, await SoporteAsync(http, admin, decision));
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
            var ordenes = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.AccountPayoutOrders.AsNoTracking()
                .Where(o => o.Purpose == PayoutPurpose.SurplusPayment).OrderBy(o => o.Id).Select(o => o.Amount).ToListAsync());
            ordenes.Should().Equal(8_000m, 16_000m);
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    /// <summary>
    /// Respuesta 90 C del dueño (E261): la asamblea define la base de los retornos (uso de servicios) y la cooperativa la sube por la plantilla
    /// <c>lending.assembly-return-bases</c>: se descarga, se revisa (un documento inexistente es error de fila y columna y no se guarda nada), se aplica, y la
    /// simulación y la aplicación reparten por esa base y no por los saldos.
    /// </summary>
    [Fact]
    public async Task E261_la_base_de_los_retornos_sube_por_plantilla_por_HTTP_y_reparte_por_ella()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_asamblea_90");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "asamblea_90");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var linea = await LineaDeAportesAsync(http, admin, des.Hoy);
        var ana = await AsociadoAsync(http, admin, "Ana");
        var beto = await AsociadoAsync(http, admin, "Beto");
        await ConAportesAsync(http, admin, des, linea, ana, 1_000_000m);
        await ConAportesAsync(http, admin, des, linea, beto, 2_000_000m);
        var documentos = await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.People.AsNoTracking()
            .Where(p => p.PublicId == ana || p.PublicId == beto).ToDictionaryAsync(p => p.PublicId, p => p.TaxId!));
        var ejercicio = des.Hoy.Year;
        var ruta = $"{Asamblea}/return-bases";

        var reloj = new ConductorDelDespachador(fx).Reloj;
        var original = reloj.Desfase;
        try
        {
            var asamblea = new DateOnly(ejercicio + 1, 3, 20);
            reloj.Desfase += asamblea.ToDateTime(new TimeOnly(10, 0)) - reloj.AhoraLocal.DateTime;
            var registro = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Asamblea, new
            {
                kind = "SurplusDistribution", year = ejercicio, fundAmount = 100_000m, value = 100_000m, @base = "UploadedBase", destination = "Contributions",
                reason = "Asamblea general ordinaria (e2e de la respuesta 90)",
            });
            registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
            var dto = await CarteraE2E.LeerAsync(registro);
            var decision = dto.GetProperty("publicId").GetGuid();
            var numero = dto.GetProperty("number").GetString()!;

            // La plantilla se descarga vacía y sin archivo nadie participa.
            using var plantilla = await http.SendAsync(Con(admin, new HttpRequestMessage(HttpMethod.Get, $"{ruta}/template.xlsx")));
            plantilla.StatusCode.Should().Be(HttpStatusCode.OK);
            var vacia = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            vacia.GetProperty("lines").GetArrayLength().Should().Be(0);

            // Un documento inexistente: error de fila y columna, revisar responde 200 y aplicar, 422; no se guarda nada.
            var malo = Libro((numero, documentos[ana], "3000000", "Uso de servicios 2026"), (numero, "999999", "100", null));
            using var revision = await ImportarAsync(http, admin, ruta, "review", malo);
            revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
            var cuerpo = await CarteraE2E.LeerAsync(revision);
            cuerpo.GetProperty("valid").GetBoolean().Should().BeFalse();
            cuerpo.GetProperty("errors").EnumerateArray().Should().Contain(e => e.GetProperty("row").GetInt32() == 3 && e.GetProperty("column").GetString() == "persona");
            using var rechazado = await ImportarAsync(http, admin, ruta, "apply", malo);
            rechazado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CausacionE2E.EnLaBaseAsync(fx, des.Coop.TenantPublicId, db => db.AssemblyReturnBases.AsNoTracking().CountAsync())).Should().Be(0);

            // Bien: se aplica y la simulación reparte por la base subida (75 % y 25 %), no por los saldos (33 % y 67 %).
            var bueno = Libro((numero, documentos[ana], "3000000", "Uso de servicios 2026"), (numero, documentos[beto], "1000000", "Uso de servicios 2026"));
            using var revisado = await ImportarAsync(http, admin, ruta, "review", bueno);
            (await CarteraE2E.LeerAsync(revisado)).GetProperty("valid").GetBoolean().Should().BeTrue();
            using var aplicadoLibro = await ImportarAsync(http, admin, ruta, "apply", bueno);
            aplicadoLibro.StatusCode.Should().Be(HttpStatusCode.OK, await aplicadoLibro.Content.ReadAsStringAsync());
            var simulacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Asamblea}/{decision}/simulate");
            simulacion.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("appliedAmount").GetDecimal()).Order().Should().Equal(25_000m, 75_000m);
            simulacion.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("baseAmount").GetDecimal()).Order().Should().Equal(1_000_000m, 3_000_000m);

            var punto = await ActasE2E.DecididoAsync(http, des, des.Originacion.Consejo, LendingDecisionSubjects.AssemblyDistribution, decision, asamblea, "Decision");
            var aplica = await UsuariosDeCartera.IngresarAsync(http, des.Usuarios.Todos["administrador"]);
            var aplicada = await AplicarAsync(http, aplica, decision, punto, simulacion.GetProperty("sha256").GetString()!, await SoporteAsync(http, admin, decision));
            aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());

            // Aplicada, ya no admite otra carga.
            using var tarde = await ImportarAsync(http, admin, ruta, "apply", bueno);
            tarde.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }
        finally
        {
            reloj.Desfase = original;
        }
    }

    private static HttpRequestMessage Con(string token, HttpRequestMessage peticion)
    {
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return peticion;
    }

    /// <summary>El libro de la plantilla de la base de los retornos: decisión, persona, base y concepto.</summary>
    private static byte[] Libro(params (string Decision, string Persona, string Base, string? Concepto)[] filas)
    {
        using var libro = new ClosedXML.Excel.XLWorkbook();
        var hoja = libro.Worksheets.Add("Datos");
        string[] encabezados = ["decision", "persona", "base", "concepto"];
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        for (var f = 0; f < filas.Length; f++)
        {
            hoja.Cell(f + 2, 1).SetValue(filas[f].Decision);
            hoja.Cell(f + 2, 2).SetValue(filas[f].Persona);
            hoja.Cell(f + 2, 3).SetValue(filas[f].Base);
            if (filas[f].Concepto is { } concepto) hoja.Cell(f + 2, 4).SetValue(concepto);
        }
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    private static Task<HttpResponseMessage> ImportarAsync(HttpClient http, string token, string ruta, string modo, byte[] contenido)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        formulario.Add(archivo, "file", "base-de-retornos.xlsx");
        formulario.Add(new StringContent("Base de los retornos subida por plantilla (e2e)"), "reason");
        return http.SendAsync(CarteraE2E.ConClave(Con(token, new HttpRequestMessage(HttpMethod.Post, $"{ruta}/import?mode={modo}") { Content = formulario })));
    }

    // ------------------------------------------------------------------------------------------------------------ apoyo --

    /// <summary>Registra una distribución de excedentes del 1 % sobre el saldo al cierre con el destino dado y devuelve su identificador.</summary>
    private static async Task<Guid> DecisionAsync(HttpClient http, string token, int ejercicio, string destino)
    {
        var registro = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Asamblea, new
        {
            kind = "SurplusDistribution", year = ejercicio, fundAmount = 30_000m, percent = 0.01m, @base = "YearEndBalance", destination = destino,
            reason = "Asamblea general ordinaria (e2e de la respuesta 89)",
        });
        registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
        return (await CarteraE2E.LeerAsync(registro)).GetProperty("publicId").GetGuid();
    }

    private static async Task<JsonElement> RegistrarAsync(HttpClient http, string token, int ejercicio, decimal porcentaje, decimal fondo, Guid? corrige)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Asamblea, new
        {
            kind = "Revaluation", year = ejercicio, fundAmount = fondo, percent = porcentaje, @base = "AverageBalance", correctsPublicId = corrige,
            reason = "Asamblea general ordinaria (e2e)",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"registrar la decisión: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    private static Task<HttpResponseMessage> AplicarAsync(HttpClient http, string token, Guid decision, Guid? punto, string huella, Guid soporte) =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Asamblea}/{decision}/apply", new
        {
            sessionItemPublicId = punto, expectedPreviewSha256 = huella, supportAttachmentPublicIds = new[] { soporte }, reason = "Según el acta (e2e)",
        });

    private static async Task<Guid> LineaDeAportesAsync(HttpClient http, string admin, DateOnly hoy)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{RutaDeLineas}/contribution", new
        {
            code = "APASAMB", name = "Aportes sociales (asamblea e2e)", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Estatutos (e2e de la asamblea)", minimumNonReducible = new { kind = "FixedAmount", amount = 100_000m },
                installment = new { kind = "FixedAmount", amount = 50_000m, periodicity = "Monthly", dueDay = 5 }, allowsExtraordinary = true,
                refundTermDays = 60,
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea de aportes: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    private static async Task<Guid> AsociadoAsync(HttpClient http, string admin, string nombre)
    {
        var documento = (6_900_000_000L + Interlocked.Increment(ref _documento) * 10L).ToString(CultureInfo.InvariantCulture);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new
            {
                idType = "C", taxId = documento, firstName = nombre, lastName = "Asamblea", personType = "01", dateOfBirth = new DateOnly(1980, 6, 1),
                mobile = "3001234560", email = $"{nombre.ToLowerInvariant()}.{documento}@asamblea.test",
            },
            associate = new { joinDate = new DateOnly(2020, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var persona = (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "GeneralTreatment");
        return persona;
    }

    private static async Task ConAportesAsync(HttpClient http, string admin, DesembolsoE2E.Escenario des, Guid linea, Guid persona, decimal aportes)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Cuentas, new
        {
            linePublicId = linea, holderPersonPublicId = persona, officePublicId = des.Usuarios.O1.AgenciaPublicId, handlingMode = "Indistinct",
            parties = Array.Empty<object>(), beneficiaries = Array.Empty<object>(), markGmfExempt = false,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"abrir la cuenta de aportes: «{await resp.Content.ReadAsStringAsync()}»");
        var cuenta = (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
        var pago = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Doble}/deposits", new
        {
            deposits = new[]
            {
                new
                {
                    idempotencyKey = $"REC:{Guid.NewGuid():N}", accountPublicId = cuenta, modality = "Free", amount = aportes, valueDate = des.Hoy,
                    meansClass = "Cash", depositorPublicId = persona,
                    receipt = new { number = "REC-ASAMBLEA", date = des.Hoy, officePublicId = (Guid?)null, meansReference = (string?)null, documentKind = "RC" },
                },
            },
        });
        pago.StatusCode.Should().Be(HttpStatusCode.OK, await pago.Content.ReadAsStringAsync());
    }

    /// <summary>Sube el acta firmada como soporte de la decisión, como el navegador: pedir, enviar al almacén local, confirmar.</summary>
    private static async Task<Guid> SoporteAsync(HttpClient http, string token, Guid decision)
    {
        var contenido = AdjuntosE2E.Pdf(4_096);
        using var pedido = await AdjuntosE2E.PedirSubidaAsync(http, token, decision, contenido, "acta-asamblea.pdf", ownerEntityType: "LendingAssemblySupport");
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var autorizacion = await CarteraE2E.LeerAsync(pedido);
        var adjunto = autorizacion.GetProperty("attachmentPublicId").GetGuid();
        var subida = autorizacion.GetProperty("upload");
        using var formulario = new MultipartFormDataContent();
        foreach (var campo in subida.GetProperty("fields").EnumerateObject())
            formulario.Add(new StringContent(campo.Value.GetString()!), campo.Name);
        formulario.Add(new ByteArrayContent(contenido), subida.GetProperty("fileField").GetString()!, "acta-asamblea.pdf");
        var url = subida.GetProperty("url").GetString()!;
        var absoluta = Uri.TryCreate(url, UriKind.Absolute, out var destino);
        using var almacen = await http.PostAsync(absoluta ? destino!.PathAndQuery : url, formulario);
        almacen.IsSuccessStatusCode.Should().BeTrue($"el almacén recibe el acta: {(int)almacen.StatusCode}");
        using var confirmacion = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/attachments/{adjunto}/confirm", null);
        confirmacion.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacion.Content.ReadAsStringAsync());
        return adjunto;
    }
}
