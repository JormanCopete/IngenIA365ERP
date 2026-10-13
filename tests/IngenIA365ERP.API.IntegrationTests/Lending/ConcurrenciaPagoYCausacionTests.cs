using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.Extensions.DependencyInjection;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0529 (feature 013, US5; DT §2.25, nombre fijo; T32, T34, C39, I-4; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): lo que
/// sólo se puede ver con una base real, en una cooperativa aislada («pagos_concurrencia») con el procesador apagado y el ciclo corrido a mano:
/// <list type="bullet">
/// <item>un pago que tiene ocupado un crédito mientras la tanda causa: la tanda lo salta (<c>SKIP LOCKED</c> / <c>READPAST</c>) y el pago lo pone al
/// día; corridos a la vez, ninguno se pierde ni causa un día dos veces;</item>
/// <item>dos recaudadores interactivos con la misma simulación sobre la misma obligación: uno aplica y el otro recibe «la distribución cambió»;
/// dos no interactivos: los dos aplican, el segundo sobre el estado nuevo (C39);</item>
/// <item>dos lotes con los mismos créditos en orden contrario, a la vez: sin interbloqueo (el cerrojo va por Id, §3.11);</item>
/// <item>el contrato llamado sin una transacción en curso: <c>Lending.Contract.TransactionRequired</c> (nunca abre la suya, I-4).</item>
/// </list>
/// El lote de Nómina contra el del recaudador de prueba lo cubre <see cref="NominaRecaudaPorElContratoTests"/> (la aprobación de la nómina aplica
/// su lote mientras el recaudador de prueba paga los mismos créditos). (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ConcurrenciaPagoYCausacionTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "pagos_concurrencia";

    private static Guid Credito(JsonElement desembolso) => desembolso.GetProperty("loanPublicId").GetGuid();

    [Fact]
    public async Task Un_pago_y_la_tanda_sobre_el_mismo_credito_no_pierden_nada()
    {
        var esc = await EscenarioAsync(fx, Cooperativa);
        var tenant = esc.Coop.TenantPublicId;
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var (_, persona, x) = await CreditoDesembolsadoAsync(http, esc, "Ocupado", 2_000_000m);
        var (_, personaY, y) = await CreditoDesembolsadoAsync(http, esc, "AlaVez", 2_000_000m);
        var (ocupado, aLaVez) = (Credito(x), Credito(y));
        var h = esc.Hoy;

        // (1) Con el crédito ocupado (como lo tendría un pago en curso) la tanda lo salta; el pago, al soltarlo, lo pone al día.
        using (RelojEn(fx, h.AddDays(2)))
        {
            await ConCreditosOcupadosAsync(fx, tenant, [ocupado], async () =>
            {
                var corrida = await OrdenarCicloAsync(http, u.Jefe.Token);
                await CorrerHastaTerminarAsync(fx, tenant, corrida, tamano: 1);
                (await MarcaAsync(fx, tenant, ocupado)).Should().Be(h, "la tanda saltó el crédito ocupado (T34)");
            });
            await AplicadoAsync(http, u, Pago(ocupado, persona, "FreeAmount", 30_000m, h.AddDays(2)));
            (await MarcaAsync(fx, tenant, ocupado)).Should().Be(h.AddDays(2), "el pago puso al día lo que la tanda saltó (C04)");
            SinDuplicados(await CausacionAsync(fx, tenant, ocupado));
        }

        // (2) Un pago y la tanda a la vez sobre los mismos créditos: los dos terminan, cada día una vez.
        using (RelojEn(fx, h.AddDays(4)))
        {
            var corrida = await OrdenarCicloAsync(http, u.Jefe.Token);
            var tanda = CorrerHastaTerminarAsync(fx, tenant, corrida, tamano: 1);
            var pago = AplicadoAsync(http, u, Pago(aLaVez, personaY, "FreeAmount", 30_000m, h.AddDays(4)));
            await Task.WhenAll(tanda, pago);
            foreach (var c in new[] { ocupado, aLaVez })
            {
                (await MarcaAsync(fx, tenant, c)).Should().Be(h.AddDays(4));
                var filas = await CausacionAsync(fx, tenant, c);
                SinDuplicados(filas);
                filas.Where(f => f.Clase == LoanTransactionKind.Accrual && f.Concepto == LoanConcept.CurrentInterest).Select(f => f.Dia).Distinct().Order()
                    .Should().Equal(Enumerable.Range(1, 4).Select(i => h.AddDays(i)), "ningún día se pierde ni se repite");
            }
            var verificacion = await CarteraE2E.ExitoAsync(http, esc.Coop.TokenAdmin, HttpMethod.Post, Integridad, new { loanPublicIds = new[] { ocupado, aLaVez } });
            verificacion.GetProperty("mismatches").GetArrayLength().Should().Be(0, "el libro y la reconstrucción coinciden");
        }
    }

    [Fact]
    public async Task Dos_recaudadores_a_la_vez_interactivos_uno_cambia_no_interactivos_los_dos_y_lotes_cruzados_sin_interbloqueo()
    {
        var esc = await EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var (_, pA, a) = await CreditoDesembolsadoAsync(http, esc, "Interactivos", 2_000_000m);
        var (_, pB, b) = await CreditoDesembolsadoAsync(http, esc, "NoInteractivos", 2_000_000m);
        var (_, pC, c) = await CreditoDesembolsadoAsync(http, esc, "Cruce", 2_000_000m);
        var (creditoA, creditoB, creditoC) = (Credito(a), Credito(b), Credito(c));
        // Con lo vencido: dos pagos iguales cubren líneas distintas (antes del vencimiento los dos irían enteros a anticipo y su distribución
        // sería idéntica: la huella no cambiaría, y eso es lo correcto, R40).
        var hoy = (await CuotaAsync(http, u.Jefe.Token, creditoA)).Vence.AddDays(5);

        using (RelojEn(fx, hoy))
        {
            // Interactivos: la misma simulación, dos claves, a la vez → uno aplica y el otro «cambió» (T32).
            var vista = (await SimularAsync(http, u, creditoA, "FreeAmount", 30_000m, hoy)).GetProperty("value").GetProperty("simulationId").GetString();
            var interactivos = await Task.WhenAll(
                AplicarUnoAsync(http, u, Pago(creditoA, pA, "FreeAmount", 30_000m, hoy, simulacion: vista), interactiva: true),
                AplicarUnoAsync(http, u, Pago(creditoA, pA, "FreeAmount", 30_000m, hoy, simulacion: vista), interactiva: true));
            interactivos.Select(Resultado).Should().BeEquivalentTo([Aplicado, Rechazado]);
            Codigo(interactivos.Single(r => Resultado(r) == Rechazado)).Should().Be("Lending.Payment.DistributionChanged");

            // No interactivos: nunca «cambió»; los dos aplican, el segundo sobre el estado que dejó el primero (C39).
            var noInteractivos = await Task.WhenAll(
                AplicarUnoAsync(http, u, Pago(creditoB, pB, "FreeAmount", 40_000m, hoy)),
                AplicarUnoAsync(http, u, Pago(creditoB, pB, "FreeAmount", 40_000m, hoy)));
            noInteractivos.Select(Resultado).Should().OnlyContain(r => r == Aplicado);
            (await OperacionesAsync(fx, esc, creditoB)).Count(o => o.Clase == LoanOperationKind.Payment).Should().Be(2);

            // Dos lotes con los mismos créditos en orden contrario, a la vez: el cerrojo los toma por Id y ninguno se interbloquea.
            var lotes = await Task.WhenAll(
                AplicarAsync(http, u, false, Pago(creditoB, pB, "FreeAmount", 10_000m, hoy), Pago(creditoC, pC, "FreeAmount", 10_000m, hoy)),
                AplicarAsync(http, u, false, Pago(creditoC, pC, "FreeAmount", 10_000m, hoy), Pago(creditoB, pB, "FreeAmount", 10_000m, hoy)));
            lotes.SelectMany(l => l.GetProperty("payments").EnumerateArray()).Select(Resultado).Should().OnlyContain(r => r == Aplicado);
        }
    }

    [Fact]
    public async Task El_contrato_sin_una_transaccion_en_curso_responde_TransactionRequired()
    {
        var esc = await EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var (_, persona, d) = await CreditoDesembolsadoAsync(http, esc, "SinTransaccion", 1_000_000m);
        var credito = Credito(d);
        var hoy = esc.Hoy;

        using var alcance = fx.Factory.Services.CreateScope();
        var cooperativa = (await alcance.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == esc.Coop.TenantPublicId);
        RespuestaDeAplicacionDto? respuesta = null;
        var origen = PasadaDeCartera.Origen;
        var ejecutada = await fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>().EjecutarAsync(cooperativa,
            Actor.ProcesoDeIntegracion(origen), origen, async (servicios, ct) =>
            {
                var contrato = servicios.GetRequiredService<IContratoDeObligaciones>();
                respuesta = await contrato.AplicarAsync(new SolicitudDeAplicacion(RecaudadorDePrueba.Recaudador, FormaDeAplicacion.NonInteractive,
                [
                    new PagoAAplicar(Clave(), credito, ModalidadDePago.FreeAmount, 10_000m, hoy, ClaseDeMedioDeRecaudo.Cash, persona,
                        new ReferenciaDeRecibo("RC-SIN-TX", hoy, null, null, "Recibo")),
                ], []), ct);
            }, CancellationToken.None);
        ejecutada.Should().Be(ResultadoEnCooperativa.Ejecutada);
        respuesta!.Payments.Should().ContainSingle();
        respuesta.Payments[0].Result.Should().Be(ResultadoDelContrato.Rejected);
        respuesta.Payments[0].Code.Should().Be("Lending.Contract.TransactionRequired", "el contrato corre en la transacción del recaudador y nunca abre la suya");
        (await OperacionesAsync(fx, esc, credito)).Should().NotContain(o => o.Clase == LoanOperationKind.Payment);
    }

    private static void SinDuplicados(List<(DateOnly Dia, LoanConcept Concepto, int? Cuota, LoanTransactionKind Clase, decimal Valor)> filas) =>
        filas.Where(f => f.Clase == LoanTransactionKind.Accrual).GroupBy(f => (f.Dia, f.Concepto, f.Cuota)).Where(g => g.Count() > 1)
            .Select(g => g.Key).Should().BeEmpty("ningún día, concepto y cuota se causa dos veces (SC-020)");
}
