using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0527 (feature 013, US5; quickstart §1.7 y §3.9, nombre fijo; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): el contrato de
/// obligaciones real por HTTP con tokens reales, sobre cooperativas aisladas activadas con <see cref="CarteraE2E.ActivarCarteraAsync"/> (C107, sin
/// <c>Skip</c>), con la fila <c>TST</c> del registro y créditos desembolsados por las rutas de US3. El recaudador de prueba llama al contrato dentro
/// de su transacción, como lo haría Recaudo:
/// <list type="bullet">
/// <item>listar y simular responden <c>Answered</c> sin escribir (C28); las siete modalidades se aplican; la cuota específica con anteriores impagas
/// se rechaza con <c>pendingBefore</c>; el abono devuelve los dos planes y se aplica con la elección y su <c>SimulationId</c>; un pago mayor que el
/// total cancela y devuelve el excedente (D5);</item>
/// <item>un abono no interactivo sin preferencia queda como saldo a favor con el aviso <c>PreferenceAssumed</c> y se resuelve como abono; con la
/// preferencia, el comprobante dice que la elección salió de ella (D24);</item>
/// <item>el reverso desde la pantalla del crédito del primero de dos pagos redistribuye el posterior y deja los saldos como si el pago no hubiera
/// existido (SC-003, SC-004), avisa al recaudador (<c>PagoReversadoPorCartera</c>) y un reverso no se reversa;</item>
/// <item>la fecha valor de cinco días atrás se admite y la de seis no (D4); un pago a un castigado emite <c>RecuperacionDeCastigada</c>;</item>
/// <item>en otra cooperativa (corre el ciclo diario): una tanda que no toca la distribución no invalida la simulación (R40), otro pago sí
/// (<c>DistributionChanged</c>); el anticipo de tres cuotas cubre la primera al vencer (<c>AdvanceApplied</c>).</item>
/// </list>
/// El recaudador fuera del registro está en <see cref="DoblesDePruebaDeCarteraTests"/> (una cooperativa sin la fila <c>TST</c>). (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ContratoDeObligacionesTests(CentralIdentityApiFixture fx)
{
    private const string Pagos = "pagos";

    private static Guid Credito(JsonElement desembolso) => desembolso.GetProperty("loanPublicId").GetGuid();

    [Fact]
    public async Task Listar_y_simular_no_escriben_y_las_siete_modalidades_se_aplican()
    {
        var esc = await EscenarioAsync(fx, Pagos);
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var (_, persona, a) = await CreditoDesembolsadoAsync(http, esc, "Modalidades", 3_000_000m);
        var (_, personaB, b) = await CreditoDesembolsadoAsync(http, esc, "Ciclo", 2_000_000m);
        var (creditoA, creditoB) = (Credito(a), Credito(b));
        var cuota1 = await CuotaAsync(http, u.Jefe.Token, creditoA);
        var cuota2 = await CuotaAsync(http, u.Jefe.Token, creditoA, 2);
        var dia = cuota1.Vence.AddDays(10);

        using (RelojEn(fx, dia))
        {
            // Listar (C28): Answered, la obligación viva con lo vencido y las modalidades que admite.
            var listado = await ListarAsync(http, u, persona);
            listado.GetProperty("lendingStatus").GetInt32().Should().Be(2, "Cartera está activada");
            var ob = listado.GetProperty("obligations").EnumerateArray().Single(o => o.GetProperty("obligationPublicId").GetGuid() == creditoA);
            ob.GetProperty("status").GetInt32().Should().Be(2);
            ob.GetProperty("pastDue").GetDecimal().Should().BePositive("la primera cuota venció hace diez días");
            ob.GetProperty("daysPastDue").GetInt32().Should().Be(10);
            ob.GetProperty("admittedModalities").EnumerateArray().Select(m => m.GetInt32()).Should().Contain([1, 2, 3, 4, 5, 7]);

            // Simular no escribe: Answered con su huella, sin una operación más.
            var antes = (await OperacionesAsync(fx, esc, creditoA)).Count;
            foreach (var (modalidad, valor) in new[] { ("PastDue", 0m), ("FreeAmount", 50_000m), ("Advance", 100_000m),
                         ("CycleInstallment", ob.GetProperty("cycleInstallment").GetDecimal()),
                         ("ExtraordinaryPrincipal", 400_000m), ("FullPayoff", 0m) })
            {
                var s = await SimularAsync(http, u, creditoA, modalidad, valor, dia);
                Resultado(s).Should().Be(Respondido, $"{modalidad}: {s}");
                Codigo(s).Should().Be("Lending.ObligationContract.Simulated");
                s.GetProperty("value").GetProperty("simulationId").GetString().Should().MatchRegex("^[0-9a-f]{64}$", "la huella SHA-256 de la simulación (T32)");
            }
            (await OperacionesAsync(fx, esc, creditoA)).Should().HaveCount(antes, "simular no escribe (C28)");
            var anticipo = await SimularAsync(http, u, creditoA, "Advance", 100_000m, dia);
            anticipo.GetProperty("value").GetProperty("notices").EnumerateArray().Select(n => n.GetString())
                .Should().Contain("Lending.Payment.AdvanceDoesNotReduceInterest");

            // La cuota específica con la primera impaga: rechazada con las anteriores pendientes (C115).
            var especifica = await AplicarUnoAsync(http, u, Pago(creditoA, persona, "SpecificInstallment", cuota2.Total, dia, cuota: 2));
            Rechazada(especifica, "Lending.Payment.SpecificInstallmentBlocked");
            especifica.GetProperty("data").GetProperty("pendingBefore").EnumerateArray().Select(x => x.GetInt32()).Should().Contain(1);

            // Valor libre y lo vencido.
            var libre = await AplicadoAsync(http, u, Pago(creditoA, persona, "FreeAmount", 50_000m, dia));
            libre.GetProperty("number").GetString().Should().StartWith(AplicacionDePagosPrefijo);
            libre.GetProperty("appliedAmount").GetDecimal().Should().Be(50_000m);
            libre.GetProperty("distribution").GetArrayLength().Should().BePositive();
            var vencido = (await ObligacionAsync(http, u, persona, creditoA)).GetProperty("pastDue").GetDecimal();
            await AplicadoAsync(http, u, Pago(creditoA, persona, "PastDue", vencido, dia));
            var alDia = await ObligacionAsync(http, u, persona, creditoA);
            alDia.GetProperty("pastDue").GetDecimal().Should().Be(0m);
            alDia.GetProperty("daysPastDue").GetInt32().Should().Be(0);

            // La cuota específica siguiente ya se admite; un anticipo no reduce intereses (D6, D8).
            await AplicadoAsync(http, u, Pago(creditoA, persona, "SpecificInstallment", cuota2.Total, dia, cuota: 2));
            var conAnticipo = await AplicadoAsync(http, u, Pago(creditoA, persona, "Advance", 100_000m, dia));
            conAnticipo.GetProperty("advanceAmount").GetDecimal().Should().Be(100_000m);

            // El abono extraordinario: sin elección, dos planes; con la elección, su huella, y se aplica en la forma interactiva con ella.
            var sinEleccion = await SimularAsync(http, u, creditoA, "ExtraordinaryPrincipal", 400_000m, dia);
            sinEleccion.GetProperty("value").GetProperty("alternatives").GetArrayLength().Should().Be(2, "reducir el plazo o la cuota (FR-075)");
            var conEleccion = await SimularAsync(http, u, creditoA, "ExtraordinaryPrincipal", 400_000m, dia, eleccion: "ReduceTerm");
            var simulacion = conEleccion.GetProperty("value").GetProperty("simulationId").GetString()!;
            var abono = await AplicadoAsync(http, u, Pago(creditoA, persona, "ExtraordinaryPrincipal", 400_000m, dia, eleccion: "ReduceTerm",
                simulacion: simulacion), interactiva: true);
            abono.GetProperty("newPlan").ValueKind.Should().Be(JsonValueKind.Object, "el abono regenera el plan");
            (await CarteraE2E.GetAsync(http, u.Jefe.Token, $"{Creditos}/{creditoA}/schedule/versions")).GetArrayLength().Should().BeGreaterThan(1);

            // Cancelar con más de lo que se debe: cancelado, el excedente vuelve al recaudador (D5) y la firmeza en el comprobante.
            var paraCancelar = (await ObligacionAsync(http, u, persona, creditoA)).GetProperty("payoffAmount").GetDecimal();
            var cancelado = await AplicadoAsync(http, u, Pago(creditoA, persona, "FullPayoff", paraCancelar + 10_000m, dia));
            cancelado.GetProperty("paidOff").GetBoolean().Should().BeTrue();
            cancelado.GetProperty("surplus").GetDecimal().Should().Be(10_000m);
            cancelado.GetProperty("finalityUntil").ValueKind.Should().Be(JsonValueKind.String);
            (await EstadoAsync(fx, esc, creditoA)).Should().Be(LoanStatus.PaidOff);

            // La cuota del ciclo en otro crédito: lo vencido y la cuota del período.
            var ciclo = (await ObligacionAsync(http, u, personaB, creditoB)).GetProperty("cycleInstallment").GetDecimal();
            ciclo.Should().BePositive();
            await AplicadoAsync(http, u, Pago(creditoB, personaB, "CycleInstallment", ciclo, dia));
            (await ObligacionAsync(http, u, personaB, creditoB)).GetProperty("pastDue").GetDecimal().Should().Be(0m);

            // Cada pago dejó su PagoAplicado en la bandeja.
            var pagos = (await OperacionesAsync(fx, esc, creditoA)).Where(o => o.Clase == LoanOperationKind.Payment).ToList();
            pagos.Should().HaveCount(6);
            (await MensajesAsync(fx, esc, pagos[0].PublicId)).Should().Contain("PagoAplicado");
        }
    }

    private const string AplicacionDePagosPrefijo = "PAG-";

    [Fact]
    public async Task Un_abono_sin_preferencia_queda_como_saldo_a_favor_y_se_resuelve_como_abono()
    {
        var esc = await EscenarioAsync(fx, Pagos);
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var (_, persona, d) = await CreditoDesembolsadoAsync(http, esc, "SaldoAFavor", 3_000_000m);
        var credito = Credito(d);
        var hoy = esc.Hoy.AddDays(2);

        // El deudor revoca su preferencia (la solicitud la traía): una versión nueva, sin elección.
        var revocada = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Creditos}/{credito}/prepayment-preference",
            new { choice = (string?)null, reason = "El deudor revoca la instrucción permanente (e2e)" });
        revocada.StatusCode.Should().Be(HttpStatusCode.Created, await revocada.Content.ReadAsStringAsync());
        var preferencia = await CarteraE2E.GetAsync(http, u.Jefe.Token, $"{Creditos}/{credito}/prepayment-preference");
        preferencia.GetProperty("history").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        using (RelojEn(fx, hoy))
        {
            // Interactivo sin elección: se rechaza; no interactivo sin preferencia: saldo a favor con su aviso (D24).
            var sim = await SimularAsync(http, u, credito, "ExtraordinaryPrincipal", 300_000m, hoy);
            var sinElegir = await AplicarUnoAsync(http, u, Pago(credito, persona, "ExtraordinaryPrincipal", 300_000m, hoy,
                simulacion: sim.GetProperty("value").GetProperty("simulationId").GetString()), interactiva: true);
            Rechazada(sinElegir, "Lending.Payment.PreferenceMissing");

            var abono = await AplicadoAsync(http, u, Pago(credito, persona, "ExtraordinaryPrincipal", 300_000m, hoy));
            abono.GetProperty("creditBalanceAmount").GetDecimal().Should().Be(300_000m);
            Avisos(abono).Should().Contain("Lending.Payment.PreferenceAssumed");
            (await MensajesAsync(fx, esc, abono.GetProperty("operationPublicId").GetGuid())).Should().Contain("SaldoAFavorReconocido");
            (await ObligacionAsync(http, u, persona, credito)).GetProperty("creditBalance").GetDecimal().Should().Be(300_000m);

            // Se resuelve como abono a capital con la elección del deudor (todo lo disponible).
            var resuelto = await ResolverAsync(http, u, new
            {
                obligationPublicId = credito, kind = "ApplyAsExtraordinaryPrincipal", idempotencyKey = Clave(), prepaymentChoice = "ReduceInstallment",
            });
            Resultado(resuelto).Should().Be(Aplicado, resuelto.ToString());
            resuelto.GetProperty("value").GetProperty("amount").GetDecimal().Should().Be(300_000m);
            (await ObligacionAsync(http, u, persona, credito)).GetProperty("creditBalance").GetDecimal().Should().Be(0m);

            // Con la preferencia de nuevo, el abono no interactivo la usa y el comprobante lo dice.
            var vuelve = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Creditos}/{credito}/prepayment-preference",
                new { choice = "ReduceTerm", reason = "El deudor vuelve a pedir reducir el plazo (e2e)" });
            vuelve.StatusCode.Should().Be(HttpStatusCode.Created, await vuelve.Content.ReadAsStringAsync());
            var conPreferencia = await AplicadoAsync(http, u, Pago(credito, persona, "ExtraordinaryPrincipal", 200_000m, hoy));
            conPreferencia.GetProperty("creditBalanceAmount").GetDecimal().Should().Be(0m);
            var comprobante = await CarteraE2E.GetAsync(http, u.Jefe.Token,
                $"{Creditos}/{credito}/operations/{conPreferencia.GetProperty("operationPublicId").GetGuid()}");
            comprobante.GetProperty("receipt").GetProperty("choiceFromPreference").GetBoolean().Should().BeTrue();
            comprobante.GetProperty("receipt").GetProperty("prepaymentChoice").GetString().Should().Be("ReduceTerm");
            comprobante.GetProperty("receipt").GetProperty("receiptNumber").GetString().Should().StartWith("RC-");
        }
    }

    [Fact]
    public async Task El_reverso_del_primero_de_dos_pagos_redistribuye_el_posterior_como_si_no_hubiera_existido()
    {
        var esc = await EscenarioAsync(fx, Pagos);
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var (_, persona, d) = await CreditoDesembolsadoAsync(http, esc, "Reverso", 3_000_000m);
        var credito = Credito(d);
        var dia = (await CuotaAsync(http, u.Jefe.Token, credito)).Vence.AddDays(5);

        using (RelojEn(fx, dia))
        {
            // Lo que quedaría sólo con el segundo pago.
            var soloElSegundo = (await SimularAsync(http, u, credito, "FreeAmount", 150_000m, dia)).GetProperty("value").GetProperty("balancesAfter");

            var clave1 = Clave();
            await AplicadoAsync(http, u, Pago(credito, persona, "FreeAmount", 100_000m, dia.AddDays(-1), clave1));
            var segundo = await AplicadoAsync(http, u, Pago(credito, persona, "FreeAmount", 150_000m, dia));
            var primero = (await OperacionesAsync(fx, esc, credito)).Single(o => o.Clave == clave1);

            // La redistribución antes de reversar, por el contrato (C38).
            var simulada = await SimularAsync(http, u, credito, "FreeAmount", 100_000m, dia.AddDays(-1), claveAReversar: clave1);
            Resultado(simulada).Should().Be(Respondido, simulada.ToString());

            // Desde la pantalla del crédito: vista previa y reverso con su huella.
            var previa = await CarteraE2E.ExitoAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Creditos}/{credito}/operations/{primero.PublicId}/reversal-preview",
                new { reason = "Cheque devuelto (e2e)" });
            previa.GetProperty("previewSha256").GetString().Should().NotBeNullOrWhiteSpace();
            previa.GetProperty("reallocations").EnumerateArray().Select(r => r.GetProperty("number").GetString())
                .Should().Contain(segundo.GetProperty("number").GetString(), "el pago posterior se redistribuye (FR-081)");
            previa.GetProperty("notifiesCollector").GetBoolean().Should().BeTrue();
            var reverso = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Creditos}/{credito}/operations/{primero.PublicId}/reverse",
                new { reason = "Cheque devuelto (e2e)", expectedPreviewSha256 = previa.GetProperty("previewSha256").GetString() });
            reverso.StatusCode.Should().Be(HttpStatusCode.Created, await reverso.Content.ReadAsStringAsync());
            var hecho = await CarteraE2E.LeerAsync(reverso);
            hecho.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("type").GetString())
                .Should().Contain(["PagoReversado", "PagoReversadoPorCartera"]);

            // Como si el primero no hubiera existido (SC-003, SC-004).
            var ahora = await ObligacionAsync(http, u, persona, credito);
            ahora.GetProperty("payoffAmount").GetDecimal().Should().Be(soloElSegundo.GetProperty("payoffAmount").GetDecimal());
            ahora.GetProperty("pastDue").GetDecimal().Should().Be(soloElSegundo.GetProperty("pastDue").GetDecimal());
            ahora.GetProperty("daysPastDue").GetInt32().Should().Be(soloElSegundo.GetProperty("daysPastDue").GetInt32());

            // El recaudador de prueba recibió el aviso; el original quedó enlazado con su reverso.
            var recibidos = await CarteraE2E.GetAsync(http, u.Pruebas.Token, $"{Recaudador}/reversals");
            recibidos.EnumerateArray().Should().Contain(r => r.GetProperty("idempotencyKey").GetString() == clave1);
            var comprobante = await CarteraE2E.GetAsync(http, u.Jefe.Token, $"{Creditos}/{credito}/operations/{primero.PublicId}");
            comprobante.GetProperty("operation").GetProperty("reversedBy").ValueKind.Should().Be(JsonValueKind.String);
            comprobante.GetProperty("receipt").GetProperty("idempotencyKey").GetString().Should().Be(clave1);

            // Un reverso no se reversa.
            var reversoId = hecho.GetProperty("reversalOperationPublicId").GetGuid();
            var deReverso = await CarteraE2E.EnviarAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Creditos}/{credito}/operations/{reversoId}/reversal-preview",
                new { reason = "Otra vez" });
            await InventarioE2E.FallaAsync(deReverso, "Lending.Reversal.OfReversal");
        }
    }

    [Fact]
    public async Task La_fecha_valor_admite_cinco_dias_atras_y_no_seis_y_un_castigado_recupera()
    {
        var esc = await EscenarioAsync(fx, Pagos);
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var (_, persona, d) = await CreditoDesembolsadoAsync(http, esc, "FechaValor", 2_000_000m);
        var credito = Credito(d);
        var hoy = esc.Hoy.AddDays(20);

        using (RelojEn(fx, hoy))
        {
            await AplicadoAsync(http, u, Pago(credito, persona, "FreeAmount", 40_000m, hoy.AddDays(-5)));
            var tarde = await AplicarUnoAsync(http, u, Pago(credito, persona, "FreeAmount", 40_000m, hoy.AddDays(-6)));
            Rechazada(tarde, "Lending.Payment.ValueDateOutOfRange");
            tarde.GetProperty("data").GetProperty("minDate").GetString().Should().Be(hoy.AddDays(-5).ToString("yyyy-MM-dd"));
            var futura = await AplicarUnoAsync(http, u, Pago(credito, persona, "FreeAmount", 40_000m, hoy.AddDays(1)));
            Rechazada(futura, "Lending.Payment.ValueDateOutOfRange");
        }

        // El castigo llega con K3 (no tiene ruta en K1): se marca en la base, como lo dejaría el castigo de un crédito en mora, y el pago de lo
        // vencido es una recuperación.
        var (_, personaC, c) = await CreditoDesembolsadoAsync(http, esc, "Castigado", 2_000_000m);
        var castigado = Credito(c);
        var vence = (await CuotaAsync(http, u.Jefe.Token, castigado)).Vence;
        await EnLaBaseAsync(fx, esc, async db =>
        {
            var loan = await db.Loans.SingleAsync(l => l.PublicId == castigado);
            loan.Status = LoanStatus.WrittenOff;
            loan.WrittenOffOn = vence.AddDays(5);
            await db.SaveChangesAsync();
            return 0;
        });
        using (RelojEn(fx, vence.AddDays(10)))
        {
            var dia = vence.AddDays(10);
            var ob = await ObligacionAsync(http, u, personaC, castigado);
            ob.GetProperty("status").GetInt32().Should().Be(4, "castigado");
            ob.GetProperty("admittedModalities").EnumerateArray().Select(m => m.GetInt32()).Should().NotContain(4, "un castigado no recibe anticipos");
            var recuperado = await AplicadoAsync(http, u, Pago(castigado, personaC, "FreeAmount", 100_000m, dia));
            (await MensajesAsync(fx, esc, recuperado.GetProperty("operationPublicId").GetGuid())).Should().Contain("RecuperacionDeCastigada");
            Rechazada(await AplicarUnoAsync(http, u, Pago(castigado, personaC, "Advance", 100_000m, dia)), "Lending.Payment.ModalityNotAllowed");
        }
    }

    [Fact]
    public async Task Una_tanda_no_invalida_la_simulacion_otro_pago_si_y_el_anticipo_cubre_la_cuota_al_vencer()
    {
        var esc = await EscenarioAsync(fx, "pagos_ciclo");
        var tenant = esc.Coop.TenantPublicId;
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var (_, personaG, g) = await CreditoDesembolsadoAsync(http, esc, "Anticipo", 3_000_000m);
        var (_, personaH, h) = await CreditoDesembolsadoAsync(http, esc, "Cambia", 3_000_000m);
        var (anticipado, otro) = (Credito(g), Credito(h));
        var cuota = await CuotaAsync(http, u.Jefe.Token, anticipado);

        // Tres cuotas de anticipo al día siguiente del desembolso (D6).
        using (RelojEn(fx, esc.Hoy.AddDays(1)))
        {
            var anticipo = await AplicadoAsync(http, u, Pago(anticipado, personaG, "Advance", 3 * cuota.Total, esc.Hoy.AddDays(1)));
            anticipo.GetProperty("advanceAmount").GetDecimal().Should().Be(3 * cuota.Total);
        }

        var dia = cuota.Vence.AddDays(3);
        using (RelojEn(fx, dia))
        {
            // Una simulación antes de la tanda sigue valiendo después: la tanda no toca la distribución (T32, R40).
            var antes = (await SimularAsync(http, u, otro, "FreeAmount", 100_000m, dia)).GetProperty("value").GetProperty("simulationId").GetString();
            await CicloDeHoyAsync(fx, http, u.Jefe.Token, tenant);
            (await MarcaAsync(fx, tenant, otro)).Should().Be(dia);
            await AplicadoAsync(http, u, Pago(otro, personaH, "FreeAmount", 100_000m, dia, simulacion: antes), interactiva: true);

            // Otro pago entre la simulación y la aplicación sí: «la distribución cambió», con la simulación nueva y sin escribir (T32).
            var vista = (await SimularAsync(http, u, otro, "FreeAmount", 80_000m, dia)).GetProperty("value").GetProperty("simulationId").GetString();
            await AplicadoAsync(http, u, Pago(otro, personaH, "FreeAmount", 30_000m, dia));
            var cuantas = (await OperacionesAsync(fx, esc, otro)).Count;
            var cambio = await AplicarUnoAsync(http, u, Pago(otro, personaH, "FreeAmount", 80_000m, dia, simulacion: vista), interactiva: true);
            Rechazada(cambio, "Lending.Payment.DistributionChanged");
            cambio.GetProperty("data").GetProperty("simulation").ValueKind.Should().Be(JsonValueKind.Object);
            (await OperacionesAsync(fx, esc, otro)).Should().HaveCount(cuantas, "«cambió» no escribe");

            // El anticipo cubrió la primera cuota el día que venció (paso 5 del ciclo) y no hay mora.
            var aplicadas = await EnLaBaseAsync(fx, esc, db => (from t in db.LoanTransactions.AsNoTracking()
                                                                join l in db.Loans.AsNoTracking() on t.LoanId equals l.Id
                                                                where l.PublicId == anticipado && t.Kind == LoanTransactionKind.AdvanceApplied
                                                                select t.ValueDate).ToListAsync());
            aplicadas.Should().Contain(cuota.Vence, "el anticipo cubre la cuota en su vencimiento (D6)");
            var ob = await ObligacionAsync(http, u, personaG, anticipado);
            ob.GetProperty("pastDue").GetDecimal().Should().Be(0m);
            ob.GetProperty("daysPastDue").GetInt32().Should().Be(0);
            ob.GetProperty("advance").GetDecimal().Should().BeLessThan(3 * cuota.Total).And.BePositive();
            (await EnLaBaseAsync(fx, esc, db => db.IntegrationMessages.AsNoTracking().CountAsync(m => m.Type == "AnticipoAplicado"))).Should().BePositive();
        }
    }
}
