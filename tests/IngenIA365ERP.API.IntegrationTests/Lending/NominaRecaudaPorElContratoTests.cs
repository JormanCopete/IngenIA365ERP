using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.NominaYCarteraE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0528 (feature 013, US5; DT §2.25, nombre fijo; FR-088, FR-112, T92, C32, C55, I-4; colección «Cartera e2e», los dos motores por
/// <c>DB_PROVIDER</c>): la nómina propia recauda por el contrato real, por HTTP, en una cooperativa con Cartera activada y la nómina preparada
/// (<see cref="NominaYCarteraE2E"/>).
/// <list type="bullet">
/// <item>la ordinaria trae la cuota de Cartera de <c>CuotasDeNominaAsync</c> como descuento; <b>I-4</b>: una aprobación que falla después de aplicar
/// (el NM no se arma: <c>DESC_CARTERA</c> sin cuentas) no deja nada en Cartera, y la auditoría rotula el intento «revertido con el recaudador»;
/// aprobada, el lote se aplica antes del NM —mientras el recaudador de prueba paga los mismos créditos, sin interbloqueo—; la cuota ya pagada por
/// otro medio vuelve como excedente y la línea baja (<c>Payroll.LoanDeductionNotApplied</c>); el empleado en licencia no remunerada queda como no
/// descontado (<c>LND_InstallmentNonDeductions</c> con origen <c>Contract</c> y la alerta al jefe de cartera);
/// <c>GET /api/payroll/runs/{runId}/loan-deductions</c> lo muestra;</item>
/// <item>el <c>jefe</c> no reversa un pago de Nómina (<c>Lending.Reversal.OwnedByCollector</c>, US10-6); reversar la ordinaria lo reversa por el
/// contrato;</item>
/// <item>la definitiva: un crédito nuevo entre la propuesta y la aprobación → <c>Payroll.Settlement.LoanProposalOutdated</c>; recalculada, aprueba y
/// aplica por el contrato; reversarla reversa también los recaudos.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class NominaRecaudaPorElContratoTests(CentralIdentityApiFixture fx)
{
    private const string Terminaciones = "/api/payroll/settlements/terminations";

    [Fact]
    public async Task La_ordinaria_aplica_su_lote_por_el_contrato_antes_del_NM_y_lo_reversa()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var plan = await PlanAsync(http, esc, "ORD");
        await SinCuentasDelConceptoAsync(http, esc.Admin, ConceptoDeCartera);
        var (personaA, _, creditoA) = await EmpleadoConCreditoAsync(http, esc, plan, "Descontada");
        var (personaB, _, creditoB) = await EmpleadoConCreditoAsync(http, esc, plan, "YaPagada");
        var (_, empleadoC, creditoC) = await EmpleadoConCreditoAsync(http, esc, plan, "Licencia");

        var cuota = await CuotaAsync(http, u.Jefe.Token, creditoA);
        var desde = new DateOnly(cuota.Vence.Year, cuota.Vence.Month, 1);
        var hasta = desde.AddMonths(1).AddDays(-1);
        var periodo = await PeriodoAsync(http, esc, plan, desde, hasta);
        await NominaE2E.RegistrarNovedadAsync(http, esc.Admin, periodo, new
        {
            employeePublicId = empleadoC, conceptCode = "LIC_NO_REMUNERADA", quantity = 30,
            startDate = desde.ToDateTime(TimeOnly.MinValue), endDate = hasta.ToDateTime(TimeOnly.MinValue),
        });

        using (RelojEn(fx, hasta.AddDays(1)))
        {
            // La cuota del período de cada obligación por nómina propia, calculada.
            var corrida = await CalcularAsync(http, esc, periodo);
            var calculados = await DescuentosAsync(http, esc, corrida);
            calculados.Select(d => d.GetProperty("obligationPublicId").GetGuid()).Should().BeEquivalentTo([creditoA, creditoB, creditoC]);
            var deA = calculados.Single(d => d.GetProperty("obligationPublicId").GetGuid() == creditoA);
            // Lo exigible de la cuota según la reconstrucción (la causación real del período, no el total teórico del plan).
            deA.GetProperty("expected").GetDecimal().Should().BeApproximately(cuota.Total, 0.02m * cuota.Total);
            deA.GetProperty("deducted").GetDecimal().Should().Be(deA.GetProperty("expected").GetDecimal());
            var deC = calculados.Single(d => d.GetProperty("obligationPublicId").GetGuid() == creditoC);
            deC.GetProperty("notDeducted").GetDecimal().Should().BePositive("en licencia no remunerada no hay de dónde descontar");

            // I-4: el NM no se arma (DESC_CARTERA sin cuentas) después de aplicar el lote: nada queda en Cartera.
            var fallida = await AprobarAsync(http, esc, corrida);
            fallida.IsSuccessStatusCode.Should().BeFalse("sin cuentas para DESC_CARTERA el comprobante no se arma");
            foreach (var c in new[] { creditoA, creditoB, creditoC })
                (await OperacionesAsync(fx, esc.Desembolso, c)).Should().NotContain(o => o.Clase == LoanOperationKind.Payment,
                    "la transacción de Nómina se revirtió con la aplicación de Cartera (I-4)");
            (await DesembolsoE2E.EnLaBaseAsync(fx, esc.Desembolso, db => db.InstallmentNonDeductions.AsNoTracking().CountAsync())).Should().Be(0);
            var claveDeA = $"NOM:{corrida:N}:{creditoA:N}";
            await AuditoriaRevertidaAsync(http, esc, claveDeA);

            // Con la cuenta, la aprobación aplica; la de B ya la pagó el recaudador de prueba: vuelve como excedente.
            await CuentasDelConceptoAsync(http, esc.Admin, ConceptoDeCartera);
            var ciclo = (await ObligacionAsync(http, u, personaB, creditoB)).GetProperty("cycleInstallment").GetDecimal();
            await AplicadoAsync(http, u, Pago(creditoB, personaB, "CycleInstallment", ciclo, hasta));
            var aprobacion = AprobarAsync(http, esc, corrida);
            var otroRecaudador = AplicarUnoAsync(http, u, Pago(creditoA, personaA, "FreeAmount", 10_000m, hasta));
            await Task.WhenAll(aprobacion, otroRecaudador);
            var aprobada = await aprobacion;
            aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
            Resultado(await otroRecaudador).Should().Be(Aplicado, "el lote de Nómina y el del recaudador de prueba no se interbloquean (§3.11)");
            (await aprobada.Content.ReadAsStringAsync()).Should().Contain("Payroll.LoanDeductionNotApplied");

            var aplicados = await DescuentosAsync(http, esc, corrida);
            var a = aplicados.Single(d => d.GetProperty("obligationPublicId").GetGuid() == creditoA);
            Estado(a).Should().Be("Applied");
            a.GetProperty("carteraReceiptPublicId").ValueKind.Should().Be(JsonValueKind.String);
            Estado(aplicados.Single(d => d.GetProperty("obligationPublicId").GetGuid() == creditoB)).Should().BeOneOf("PartiallyApplied", "Rejected");
            Estado(aplicados.Single(d => d.GetProperty("obligationPublicId").GetGuid() == creditoC)).Should().Be("NotApplied");

            var pagoDeNomina = (await OperacionesAsync(fx, esc.Desembolso, creditoA)).Single(o => o.Clave == claveDeA);
            pagoDeNomina.Clase.Should().Be(LoanOperationKind.Payment);
            var noDescontado = await DesembolsoE2E.EnLaBaseAsync(fx, esc.Desembolso, db => (from n in db.InstallmentNonDeductions.AsNoTracking()
                                                                                            join l in db.Loans.AsNoTracking() on n.LoanId equals l.Id
                                                                                            where l.PublicId == creditoC
                                                                                            select new { n.Origin, n.CollectorCode }).ToListAsync());
            noDescontado.Should().ContainSingle().Which.Origin.Should().Be(NonDeductionOrigin.Contract);
            (await DesembolsoE2E.EnLaBaseAsync(fx, esc.Desembolso, db => db.Alerts.AsNoTracking().CountAsync(x => x.TypeCode == "Cartera.CuotaNoDescontadaPorNomina")))
                .Should().BePositive("la alerta al jefe de cartera (FR-112)");

            // El jefe de cartera no reversa un pago de Nómina: sólo Nómina lo reversa (T31, C37, US10-6).
            var previa = await CarteraE2E.EnviarAsync(http, u.Jefe.Token, HttpMethod.Post, $"{DesembolsoE2E.Creditos}/{creditoA}/operations/{pagoDeNomina.PublicId}/reversal-preview",
                new { reason = "Intento del jefe (e2e)" });
            (await InventarioE2E.FallaAsync(previa, "Lending.Reversal.OwnedByCollector")).GetProperty("data").GetProperty("collectorClass").GetString().Should().Be("Payroll");
            var reverso = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{DesembolsoE2E.Creditos}/{creditoA}/operations/{pagoDeNomina.PublicId}/reverse",
                new { reason = "Intento del jefe (e2e)", expectedPreviewSha256 = "x" });
            await InventarioE2E.FallaAsync(reverso, "Lending.Reversal.OwnedByCollector");

            // Reversar la ordinaria la reversa en Cartera por el contrato (G3).
            var reversada = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, $"/api/payroll/runs/{corrida}/reverse", new { reason = "Se liquidó con el salario errado (e2e)" });
            reversada.IsSuccessStatusCode.Should().BeTrue(await reversada.Content.ReadAsStringAsync());
            (await OperacionesAsync(fx, esc.Desembolso, creditoA)).Single(o => o.Clave == claveDeA).ReversadaPor.Should().NotBeNull();
            Estado((await DescuentosAsync(http, esc, corrida)).Single(d => d.GetProperty("obligationPublicId").GetGuid() == creditoA)).Should().Be("Reversed");
        }
    }

    [Fact]
    public async Task La_ordinaria_aprobada_antes_del_fin_del_periodo_recauda_con_la_fecha_valor_del_fin_del_periodo()
    {
        // A9 (respuesta del dueño, 2026-10-14): la fecha valor de Nómina la fija la spec (el fin del período en la ordinaria) y se aplica aunque sea
        // futura. Hasta ese día el contrato la rechazaba (Lending.Payment.ValueDateOutOfRange) y la ordinaria se aprobaba sin recaudar.
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var plan = await PlanAsync(http, esc, "ANT");
        await CuentasDelConceptoAsync(http, esc.Admin, ConceptoDeCartera);
        var (_, _, credito) = await EmpleadoConCreditoAsync(http, esc, plan, "Anticipada");
        var cuota = await CuotaAsync(http, esc.Usuarios.Jefe.Token, credito);
        var desde = new DateOnly(cuota.Vence.Year, cuota.Vence.Month, 1);
        var hasta = desde.AddMonths(1).AddDays(-1);
        var periodo = await PeriodoAsync(http, esc, plan, desde, hasta);

        using (RelojEn(fx, hasta.AddDays(-3)))
        {
            var corrida = await CalcularAsync(http, esc, periodo);
            var aprobada = await AprobarAsync(http, esc, corrida);
            aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
            (await aprobada.Content.ReadAsStringAsync()).Should().NotContain("Payroll.LoanDeductionNotApplied", "Cartera tomó la cuota");

            Estado((await DescuentosAsync(http, esc, corrida)).Single(d => d.GetProperty("obligationPublicId").GetGuid() == credito)).Should().Be("Applied");
            var pago = (await OperacionesAsync(fx, esc.Desembolso, credito)).Single(o => o.Clave == $"NOM:{corrida:N}:{credito:N}");
            pago.Clase.Should().Be(LoanOperationKind.Payment);
            var fechaValor = await DesembolsoE2E.EnLaBaseAsync(fx, esc.Desembolso,
                db => db.LoanOperations.AsNoTracking().Where(o => o.PublicId == pago.PublicId).Select(o => o.ValueDate).SingleAsync());
            fechaValor.Should().Be(hasta, "la fecha valor de la ordinaria es el fin del período, tres días adelante de hoy");
        }
    }

    [Fact]
    public async Task La_definitiva_con_un_credito_nuevo_entre_la_propuesta_y_la_aprobacion_se_rechaza_y_recalculada_recauda()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var plan = await PlanAsync(http, esc, "DEF");
        await CuentasDelConceptoAsync(http, esc.Admin, ConceptoDeCartera);
        var (persona, empleado, credito) = await EmpleadoConCreditoAsync(http, esc, plan, "Retirada", monto: 800_000m, afiliado: true);
        var hoy = esc.Hoy;
        var desde = new DateOnly(hoy.Year, hoy.Month, 1);
        await PeriodoAsync(http, esc, plan, desde, desde.AddMonths(1).AddDays(-1));

        var registro = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, Terminaciones, new
        {
            employeePublicId = empleado, terminationDate = hoy, reasonCode = "RENUNCIA", contractType = "Indefinite", notes = "Retiro con crédito de Cartera (e2e)",
        });
        registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
        var corrida = (await NominaE2E.LeerAsync(registro)).GetProperty("runPublicId").GetGuid();
        var propuesta = await NominaE2E.GetAsync(http, esc.Admin, $"{Terminaciones}/{corrida}/deductions");
        // Los ítems de la propuesta llevan su propio PublicId (el del descuento): se reconocen por el número del crédito.
        var numero = await DesembolsoE2E.EnLaBaseAsync(fx, esc.Desembolso, db => db.Loans.AsNoTracking().Where(l => l.PublicId == credito).Select(l => l.Number).SingleAsync());
        DeCartera(propuesta).Should().ContainSingle().Which.Should().Contain(numero);

        // Un crédito nuevo de la misma persona entre la propuesta y la aprobación: la propuesta quedó vieja (R104).
        var otro = await CreditoPorNominaAsync(http, esc, persona, 600_000m);
        var vieja = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, $"{Terminaciones}/{corrida}/approve", new { confirm = true, confirmWithoutSegregation = true });
        (await InventarioE2E.FallaAsync(vieja, "Payroll.Settlement.LoanProposalOutdated")).GetProperty("data").ValueKind.Should().Be(JsonValueKind.Object);
        (await OperacionesAsync(fx, esc.Desembolso, credito)).Should().NotContain(o => o.Clase == LoanOperationKind.Payment);

        // Recalculada, la propuesta trae los dos y la aprobación los aplica por el contrato, todo o nada.
        var recalculo = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, $"{Terminaciones}/{corrida}/recalculate", null);
        recalculo.StatusCode.Should().Be(HttpStatusCode.Created, await recalculo.Content.ReadAsStringAsync());
        var nueva = (await NominaE2E.LeerAsync(recalculo)).GetProperty("runPublicId").GetGuid();
        var numeroDelOtro = await DesembolsoE2E.EnLaBaseAsync(fx, esc.Desembolso, db => db.Loans.AsNoTracking().Where(l => l.PublicId == otro).Select(l => l.Number).SingleAsync());
        var recalculada = DeCartera(await NominaE2E.GetAsync(http, esc.Admin, $"{Terminaciones}/{nueva}/deductions"));
        recalculada.Should().HaveCount(2);
        recalculada.Should().Contain(d => d.Contains(numero)).And.Contain(d => d.Contains(numeroDelOtro));
        var aprobada = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, $"{Terminaciones}/{nueva}/approve", new { confirm = true, confirmWithoutSegregation = true });
        aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
        var recaudos = (await OperacionesAsync(fx, esc.Desembolso, credito)).Where(o => o.Clase == LoanOperationKind.Payment && o.Clave!.StartsWith("NOM:")).ToList();
        recaudos.Should().ContainSingle("la definitiva recauda por el contrato con la clave de Nómina");

        // Reversar la definitiva reversa sus recaudos por el contrato.
        var reversada = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, $"{Terminaciones}/{nueva}/reverse", new { reason = "La fecha de retiro estaba errada (e2e)" });
        reversada.IsSuccessStatusCode.Should().BeTrue(await reversada.Content.ReadAsStringAsync());
        (await OperacionesAsync(fx, esc.Desembolso, credito)).Single(o => o.Clave == recaudos[0].Clave).ReversadaPor.Should().NotBeNull();
    }

    /// <summary>Las descripciones de los descuentos de Cartera de la propuesta («Crédito CR-… (línea)»).</summary>
    private static List<string> DeCartera(JsonElement propuesta) => propuesta.GetProperty("items").EnumerateArray()
        .Select(i => i.GetProperty("description").GetString()!).Where(d => d.StartsWith("Crédito ", StringComparison.Ordinal)).ToList();

    private static string Estado(JsonElement descuento) => descuento.GetProperty("status") switch
    {
        { ValueKind: JsonValueKind.String } s => s.GetString()!,
        var n => ((IngenIA365ERP.Domain.Enums.Payroll.PayrollLoanDeductionStatus)n.GetInt32()).ToString(),
    };

    /// <summary>
    /// A11: el intento del contrato quedó (se escribe aparte) y su hecho se fue con la transacción de Nómina; pasado el tiempo máximo de una
    /// transacción, <c>?operationKey=</c> lo rotula «revertido con el recaudador».
    /// </summary>
    private async Task AuditoriaRevertidaAsync(HttpClient http, Escenario esc, string clave)
    {
        await CarteraE2E.ReenviarAuditoriaAsync(fx, esc.Coop.TenantPublicId);
        using var _ = RelojEn(fx, CarteraE2E.Hoy(fx).AddDays(1));
        var eventos = await CarteraE2E.GetAsync(http, esc.Admin, $"/api/audit/logs?operationKey={Uri.EscapeDataString(clave)}&pageSize=50");
        var items = eventos.GetProperty("items").EnumerateArray().ToList();
        items.Should().Contain(e => e.GetProperty("action").GetString() == "Lending.ObligationContract.Attempted"
            && e.GetProperty("label").GetString() == "Revertido con el recaudador", "el intento sin hecho fue revertido con el recaudador (A11)");
        items.Should().NotContain(e => e.GetProperty("action").GetString() == "Lending.ObligationContract.Applied");
    }
}
