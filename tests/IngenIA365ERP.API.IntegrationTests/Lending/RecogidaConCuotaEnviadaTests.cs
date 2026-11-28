using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Receipts;
using IngenIA365ERP.API.IntegrationTests.Treasury;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.LibranzaE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.RecogidaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T097 (feature 028, HU4; FR-019 a FR-022, SC-004, SC-006; colección «Recaudo y Tesorería e2e», los dos motores por <c>DB_PROVIDER</c>): la cuota ya enviada a
/// la pagaduría en una recogida, por HTTP, sobre el escenario de libranza de K2 en la cooperativa aislada «recogida_lib», con
/// <c>Recogida.CuotaCausada = Preguntar</c> (el asesor elige por crédito, D2) y <c>Recogida.GiroPosteriorDeCreditoRecogido = AlCreditoNuevo</c> (D3):
/// <list type="number">
/// <item>un asociado con dos créditos por libranza; el archivo de descuentos del período lleva la cuota 1 de cada uno y la pagaduría lo recibe (enviado);</item>
/// <item>cinco días después, la consulta de productos muestra la cuota 1 de los dos causada, enviada sin respuesta y elegible para dejar;</item>
/// <item>un crédito nuevo los recoge: uno <b>excluyendo</b> la cuota (queda vivo sólo con ella, que sale de los archivos siguientes) y el otro
/// <b>incluyéndola</b> (cancelado);</item>
/// <item>la pagaduría responde «descontado» por las dos líneas: la del cancelado queda «descuento de un crédito cancelado», sin pendiente de giro; la del vivo,
/// pendiente de giro como siempre;</item>
/// <item>el giro de la pagaduría en Recaudo real: la cuota dejada cancela el crédito viejo al peso y lo de más, y lo girado por el crédito ya cancelado, entran al
/// crédito nuevo; no queda nada pendiente ni no aplicado, y el crédito nuevo dice que la cuota dejada se pagó.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class RecogidaConCuotaEnviadaTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "recogida_lib";
    private const decimal Monto = 2_500_000m;

    [Fact]
    public async Task La_cuota_enviada_se_deja_o_se_incluye_y_el_giro_posterior_entra_al_credito_nuevo()
    {
        var esc = await EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        esc = esc with { Desembolso = await RefrescarAsync(http, esc.Desembolso, Cooperativa) };
        var a = await AccesosAsync(http, esc);
        var h = esc.Hoy;
        await ParametroAsync(http, a.Admin, "Recogida.CuotaCausada", "Preguntar", h);
        await ParametroAsync(http, a.Admin, "Recogida.GiroPosteriorDeCreditoRecogido", "AlCreditoNuevo", h);

        // --------------------------------------------------- dos créditos por libranza del mismo asociado y su archivo enviado --
        var (persona, _) = await DesembolsoE2E.DeudorAsync(http, a.Admin, "CuotaEnviada");
        var (dejada, numeroDejada) = await PorLibranzaAsync(http, esc, a, persona, "EMP-028-01");
        var (incluida, numeroIncluida) = await PorLibranzaAsync(http, esc, a, persona, "EMP-028-01");
        var (_, vence, _) = await PagosE2E.CuotaAsync(http, a.Jefe, dejada);
        var exigible = await PagosE2E.ExigibleAsync(http, a.Jefe, dejada);
        (await PagosE2E.ExigibleAsync(http, a.Jefe, incluida)).Should().Be(exigible, "los dos se desembolsaron el mismo día con el mismo plan");

        var adm = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var archivo = (await GenerarAsync(http, adm, esc.Convenio, vence.Year, vence.Month)).GetProperty("publicId").GetGuid();
        await ConfirmarEnvioAsync(fx, http, esc, archivo, "RAD-028-0001");
        var (enviado, lineas) = await ArchivoAsync(http, adm, archivo);
        enviado.GetProperty("status").GetString().Should().Be("Sent");
        lineas.Should().HaveCount(2);
        var lineaDe = lineas.ToDictionary(l => l.GetProperty("loanPublicId").GetGuid());
        decimal Total(Guid credito) => lineaDe[credito].GetProperty("total").GetDecimal();

        // ------------------------------------------------- cinco días después: la cuota 1 causada y enviada sin respuesta --
        var d = h.AddDays(5);
        d.Should().BeBefore(vence, "la recogida es antes del vencimiento de la cuota 1");
        Guid nuevo;
        decimal valorDejado;
        using (RelojEn(fx, d))
        {
            esc = esc with { Desembolso = await RefrescarAsync(http, esc.Desembolso, Cooperativa) };
            var asesor = esc.Usuarios.AsesorO1.Token;
            var candidatos = await CarteraE2E.ExitoAsync(http, asesor, HttpMethod.Post, Candidatos, new
            {
                personPublicId = persona, creditLinePublicId = esc.Desembolso.Originacion.Origen, projectedDisbursementDate = d,
            });
            foreach (var credito in new[] { dejada, incluida })
            {
                var p = candidatos.GetProperty("products").EnumerateArray().Single(x => x.GetProperty("productPublicId").GetGuid() == credito);
                p.GetProperty("eligible").GetBoolean().Should().BeTrue(p.ToString());
                var cuota = p.GetProperty("currentInstallment");
                cuota.GetProperty("number").GetInt32().Should().Be(1, p.ToString());
                cuota.GetProperty("choosable").GetBoolean().Should().BeTrue("con «Preguntar» el asesor elige por crédito");
                var envio = cuota.GetProperty("sent");
                envio.GetProperty("state").GetInt32().Should().Be((int)SentInstallmentState.SentWithoutResponse, p.ToString());
                envio.GetProperty("lineAmount").GetDecimal().Should().Be(Total(credito), "lo que salió en el archivo");
            }

            var radicada = await AprobadaQueRecogeAsync(http, esc.Desembolso, SolicitudQueRecoge(esc.Desembolso, persona, Monto, d,
                [Credito(dejada, cuotaCausada: "Exclude"), Credito(incluida, cuotaCausada: "Include")]));
            var solicitud = radicada.GetProperty("publicId").GetGuid();

            var jefe = esc.Usuarios.Jefe.Token;
            var cuerpo = DesembolsoE2E.Desembolso(esc.Desembolso, solicitud, persona);
            cuerpo["acknowledgedPayoffDifferences"] = true;
            var previa = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"/api/lending/applications/{solicitud}/disbursement-preview", cuerpo);
            var enPrevia = previa.GetProperty("payoffs").EnumerateArray().Single(p => p.GetProperty("loanPublicId").GetGuid() == dejada);
            var cuotaEnPrevia = enPrevia.GetProperty("currentInstallment");
            cuotaEnPrevia.GetProperty("treatment").GetString().Should().Be("Exclude", enPrevia.ToString());
            cuotaEnPrevia.GetProperty("left").GetDecimal().Should().BePositive();
            cuotaEnPrevia.GetProperty("sent").GetProperty("state").GetInt32().Should().Be((int)SentInstallmentState.SentWithoutResponse);

            var hecho = await DesembolsoE2E.DesembolsarAsync(http, esc.Desembolso, cuerpo);
            nuevo = hecho.GetProperty("loanPublicId").GetGuid();
            var recogidas = hecho.GetProperty("payoffs").EnumerateArray().ToDictionary(p => p.GetProperty("loanPublicId").GetGuid());
            recogidas[dejada].GetProperty("leftInstallmentNumber").GetInt32().Should().Be(1);
            valorDejado = recogidas[dejada].GetProperty("leftInstallment").GetDecimal();
            valorDejado.Should().BePositive().And.BeLessThanOrEqualTo(Total(dejada), "la cuota se deja tal como se envió o, dentro del tope del prepago, con el proporcional");
            recogidas[incluida].GetProperty("leftInstallment").ValueKind.Should().Be(JsonValueKind.Null, "incluida, se recoge todo");
            hecho.GetProperty("netToDisburse").GetDecimal().Should().Be(Monto - recogidas.Values.Sum(p => p.GetProperty("applied").GetDecimal()));

            (await PagosE2E.EstadoAsync(fx, esc.Desembolso, dejada)).Should().Be(LoanStatus.Active, "queda vivo sólo con la cuota dejada");
            (await PagosE2E.EstadoAsync(fx, esc.Desembolso, incluida)).Should().Be(LoanStatus.PaidOff);

            // La cuota dejada era la última: en el archivo del período siguiente ninguno de los dos lleva cuota; sólo la novedad en cero para que la pagaduría
            // deje de descontar (FR-020): el cancelado, «Cancellation»; el que sigue vivo con la cuota dejada, el cambio a cero.
            var siguiente = vence.AddMonths(1);
            var previaDelSiguiente = await CarteraE2E.ExitoAsync(http, esc.Usuarios.Administrador.Token, HttpMethod.Post, $"{Archivos}/preview",
                new { agreementPublicId = esc.Convenio, year = siguiente.Year, month = siguiente.Month });
            var delSiguiente = previaDelSiguiente.GetProperty("lines").EnumerateArray().ToList();
            foreach (var credito in new[] { dejada, incluida })
                delSiguiente.Where(l => l.GetProperty("loanPublicId").GetGuid() == credito).Should().OnlyContain(l =>
                    l.GetProperty("total").GetDecimal() == 0m && l.GetProperty("installmentNumber").ValueKind == JsonValueKind.Null, previaDelSiguiente.ToString());
            delSiguiente.Should().ContainSingle(l => l.GetProperty("loanPublicId").GetGuid() == incluida)
                .Which.GetProperty("novelty").GetString().Should().Be("Cancellation", "el crédito recogido se le retira a la pagaduría");
        }

        // ------------------------------------- el día exigible la pagaduría responde y gira las dos líneas tal como se enviaron --
        using (RelojEn(fx, exigible))
        {
            esc = esc with { Desembolso = await RefrescarAsync(http, esc.Desembolso, Cooperativa) };
            await ResponderAsync(fx, http, esc, archivo,
                [(numeroDejada, "00", Total(dejada), (DateOnly?)exigible), (numeroIncluida, "00", Total(incluida), (DateOnly?)exigible)]);

            // FR-022: la respuesta sobre el crédito cancelado queda marcada y no deja pendiente de giro; la del vivo, sí.
            var cerrado = await DesembolsoE2E.EnLaBaseAsync(fx, esc.Desembolso, db => (from r in db.PayrollDeductionResults.AsNoTracking()
                                                                                       join l in db.Loans.AsNoTracking() on r.LoanId equals l.Id
                                                                                       select new { l.PublicId, r.LoanClosedAtResult }).ToListAsync());
            cerrado.Should().ContainSingle(r => r.PublicId == incluida).Which.LoanClosedAtResult.Should().BeTrue("«descuento de un crédito cancelado»");
            cerrado.Should().ContainSingle(r => r.PublicId == dejada).Which.LoanClosedAtResult.Should().BeFalse();
            adm = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
            var pendiente = (await CarteraE2E.GetAsync(http, adm, $"{Convenios}/{esc.Convenio}/pending-transfers")).EnumerateArray().Should().ContainSingle().Subject;
            pendiente.GetProperty("lines").GetInt32().Should().Be(1, "sólo la cuota del crédito vivo espera el giro");
            pendiente.GetProperty("amount").GetDecimal().Should().Be(Total(dejada));

            // Recaudo real evalúa la respuesta con Cartera (como en PagaduriaDePuntaAPuntaTests): aceptada, su archivo queda respondido y se puede girar.
            var admin = esc.Coop.TokenAdmin;
            var deRecaudo = await ArchivoEnRecaudoAsync(fx, http, esc, archivo);
            var respuesta = (await CarteraE2E.GetAsync(http, admin, $"{ArchivosDeRecaudo}/{deRecaudo}/responses")).EnumerateArray()
                .OrderByDescending(r => r.GetProperty("version").GetInt32()).First().GetProperty("publicId").GetGuid();
            var evaluada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{ArchivosDeRecaudo}/{deRecaudo}/responses/{respuesta}/refresh", new { });
            evaluada.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, await evaluada.Content.ReadAsStringAsync());
            RecaudoYTesoreriaE2E.Enum<PaymasterResponseState>((await CarteraE2E.LeerAsync(evaluada)).GetProperty("status"))
                .Should().Be(PaymasterResponseState.AcceptedByLending, "Cartera aceptó el resultado, también la línea del crédito cancelado");
            RecaudoYTesoreriaE2E.Enum<PayrollDeductionFileState>((await CarteraE2E.GetAsync(http, admin, $"{ArchivosDeRecaudo}/{deRecaudo}")).GetProperty("status"))
                .Should().Be(PayrollDeductionFileState.Responded);

            // El giro de las dos líneas, por lo descontado, en Recaudo real.
            var lineasDeRecaudo = (await CarteraE2E.GetAsync(http, admin, $"{ArchivosDeRecaudo}/{deRecaudo}")).GetProperty("lines").EnumerateArray()
                .ToDictionary(l => l.GetProperty("productNumber").GetString()!);
            var giro = Total(dejada) + Total(incluida);
            var cuenta = await TesoreriaE2E.CuentaPropiaAsync(http, admin, esc.Desembolso, "RECPAG", 1, esc.Desembolso.CuentaDeOrigen);
            var csv = string.Join("\r\n", "fecha;valor;referencia;documento;consignante;descripcion;saldo",
                $"{exigible:yyyy-MM-dd};{giro.ToString("0.00", CultureInfo.InvariantCulture)};GIRO-028-{vence:yyyyMM};;Secretaría de Educación;Giro de libranzas;");
            (await ExtractoE2E.CargarAsync(http, admin, cuenta, csv)).GetProperty("loaded").GetInt32().Should().Be(1);
            var abono = (await InventarioE2E.GetAsync(http, admin, $"/api/receipts/bank-items?account={cuenta}&onlyPending=true")).GetProperty("items")
                .EnumerateArray().Single().GetProperty("publicId").GetGuid();
            var registrado = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/receipts/paymaster-remittances", new
            {
                paymasterPersonPublicId = esc.Pagaduria, bankStatementLinePublicId = abono, filePublicIds = new[] { deRecaudo }, allocationOrder = "AnalystOrder",
                lineOrder = new[] { numeroDejada, numeroIncluida }.Select(n => lineasDeRecaudo[n].GetProperty("deductionFileLinePublicId").GetGuid()).ToArray(),
                surplusAsCreditBalance = false, note = "Giro de las cuotas enviadas de dos créditos recogidos (e2e 028)",
            });
            registrado.GetProperty("amount").GetDecimal().Should().Be(giro);
            var giroId = registrado.GetProperty("publicId").GetGuid();
            await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/receipts/paymaster-remittances/{giroId}/apply", new { });
            var aplicado = await CarteraE2E.GetAsync(http, admin, $"/api/receipts/paymaster-remittances/{giroId}");
            RecaudoYTesoreriaE2E.Enum<PaymasterRemittanceState>(aplicado.GetProperty("status")).Should().Be(PaymasterRemittanceState.Applied, aplicado.GetRawText());
            aplicado.GetProperty("items").EnumerateArray().Should().OnlyContain(l =>
                RecaudoYTesoreriaE2E.Enum<PaymasterRemittanceLineOutcome>(l.GetProperty("outcome")) == PaymasterRemittanceLineOutcome.Applied, aplicado.GetRawText());

            // SC-006: la cuota dejada cancela el crédito viejo al peso; lo de más y lo del crédito ya cancelado van al crédito nuevo (D3).
            (await PagosE2E.EstadoAsync(fx, esc.Desembolso, dejada)).Should().Be(LoanStatus.PaidOff, "el giro de la cuota dejada cierra el crédito viejo");
            var alSucesor = await DesembolsoE2E.EnLaBaseAsync(fx, esc.Desembolso, db => (from o in db.LoanOperations.AsNoTracking()
                                                                                         join l in db.Loans.AsNoTracking() on o.LoanId equals l.Id
                                                                                         where l.PublicId == nuevo && o.Kind == LoanOperationKind.Payment
                                                                                               && o.ReversedByOperationId == null && o.IdempotencyKey != null
                                                                                               && o.IdempotencyKey.EndsWith(":sucesor")
                                                                                         select o.Amount).ToListAsync());
            alSucesor.Should().NotBeEmpty("lo girado por el crédito recogido entra al crédito que lo recogió");
            alSucesor.Sum().Should().Be(Total(incluida) + (Total(dejada) - valorDejado),
                "la línea del crédito cancelado completa y lo que la cuota dejada recibió de más");

            // Sin pendientes ni no aplicados de los dos (SC-004), y el crédito nuevo dice que la cuota dejada ya se pagó.
            adm = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
            (await CarteraE2E.GetAsync(http, adm, $"{Convenios}/{esc.Convenio}/pending-transfers")).EnumerateArray()
                .Sum(p => p.GetProperty("lines").GetInt32()).Should().Be(0, "el giro levantó el pendiente");
            (await CarteraE2E.GetAsync(http, adm, $"{NoAplicados}?agreementPublicId={esc.Convenio}&status=Open")).GetProperty("items").GetArrayLength()
                .Should().Be(0, "nada quedó como no aplicado");
            (await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{DesembolsoE2E.Creditos}/{nuevo}")).GetProperty("paidOff").EnumerateArray()
                .Should().Contain(p => p.GetProperty("loanPublicId").GetGuid() == dejada, "el crédito nuevo dice lo que recogió");
            // La cuota dejada la muestra el detalle del recogido, en «recogido por» (E-12).
            var vinculo = (await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{DesembolsoE2E.Creditos}/{dejada}")).GetProperty("paidOffBy").EnumerateArray()
                .Single(p => p.GetProperty("loanPublicId").GetGuid() == nuevo);
            vinculo.GetProperty("leftInstallmentNumber").GetInt32().Should().Be(1);
            vinculo.GetProperty("leftInstallmentSettledAt").ValueKind.Should().Be(JsonValueKind.String, "la cuota dejada quedó pagada por su canal");
        }
    }

    /// <summary>Un crédito por libranza de <paramref name="persona"/>: radica <c>asesor.o1</c>, aprueba <c>analista</c>, desembolsa <c>jefe</c> por cheque.</summary>
    private static async Task<(Guid Credito, string Numero)> PorLibranzaAsync(HttpClient http, LibranzaE2E.Escenario esc, Accesos a, Guid persona, string empleado)
    {
        var radicada = await OriginacionE2E.RadicarAsync(http, a.Asesor, Solicitud(esc, persona, 1_000_000m, IngresoCertificado, empleado));
        var solicitud = radicada.GetProperty("publicId").GetGuid();
        var (_, huella) = await OriginacionE2E.EnrutamientoAsync(http, a.Asesor, solicitud);
        var aprobada = await OriginacionE2E.DecidirAsync(http, a.Analista, "applications", solicitud, "Approve", huella);
        aprobada.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        var resp = await CarteraE2E.ConClaveAsync(http, a.Jefe, HttpMethod.Post, DesembolsoE2E.Desembolsos, DesembolsoE2E.Desembolso(esc.Desembolso, solicitud, persona));
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, $"desembolsar: «{await resp.Content.ReadAsStringAsync()}»");
        var hecho = await CarteraE2E.LeerAsync(resp);
        return (hecho.GetProperty("loanPublicId").GetGuid(), hecho.GetProperty("loanNumber").GetString()!);
    }
}
