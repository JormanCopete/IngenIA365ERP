using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.RecogidaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T096 (feature 028, HU1, HU2, HU3, HU8; FR-001 a FR-016, FR-027, FR-027d, FR-028, FR-029; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): la
/// recogida de un crédito por HTTP de punta a punta, en la cooperativa aislada «recogida» con la matriz contable de Cartera completa (cargada por la plantilla,
/// sin huecos) y el puente de la recogida propia (<c>RecaudoPorAplicar</c> con la clase <c>OwnLoanPayoff</c>) en su propia cuenta:
/// <list type="number">
/// <item>el crédito viejo, de la línea con pagaré desmaterializado en custodia, sale en <c>POST /payoffs/candidates</c> recogible, con lo que cuesta y el neto;</item>
/// <item>la solicitud que lo recoge guarda la proyección, se radica y la aprueba otra persona;</item>
/// <item>el asociado paga en la caja (Recaudo real por el contrato) antes del desembolso: la vista previa recalcula, muestra la diferencia con su causa
/// (<c>Payments</c>) y el neto mayor; sin reconocerla, <c>Lending.Payoff.DifferencesNotAcknowledged</c>;</item>
/// <item>reconocida, el desembolso cancela el viejo (<c>PaidOff</c>, «recogido por» en su detalle y «recogió» en el nuevo), la orden de giro sale por el bruto
/// menos lo aplicado y la fila de la recogida guarda lo proyectado, lo aplicado, la causa y quién la reconoció;</item>
/// <item>en Contabilidad, el <c>DS</c> acredita «desembolso por girar» por el neto y el puente por lo recogido, el <c>RC</c> de la recogida debita el puente y
/// el puente queda en cero; la conciliación de Cartera cuadra;</item>
/// <item>el pagaré del recogido no se anula mientras el crédito nuevo no esté girado (<c>Lending.Collateral.PendingNewLoanDisbursement</c>, «por liberar al
/// girar»); Tesorería real gira la orden, Cartera consume el resultado y ya se anula;</item>
/// <item>la vista <c>payoffs</c> del centro de informes lista la recogida con lo proyectado, lo aplicado, la diferencia y su causa.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class RecogidaDeCreditosTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "recogida";

    /// <summary>El puente de la recogida propia en su propia cuenta (bajo 144105, como los roles débito de la matriz del ensayo).</summary>
    private const string Puente = "14410599";

    private const decimal Monto = 3_000_000m;
    private const decimal Abono = 100_000m;

    [Fact]
    public async Task Candidatos_solicitud_pago_en_caja_diferencia_desembolso_contabilidad_giro_garantias_e_informe()
    {
        var inicial = await EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var esc = await RefrescarAsync(http, inicial, Cooperativa);
        var admin = esc.Coop.TokenAdmin;
        var hoy = esc.Hoy;

        // ----------------------------------------------- la matriz de Cartera completa, con el puente de la recogida aparte --
        await ContabilidadE2E.CrearCuentaAsync(http, admin, Puente, "Puente de la recogida propia (e2e 028)", "144105", ["CNT", "CAR"]);
        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: false,
            cuentasPropias: new Dictionary<(string, string), string> { [("RecaudoPorAplicar", "OwnLoanPayoff")] = Puente });
        var cuentas = await ContabilidadDeCarteraE2E.CuentasPorRolAsync(http, admin);

        // -------------------------------------------- el crédito viejo, con su pagaré desmaterializado en custodia del depósito --
        var (persona, _) = await DeudorAsync(http, admin, "Recogido");
        var (anterior, _) = await AprobadaAsync(http, esc, persona, 1_000_000m, esc.LineaConPagare);
        var pagare = await PagareEnCustodiaAsync(http, esc, anterior);
        var recogido = (await DesembolsarAsync(http, esc, Desembolso(esc, anterior, persona))).GetProperty("loanPublicId").GetGuid();

        // ------------------------------------------------------------------- HU1: los productos para recoger y el neto --
        var asesor = esc.Usuarios.AsesorO1.Token;
        var candidatos = await CarteraE2E.ExitoAsync(http, asesor, HttpMethod.Post, Candidatos, new
        {
            personPublicId = persona, creditLinePublicId = esc.Originacion.Origen, projectedDisbursementDate = hoy,
            selected = new[] { new { kind = "Loan", productPublicId = recogido, mode = "Total" } },
            newLoan = new { amount = Monto },
        });
        var producto = candidatos.GetProperty("products").EnumerateArray().Single(p => p.GetProperty("productPublicId").GetGuid() == recogido);
        producto.GetProperty("eligible").GetBoolean().Should().BeTrue(producto.ToString());
        var proyectado = producto.GetProperty("toPayOff").GetDecimal();
        proyectado.Should().BeGreaterThan(900_000m, "recogerlo cuesta su capital más lo causado");
        candidatos.GetProperty("summary").GetProperty("netToDisburse").GetDecimal().Should().Be(Monto - proyectado, "el neto descuenta lo marcado");

        // --------------------------------------------- HU2: la solicitud guarda la proyección; la radica el asesor y la decide otro --
        var radicada = await AprobadaQueRecogeAsync(http, esc, SolicitudQueRecoge(esc, persona, Monto, hoy, [Credito(recogido)]));
        var solicitud = radicada.GetProperty("publicId").GetGuid();
        var guardada = radicada.GetProperty("payoffs").EnumerateArray().Single();
        guardada.GetProperty("productPublicId").GetGuid().Should().Be(recogido);
        guardada.GetProperty("projected").GetBoolean().Should().BeTrue("la solicitud guardó la proyección");
        guardada.GetProperty("projectedAmount").GetDecimal().Should().Be(proyectado, "la misma liquidación que la consulta de productos");
        radicada.GetProperty("payoffSummary").GetProperty("netToDisburse").GetDecimal().Should().Be(Monto - proyectado);

        // ------------------------------------------------------ el asociado abona en la caja después de la proyección --
        var abono = await PagosE2E.AplicadoAsync(http, esc.Usuarios, PagosE2E.Pago(recogido, persona, "FreeAmount", Abono, hoy));
        abono.GetProperty("operationPublicId").GetGuid().Should().NotBeEmpty();

        // ------------------------------------------------- HU3: la vista previa recalcula y muestra la diferencia con su causa --
        var jefe = esc.Usuarios.Jefe.Token;
        var cuerpo = Desembolso(esc, solicitud, persona, transferencia: true);
        var previa = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"/api/lending/applications/{solicitud}/disbursement-preview", cuerpo);
        var enPrevia = previa.GetProperty("payoffs").EnumerateArray().Single();
        enPrevia.GetProperty("loanPublicId").GetGuid().Should().Be(recogido);
        enPrevia.GetProperty("omitted").GetBoolean().Should().BeFalse();
        enPrevia.GetProperty("projected").GetDecimal().Should().Be(proyectado);
        var ahora = enPrevia.GetProperty("current").GetDecimal();
        ahora.Should().Be(proyectado - Abono, "el abono de la caja es un anticipo que la cancelación descuenta");
        enPrevia.GetProperty("toApply").GetDecimal().Should().Be(ahora, "con Recogida.AlDesembolsar = Recalcular se aplica lo de ahora");
        enPrevia.GetProperty("difference").GetDecimal().Should().Be(-Abono);
        enPrevia.GetProperty("causes").EnumerateArray().Select(c => c.GetString()).Should().Contain("Payments", "la causa es el pago recibido después de proyectar");
        var diferencias = previa.GetProperty("payoffDifferences");
        diferencias.GetProperty("requiresAcknowledgement").GetBoolean().Should().BeTrue();
        diferencias.GetProperty("aboveTolerance").GetBoolean().Should().BeFalse("de fábrica no hay tolerancia: sólo se avisa y se reconoce");
        previa.GetProperty("netToDisburse").GetDecimal().Should().Be(Monto - ahora, "el neto sube con lo que ya pagó");

        var sinReconocer = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Desembolsos, cuerpo);
        await InventarioE2E.FallaAsync(sinReconocer, "Lending.Payoff.DifferencesNotAcknowledged");

        cuerpo["acknowledgedPayoffDifferences"] = true;
        var hecho = await DesembolsarAsync(http, esc, cuerpo);
        var nuevo = hecho.GetProperty("loanPublicId").GetGuid();
        var numeroNuevo = hecho.GetProperty("loanNumber").GetString()!;
        var recogida = hecho.GetProperty("payoffs").EnumerateArray().Single();
        recogida.GetProperty("loanPublicId").GetGuid().Should().Be(recogido);
        recogida.GetProperty("applied").GetDecimal().Should().Be(ahora);
        recogida.GetProperty("projected").GetDecimal().Should().Be(proyectado);
        recogida.GetProperty("difference").GetDecimal().Should().Be(-Abono);
        var neto = Monto - ahora;
        hecho.GetProperty("netToDisburse").GetDecimal().Should().Be(neto, "el neto es el bruto menos lo aplicado (FR-016)");

        // El viejo queda cancelado por el contrato y cada uno nombra al otro (FR-029).
        (await PagosE2E.EstadoAsync(fx, esc, recogido)).Should().Be(LoanStatus.PaidOff);
        var pagoDeLaRecogida = recogida.GetProperty("receiptOperationPublicId").GetGuid();
        (await PagosE2E.OperacionesAsync(fx, esc, recogido)).Single(o => o.PublicId == pagoDeLaRecogida).Clave.Should().StartWith("CAR:");
        var detalleViejo = await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{recogido}");
        var recogidoPor = detalleViejo.GetProperty("paidOffBy").EnumerateArray().Should().ContainSingle().Subject;
        recogidoPor.GetProperty("number").GetString().Should().Be(numeroNuevo);
        recogidoPor.GetProperty("applied").GetDecimal().Should().Be(ahora);
        var detalleNuevo = await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{nuevo}");
        detalleNuevo.GetProperty("paidOff").EnumerateArray().Should().ContainSingle(p => p.GetProperty("loanPublicId").GetGuid() == recogido);

        // La orden de giro por el bruto menos lo aplicado, al peso (H4, SC-002).
        var orden = hecho.GetProperty("order").GetProperty("publicId").GetGuid();
        var deLaOrden = await CarteraE2E.GetAsync(http, jefe, $"{Ordenes}/{orden}");
        deLaOrden.GetProperty("grossAmount").GetDecimal().Should().Be(Monto);
        deLaOrden.GetProperty("payoffAmount").GetDecimal().Should().Be(ahora);
        deLaOrden.GetProperty("netAmount").GetDecimal().Should().Be(neto);

        // La fila de la recogida guarda lo proyectado, lo aplicado, la causa y quién reconoció la diferencia (FR-030).
        var fila = await EnLaBaseAsync(fx, esc, async db =>
        {
            var id = await db.Loans.AsNoTracking().Where(l => l.PublicId == recogido).Select(l => l.Id).SingleAsync();
            return await db.DisbursementPayoffs.AsNoTracking().SingleAsync(p => p.LoanId == id);
        });
        fila.ProjectedAmount.Should().Be(proyectado);
        fila.AppliedAmount.Should().Be(ahora);
        fila.DifferenceCause.HasFlag(PayoffDifferenceCause.Payments).Should().BeTrue();
        fila.AcknowledgedBy.Should().NotBeNullOrWhiteSpace("la diferencia quedó reconocida por quien desembolsó");

        // -------------------------------------- FR-027: el DS acredita el neto y el puente; el RC debita el puente; el puente en cero --
        var mensajeDs = await MensajeAsync(fx, esc, hecho.GetProperty("operationPublicId").GetGuid(), "CreditoDesembolsado");
        var ds = await HechoProcesadoAsync(fx, http, admin, esc, nuevo, "CreditoDesembolsado", mensajeDs);
        ds.GetProperty("voucher").GetProperty("voucherTypeCode").GetString().Should().Be("DS");
        var (_, lineasDs) = await LineasDelComprobanteAsync(http, admin, mensajeDs);
        CreditosEnLaCuenta(lineasDs, cuentas["DesembolsoPorGirar"]).Should().Be(neto, "«desembolso por girar» por el neto, no por el bruto (H5)");
        CreditosEnLaCuenta(lineasDs, Puente).Should().Be(ahora, "el DS lleva lo recogido al puente de la recogida propia");

        var mensajeRc = await MensajeAsync(fx, esc, pagoDeLaRecogida, "PagoAplicado");
        var rc = await HechoProcesadoAsync(fx, http, admin, esc, recogido, "PagoAplicado", mensajeRc);
        rc.GetProperty("voucher").GetProperty("voucherTypeCode").GetString().Should().Be("RC");
        var (_, lineasRc) = await LineasDelComprobanteAsync(http, admin, mensajeRc);
        (DebitosEnLaCuenta(lineasRc, Puente) - CreditosEnLaCuenta(lineasRc, Puente)).Should().Be(ahora,
            $"el recibo de la recogida debita el mismo puente por lo aplicado: {string.Join(" | ", lineasRc.Select(l => l.ToString()))}");
        (await SaldoDeLaCuentaAsync(fx, esc, Puente)).Should().Be(0m, "el puente de la recogida queda en cero sin que la contadora iguale dos reglas");

        // La conciliación de Cartera: el puente de la recogida propia en cero y los saldos, el anticipo y «desembolso por girar» cuadrados. El abono de la caja
        // entró por el contrato como Recaudo (clase Receipts) y en esta cooperativa Recaudo «no pasa» a Contabilidad: su puente queda con el abono, del otro lado
        // (lo acredita el recibo de Recaudo), y nada queda sin explicar.
        var conciliacion = await CarteraE2E.GetAsync(http, admin, $"/api/lending/accounting/reconciliation?date={hoy:yyyy-MM-dd}");
        static bool DeRecaudo(JsonElement c) => c.GetProperty("combinations").EnumerateArray()
            .Any(x => x.GetProperty("role").GetString() == "RecaudoPorAplicar" && x.GetProperty("collectorClass").GetString() == "Receipts");
        var conjuntos = conciliacion.GetProperty("sets").EnumerateArray().ToList();
        conjuntos.Where(c => !DeRecaudo(c)).Should().OnlyContain(c => c.GetProperty("difference").GetDecimal() == 0m, conciliacion.ToString());
        conjuntos.Where(DeRecaudo).Should().OnlyContain(c => c.GetProperty("difference").GetDecimal() == -Abono
                                                              && c.GetProperty("unexplainedIsOtherSide").GetBoolean(), conciliacion.ToString());
        conciliacion.GetProperty("bridges").EnumerateArray().Single(b => b.GetProperty("collectorClass").GetString() == "OwnLoanPayoff")
            .GetProperty("balance").GetDecimal().Should().Be(0m, "el puente de la recogida propia cuadra solo (H5)");
        conciliacion.GetProperty("unexplainedTotal").GetDecimal().Should().Be(0m, conciliacion.ToString());

        // ------------------------------------------------- FR-027d: el pagaré del recogido espera el giro del crédito nuevo --
        var delRecogido = (await CarteraE2E.GetAsync(http, asesor, $"{Pagares}?loanPublicId={recogido}")).GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("publicId").GetGuid() == pagare);
        delRecogido.GetProperty("releaseNotice").GetString().Should().Contain(numeroNuevo, "«por liberar al girar el crédito …»");
        var antes = await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, $"{Pagares}/{pagare}/void", new { reason = "Crédito recogido (e2e 028)" });
        var porGirar = await InventarioE2E.FallaAsync(antes, "Lending.Collateral.PendingNewLoanDisbursement");
        porGirar.GetProperty("message").GetString().Should().Contain(numeroNuevo);

        // Tesorería real gira la orden por el neto y Cartera consume el resultado.
        var resultado = await ResultadoDelGiroAsync(fx, http, esc, orden, "Disbursed");
        (await ConsumirAsync(fx, esc, resultado)).Should().BeOfType<IngenIA365ERP.Application.Common.Integration.ResultadoDeConsumo.Processed>();
        (await CarteraE2E.GetAsync(http, jefe, $"{Ordenes}/{orden}")).GetProperty("status").GetString().Should().Be("Disbursed");

        var despues = await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, $"{Pagares}/{pagare}/void", new { reason = "Crédito recogido y girado (e2e 028)" });
        despues.StatusCode.Should().Be(HttpStatusCode.OK, $"girado el crédito nuevo, la cancelación del recogido queda en firme: «{await despues.Content.ReadAsStringAsync()}»");
        (await CarteraE2E.LeerAsync(despues)).GetProperty("status").GetString().Should().Be("Voided");

        // ----------------------------------------------------------- HU8: la vista «Recogidas» del centro de informes --
        var vista = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, jefe, $"/api/reports/lending/payoffs?from={hoy:yyyy-MM-dd}&to={hoy:yyyy-MM-dd}"));
        var deLaVista = vista.Filas.Should().ContainSingle(f => ContabilidadE2E.Tabla.Celda(f, vista.IndicePorClave("_credito")).GetString() == nuevo.ToString())
            .Subject;
        ContabilidadE2E.Tabla.Numero(deLaVista, vista.Indice("Proyectado")).Should().Be(proyectado);
        ContabilidadE2E.Tabla.Numero(deLaVista, vista.Indice("Aplicado")).Should().Be(ahora);
        ContabilidadE2E.Tabla.Numero(deLaVista, vista.Indice("Diferencia")).Should().Be(-Abono);
        ContabilidadE2E.Tabla.Texto(deLaVista, vista.Indice("Causa")).Should().NotBeNullOrWhiteSpace();
        ContabilidadE2E.Tabla.Texto(deLaVista, vista.Indice("Estado")).Should().Be("Aplicada");
    }

    /// <summary>
    /// El pagaré desmaterializado de la solicitud, firmado: el depósito <c>SIMULADO</c> firma a la consulta siguiente (documento terminado en 0) y
    /// «actualizar estado» lo deja en custodia. Devuelve el pagaré.
    /// </summary>
    private static async Task<Guid> PagareEnCustodiaAsync(HttpClient http, Escenario esc, Guid solicitud)
    {
        var asesor = esc.Usuarios.AsesorO1.Token;
        var creado = await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, Pagares, new
        {
            ownerKind = "Application", ownerPublicId = solicitud, form = "Dematerialized",
        });
        creado.StatusCode.Should().Be(HttpStatusCode.Created, await creado.Content.ReadAsStringAsync());
        var pagare = (await CarteraE2E.LeerAsync(creado)).GetProperty("publicId").GetGuid();
        string? estado = null;
        for (var i = 0; i < 5 && estado != "InCustody"; i++)
        {
            var refrescado = await CarteraE2E.EnviarAsync(http, asesor, HttpMethod.Post, $"{Pagares}/{pagare}/refresh", null);
            refrescado.StatusCode.Should().Be(HttpStatusCode.OK, await refrescado.Content.ReadAsStringAsync());
            estado = (await CarteraE2E.LeerAsync(refrescado)).GetProperty("status").GetString();
        }
        estado.Should().Be("InCustody", "firmado, el pagaré queda en custodia del depósito");
        return pagare;
    }
}
