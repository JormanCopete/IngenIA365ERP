using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0632 (feature 013, US7; quickstart, Independent Test de la fase 11; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): Contabilidad
/// recibe cada hecho de Cartera por la bandeja y lo convierte en comprobante con la matriz <c>ACC_LendingPostingRules</c>, sin que Cartera escriba un
/// asiento. Sobre una cooperativa aislada activada (<see cref="CarteraE2E.ActivarCarteraAsync"/>, C107, con la contabilidad iniciada) y la matriz
/// cargada <b>por la plantilla</b> (<c>GET /rules/template.xlsx?withData=true</c> → cuenta en cada fila sugerida → <c>POST /rules/import?mode=apply</c>)
/// con dos huecos deliberados —<c>DesembolsoPorGirar|Transfer</c> y <c>RecaudoPorAplicar|Test</c>—, con el despachador conducido a mano
/// (<see cref="ConductorDelDespachador"/>):
/// <list type="bullet">
/// <item>el desembolso por cheque se contabiliza en línea en un <c>DS</c>; contra el hueco y con <c>Contabilidad.FaltaDeRegla = Bloquear</c> la pantalla
/// responde 422 <c>Lending.Prevalidation.NotPostable</c> con <c>data.errors[]</c> (A8);</item>
/// <item>con el doble <see cref="ContabilidadParaCarteraConDoble"/> «sin respuesta» el desembolso sella <c>NoResponse</c>; con
/// <c>PoliticaSinRespuesta = Bloquear</c> responde 422 <c>Lending.Prevalidation.NoResponse</c> (D7);</item>
/// <item>dos días de causación dejan, cada uno, un lote <c>LendingAccrualRun</c> con un <c>CA</c> resumido que lleva una línea por crédito en la cuenta
/// de interés que exige tercero;</item>
/// <item>con <c>FaltaDeRegla = Bloquear</c> el pago por el recaudador de prueba contra el hueco se <b>rechaza</b> con
/// <c>Lending.Prevalidation.NotPostable</c> y su <c>data.errors[]</c> (A8 = «Bloquear todo», respuesta del dueño del 2026-10-14; hasta ese día el
/// contrato nunca bloqueaba); con <c>Advertir</c> se aplica sellado <c>NotPostable</c> y su hecho queda <c>Rejected</c>; creada la regla y
/// reprocesado por <c>/api/lending/messages/reprocess</c>, queda en un <c>RC</c>; el reverso del pago es un comprobante nuevo y el original sigue
/// intacto (T55);</item>
/// <item>un comprobante <c>CAR</c> no se reversa desde Comprobantes (<c>Accounting.Document.ModuleOwned</c>); la conciliación a la fecha da diferencia
/// cero sin pendientes; el balance de prueba cuadra.</item>
/// </list>
/// Lo que esta prueba no recorre y cubren las de Application (anotado en tasks.md, T0632): el ajuste por fecha valor que entra en el lote del día
/// siguiente (C09, <c>RunDailyAccrualCommandHandlerTests</c>), la dimensión contable que cambia cuando la persona deja de ser empleada (C8,
/// <c>ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite</c>), la reclasificación de la cuenta por cobrar de Comercial (US7-6, caso dorado 03/04) y
/// <c>Accounting.Period.LendingPending</c> (el aviso lo fija <c>ClosePeriodConCarteraTests</c>). Los hechos <c>INV</c> del mismo host
/// los recorren las colecciones «Inventario e2e» y «Ventas». (nuevo)
///
/// <para>
/// Fase 12 (T0687, US9, US7-4): hasta la fase 11 terminaba en <c>Accounting.Period.LendingNotClosed</c>; ahora, con los cierres de Cartera, sigue:
/// una calificación por análisis lleva el crédito a C, el cierre del mes deja el lote <c>LendingMonthlyClose</c> con un <c>PV</c> que contabiliza la
/// reclasificación A→C de capital e interés y el deterioro en delta frente al base, la conciliación al último día da cero, la reapertura deja un
/// <c>PV</c> nuevo con los reversos en su propio lote, la versión 2 vuelve a cuadrar y Contabilidad cierra el mes. La reclasificación del
/// deterioro y del suspenso (que piden un deterioro ya reconocido y más de 60 días de mora) la fijan <c>ExecuteMonthlyCloseCommandHandlerTests</c>
/// y los casos dorados.
/// </para>
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ContabilizacionDeCarteraPorMensajesTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "contab_cartera";
    private const string Reglas = "/api/accounting/lending/rules";
    private const string Hechos = "/api/lending/accounting/facts";

    [Fact]
    public async Task Cada_hecho_de_Cartera_llega_a_Contabilidad_por_la_matriz_y_se_corrige_con_comprobante_nuevo()
    {
        var esc = await EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var tenant = esc.Coop.TenantPublicId;
        var despachador = new ConductorDelDespachador(fx);
        var hoy = esc.Hoy;

        // ----------------------------------------------------------------- la matriz por la plantilla, con dos huecos --
        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy);
        var completitud = await CarteraE2E.GetAsync(http, admin, $"/api/accounting/lending/completeness?date={hoy:yyyy-MM-dd}");
        completitud.GetProperty("collectorsWithoutBridge").EnumerateArray().Select(c => c.ToString())
            .Should().Contain(c => c.Contains("Test"), "el hueco deliberado del recaudador de prueba se ve en la completitud");

        // --------------------------------------------------------------------------- desembolso por cheque: un DS --
        var (_, persona, desembolso) = await CreditoDesembolsadoAsync(http, esc, "Contable", 3_000_000m);
        var credito = desembolso.GetProperty("loanPublicId").GetGuid();
        var ds = await DespacharHastaResolverAsync(despachador, tenant, http, admin, credito, "CreditoDesembolsado");
        Estado(ds).Should().Be("Processed", ds.ToString());
        ds.GetProperty("voucher").GetProperty("voucherTypeCode").GetString().Should().Be("DS");
        var documentoDs = await ComprobanteDelHechoAsync(http, admin, ds.GetProperty("messagePublicId").GetGuid());

        // ------------------------------------------------ sin respuesta de Contabilidad: confirma con pendiente (D7) --
        fx.Dobles.ContabilidadNoResponde(tenant);
        try
        {
            var (_, _, sinRespuesta) = await CreditoDesembolsadoAsync(http, esc, "SinRespuesta", 1_000_000m);
            var hecho = await HechoAsync(http, admin, sinRespuesta.GetProperty("loanPublicId").GetGuid(), "CreditoDesembolsado");
            hecho.GetProperty("prevalidationOutcome").GetString().Should().Be("NoResponse");
        }
        finally
        {
            fx.Dobles.ContabilidadNoResponde(tenant, noResponde: false);
        }

        // --------------------------------------------------- contra el hueco, en pantalla y con «Bloquear»: 422 (A8) --
        await ParametroAsync(http, admin, "Contabilidad.FaltaDeRegla", "Bloquear", hoy);
        var (deudorT, _) = await DeudorAsync(http, admin, "Hueco");
        var (solicitudT, _) = await AprobadaAsync(http, esc, deudorT, 1_000_000m);
        var contraElHueco = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, Desembolsos,
            Desembolso(esc, solicitudT, deudorT, transferencia: true));
        contraElHueco.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await contraElHueco.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(contraElHueco)).Should().Be("Lending.Prevalidation.NotPostable");
        var errores = (await CarteraE2E.LeerAsync(contraElHueco)).GetProperty("data").GetProperty("errors").EnumerateArray().ToList();
        errores.Should().Contain(e => e.GetProperty("role").GetString() == "DesembolsoPorGirar", "el hallazgo dice el rol sin regla");
        errores[0].GetProperty("whoFixes").ValueKind.Should().Be(JsonValueKind.Object, "y quién lo corrige (§13.3)");

        // --------------------------------- sin respuesta y con «Bloquear» también en la política sin respuesta: 422 (D7) --
        await ParametroAsync(http, admin, "Contabilidad.PoliticaSinRespuesta", "Bloquear", hoy);
        fx.Dobles.ContabilidadNoResponde(tenant);
        try
        {
            var (deudorN, _) = await DeudorAsync(http, admin, "Bloqueado");
            var (solicitudN, _) = await AprobadaAsync(http, esc, deudorN, 1_000_000m);
            var bloqueado = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, Desembolsos, Desembolso(esc, solicitudN, deudorN));
            bloqueado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await bloqueado.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(bloqueado)).Should().Be("Lending.Prevalidation.NoResponse");
        }
        finally
        {
            fx.Dobles.ContabilidadNoResponde(tenant, noResponde: false);
        }

        // ----------------------------------------- dos días de causación: un CA resumido por día, por lote (C05, C09) --
        foreach (var dia in new[] { hoy.AddDays(1), hoy.AddDays(2) })
        {
            using (RelojEn(fx, dia))
            {
                await CicloDeHoyAsync(fx, http, esc.Usuarios.Jefe.Token, tenant);
                await despachador.PasadasAsync(tenant, 3);
            }
        }
        var lotes = await CarteraE2E.GetAsync(http, admin, "/api/accounting/lending/batches?trigger=LendingAccrualRun&pageSize=50");
        var deCausacion = lotes.GetProperty("items").EnumerateArray().ToList();
        deCausacion.Should().HaveCountGreaterThanOrEqualTo(2, "cada día de causación deja su lote LendingAccrualRun");
        deCausacion.Should().OnlyContain(l => l.GetProperty("trigger").GetInt32() == 7);
        var causados = (await CarteraE2E.GetAsync(http, admin, $"{Hechos}?type=InteresCausado&loanPublicId={credito}&pageSize=50"))
            .GetProperty("items").EnumerateArray().ToList();
        causados.Should().NotBeEmpty("el crédito causó interés");
        causados.Should().OnlyContain(h => Estado(h) == "Processed");
        causados.Select(h => h.GetProperty("voucher").GetProperty("voucherTypeCode").GetString()).Should().OnlyContain(t => t == "CA");
        var ca = await ComprobanteDelHechoAsync(http, admin, causados[0].GetProperty("messagePublicId").GetGuid());
        var lineasCa = (await CarteraE2E.GetAsync(http, admin, $"/api/accounting/documents/{ca}")).GetProperty("lines").EnumerateArray().ToList();
        var deInteres = lineasCa.Where(l => l.GetProperty("accountCode").GetString() == ContabilidadDeCarteraE2E.CuentaDeInteres).ToList();
        deInteres.Should().NotBeEmpty("el CA debita el interés por cobrar");
        deInteres.Should().OnlyContain(l => l.GetProperty("personPublicId").ValueKind == JsonValueKind.String,
                "la cuenta de interés exige tercero: una línea por crédito con su deudor");

        // ----------------------- el pago contra el hueco con «Bloquear»: el contrato lo rechaza con su código (A8 = «Bloquear todo») --
        var diaDePago = hoy.AddDays(2);
        using (RelojEn(fx, hoy.AddDays(1)))
        {
            var rechazado = await AplicarUnoAsync(http, esc.Usuarios, Pago(credito, persona, "FreeAmount", 80_000m, hoy.AddDays(1)));
            Resultado(rechazado).Should().Be(Rechazado, rechazado.ToString());
            rechazado.GetProperty("code").GetString().Should().Be("Lending.Prevalidation.NotPostable");
            rechazado.GetProperty("data").GetProperty("errors").EnumerateArray()
                .Should().Contain(e => e.GetProperty("role").GetString() == "RecaudoPorAplicar", "dice qué regla falta");
        }
        (await OperacionesAsync(fx, esc, credito)).Should().NotContain(o => o.Clase == LoanOperationKind.Payment, "el pago rechazado no se escribe");

        // ------------- con «Advertir» desde el día del pago: se aplica, sella NotPostable y su hecho queda Rejected --
        await ParametroAsync(http, admin, "Contabilidad.FaltaDeRegla", "Advertir", diaDePago);
        string clave;
        using (RelojEn(fx, diaDePago))
        {
            clave = Clave();
            await AplicadoAsync(http, esc.Usuarios, Pago(credito, persona, "FreeAmount", 80_000m, diaDePago, clave));
        }
        var pago = await DespacharHastaResolverAsync(despachador, tenant, http, admin, credito, "PagoAplicado", diaDePago);
        pago.GetProperty("prevalidationOutcome").GetString().Should().Be("NotPostable", "con Advertir el contrato aplica y sella");
        Estado(pago).Should().Be("Rejected", pago.ToString());

        // ---------------------------------------------------- crear la regla, reprocesar y ver el RC (T58, FR-079) --
        var puente = await ContabilidadDeCarteraE2E.CuentaPorRolAsync(http, admin, "RecaudoPorAplicar");
        var regla = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, Reglas, new
        {
            operation = "*", role = "RecaudoPorAplicar", dimensions = new { collectorClass = "Receipts" }, accountPublicId = puente,
            validFrom = new DateOnly(hoy.Year, hoy.Month, 1), reason = "Puente del recaudador de prueba (e2e)",
        });
        regla.StatusCode.Should().Be(HttpStatusCode.Created, await regla.Content.ReadAsStringAsync());
        var reproceso = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/messages/reprocess", new
        {
            messagePublicIds = new[] { pago.GetProperty("messagePublicId").GetGuid() }, reason = "Regla creada (e2e)",
        });
        reproceso.StatusCode.Should().Be(HttpStatusCode.Accepted, await reproceso.Content.ReadAsStringAsync());
        pago = await DespacharHastaResolverAsync(despachador, tenant, http, admin, credito, "PagoAplicado", diaDePago);
        Estado(pago).Should().Be("Processed", pago.ToString());
        var recibosDelPago = await CarteraE2E.GetAsync(http, admin, $"/api/accounting/lending/postings?message={pago.GetProperty("messagePublicId").GetGuid()}");
        pago.GetProperty("voucher").GetProperty("voucherTypeCode").GetString().Should().Be("RC", $"{pago} / {recibosDelPago}");
        (await HechoAsync(http, admin, credito, "AnticipoRecibido")).GetProperty("status").GetString()
            .Should().Be("Processed", "el acompañante se reprocesa con su pago: un solo RC (C01)");
        var documentoRc = await ComprobanteDelHechoAsync(http, admin, pago.GetProperty("messagePublicId").GetGuid());

        // ------------------------------------------------- el reverso del pago es un comprobante nuevo (T55, C08) --
        using (RelojEn(fx, diaDePago))
        {
            await ReversarPorClaveAsync(http, esc.Usuarios, clave, "Cheque devuelto (e2e)");
        }
        var reversado = await DespacharHastaResolverAsync(despachador, tenant, http, admin, credito, "PagoReversado", diaDePago);
        Estado(reversado).Should().Be("Processed", reversado.ToString());
        var documentoReverso = await ComprobanteDelHechoAsync(http, admin, reversado.GetProperty("messagePublicId").GetGuid());
        documentoReverso.Should().NotBe(documentoRc, "el reverso es un comprobante nuevo");
        (await ComprobanteDelHechoAsync(http, admin, pago.GetProperty("messagePublicId").GetGuid())).Should().Be(documentoRc, "el original queda intacto");

        // ------------------------------------------- un comprobante CAR no se reversa desde Comprobantes (T55, §9) --
        var desdeComprobantes = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{documentoDs}/reverse",
            new { reason = "Intento desde Comprobantes (e2e)" });
        desdeComprobantes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await desdeComprobantes.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(desdeComprobantes)).Should().Be("Accounting.Document.ModuleOwned");

        // ------------------------------------------------ la conciliación a la fecha: diferencia cero sin pendientes --
        var conciliacion = await CarteraE2E.GetAsync(http, admin, $"/api/lending/accounting/reconciliation?date={diaDePago:yyyy-MM-dd}");
        var conjuntos = conciliacion.GetProperty("sets").EnumerateArray().ToList();
        conjuntos.Should().NotBeEmpty();
        conjuntos.Should().OnlyContain(c => c.GetProperty("difference").GetDecimal() == 0m, conciliacion.ToString());
        conciliacion.GetProperty("unexplainedTotal").GetDecimal().Should().Be(0m);

        // ------------------------------------- el mes no se cierra mientras Cartera, activada, no lo cierre (T57) --
        await CierreE2E.CerrarContabilidadAsync(http, admin, hoy.Year, hoy.Month, "Accounting.Period.LendingNotClosed");

        // ------------------------------------------ el cierre de Cartera del mes: el PV con la reclasificación (T0687) --
        // Fase 12 (US9, US7-4): una calificación por análisis lleva el crédito de A a C; el cierre reclasifica sus saldos y calcula su deterioro
        // en delta frente al base, por partes que forman un lote LendingMonthlyClose con un comprobante PV.
        // La matriz del ensayo da la misma cuenta a todas las categorías (la reclasificación quedaría «sin comprobante, mismas cuentas»): el capital
        // de consumo en C de la línea ORIGEN va a una auxiliar propia, por una regla que gana por línea.
        await ContabilidadE2E.CrearCuentaAsync(http, admin, "14410590", "Capital de consumo en C (e2e)", "144105", ["CNT", "CAR"]);
        var capitalEnC = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, Reglas, new
        {
            operation = "*", role = "Capital", dimensions = new { modalityCode = "CONSUMO", riskCategory = "C", creditLineCode = "ORIGEN" },
            accountPublicId = await ContabilidadE2E.CuentaAsync(http, admin, "14410590"), validFrom = new DateOnly(hoy.Year, hoy.Month, 1),
            reason = "Capital de consumo en C en su propia cuenta (e2e)",
        });
        capitalEnC.StatusCode.Should().Be(HttpStatusCode.Created, await capitalEnC.Content.ReadAsStringAsync());
        var analisis = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{CierreE2E.Riesgo}/analysis-ratings", new
        {
            loanPublicId = credito, category = "C", reason = "Reestructuración pedida por el deudor (e2e)",
        });
        analisis.StatusCode.Should().Be(HttpStatusCode.Created, await analisis.Content.ReadAsStringAsync());
        var (anio, mes) = (hoy.Year, hoy.Month);
        var finDelMes = new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes));
        var diaDeCierre = finDelMes.AddDays(1) > diaDePago ? finDelMes.AddDays(1) : diaDePago;
        var jefe = esc.Usuarios.Jefe.Token;
        using (RelojEn(fx, diaDeCierre))
        {
            await CicloDeHoyAsync(fx, http, jefe, tenant);
            await despachador.PasadasAsync(tenant, 3);
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, await CierreE2E.OrdenarAsync(http, jefe, anio, mes));
            var v1 = await CierreE2E.MensualAsync(http, jefe, anio, mes, version: 1);

            var reclasificada = await DespacharHastaResolverAsync(despachador, tenant, http, admin, credito, "CarteraReclasificadaPorCalificacion", diaDeCierre);
            Estado(reclasificada).Should().Be("Processed", reclasificada.ToString());
            var recibosDelCierre = await CarteraE2E.GetAsync(http, admin, $"/api/accounting/lending/postings?message={reclasificada.GetProperty("messagePublicId").GetGuid()}");
            reclasificada.GetProperty("voucher").GetProperty("voucherTypeCode").GetString().Should().Be("PV", $"{reclasificada} / {recibosDelCierre}");
            reclasificada.GetProperty("accountingDate").GetString().Should().Be(finDelMes.ToString("yyyy-MM-dd"), "los hechos del cierre van al último día del mes");
            var parte = await CarteraE2E.GetAsync(http, admin,
                $"/api/lending/accounting/fact-parts/{reclasificada.GetProperty("part").GetProperty("partPublicId").GetGuid()}?pageSize=200");
            var porConcepto = parte.GetProperty("loans").GetProperty("items").EnumerateArray()
                .Single(l => l.GetProperty("loanPublicId").GetGuid() == credito).GetProperty("byConcept");
            porConcepto.EnumerateObject().Select(c => c.Name).Should().Contain(["Principal", "CurrentInterest"],
                "la reclasificación A→C mueve el capital y el interés del crédito (US7-4)");
            var deterioro = await DespacharHastaResolverAsync(despachador, tenant, http, admin, credito, "DeterioroCalculado", diaDeCierre);
            Estado(deterioro).Should().Be("Processed", deterioro.ToString());
            deterioro.GetProperty("voucher").GetProperty("voucherTypeCode").GetString().Should().Be("PV");
            deterioro.GetProperty("totals").GetDecimal().Should().BeGreaterThan(0m, "el deterioro en C se reconoce en delta frente al base (nada migrado)");
            var lotesDelCierre = (await CarteraE2E.GetAsync(http, admin, "/api/accounting/lending/batches?trigger=LendingMonthlyClose&pageSize=50"))
                .GetProperty("items").EnumerateArray().ToList();
            lotesDelCierre.Should().NotBeEmpty("el cierre forma su lote LendingMonthlyClose").And.OnlyContain(l => l.GetProperty("trigger").GetInt32() == 8);
            await ConciliacionEnCeroAsync(http, admin, v1, "al último día del mes cerrado (SC-006)");

            // --------------------------------------- la reapertura: un PV nuevo con los reversos; Version + 1 vuelve a cuadrar --
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, await CierreE2E.ReabrirAsync(http, admin, v1, "Análisis corregido (e2e)"));
            var reverso = await DespacharHastaResolverAsync(despachador, tenant, http, admin, credito, "ReversoDeCierre", diaDeCierre);
            Estado(reverso).Should().Be("Processed", reverso.ToString());
            reverso.GetProperty("voucher").GetProperty("voucherTypeCode").GetString().Should().Be("PV");
            (await ComprobanteDelHechoAsync(http, admin, reverso.GetProperty("messagePublicId").GetGuid()))
                .Should().NotBe(await ComprobanteDelHechoAsync(http, admin, reclasificada.GetProperty("messagePublicId").GetGuid()),
                    "la reapertura deja su propio comprobante; el del cierre queda intacto");
            (await CarteraE2E.GetAsync(http, admin, "/api/accounting/lending/batches?trigger=LendingMonthlyClose&pageSize=50"))
                .GetProperty("items").GetArrayLength().Should().BeGreaterThan(lotesDelCierre.Count, "la reapertura crea su propio lote");
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, await CierreE2E.OrdenarAsync(http, jefe, anio, mes));
            var v2 = await CierreE2E.MensualAsync(http, jefe, anio, mes, version: 2);
            await despachador.PasadasAsync(tenant, 3);
            await DespacharHastaResolverAsync(despachador, tenant, http, admin, credito, "CarteraReclasificadaPorCalificacion", diaDeCierre);
            await ConciliacionEnCeroAsync(http, admin, v2, "tras volver a cerrar con la versión 2");

            // ------------------------------------------ con el cierre de Cartera vigente, Contabilidad cierra el mes (T57) --
            await CierreE2E.CerrarContabilidadAsync(http, admin, anio, mes, codigoEsperado: null);
        }

        // ---------------------------------------------------------------------------------- el balance de prueba cuadra --
        var descuadre = await EnLaBaseAsync(fx, esc, db => db.JournalEntries.AsNoTracking().Where(j => j.IsPosted && !j.IsDeleted)
            .SumAsync(j => j.Debit - j.Credit));
        descuadre.Should().Be(0m, "ElBalanceDePruebaCuadra");
        var deCartera = await EnLaBaseAsync(fx, esc, db => db.AccountingDocuments.AsNoTracking().CountAsync(d => d.OriginModule == "CAR" && !d.IsDeleted));
        deCartera.Should().BeGreaterThanOrEqualTo(5, "DS, CA de dos días, RC y su reverso");
    }

    /// <summary>El cuadre del cierre con Contabilidad (<c>GET /risk/monthly-closes/{id}/reconciliation</c>): toda diferencia en cero, nada sin explicar.</summary>
    private static async Task ConciliacionEnCeroAsync(HttpClient http, string token, Guid cierre, string cuando)
    {
        var cuadre = await CarteraE2E.GetAsync(http, token, $"{CierreE2E.Cierres}/{cierre}/reconciliation");
        cuadre.GetProperty("sets").EnumerateArray().Should().NotBeEmpty();
        cuadre.GetProperty("sets").EnumerateArray().Should().OnlyContain(c => c.GetProperty("difference").GetDecimal() == 0m, $"{cuando}: {cuadre}");
        cuadre.GetProperty("unexplainedTotal").GetDecimal().Should().Be(0m, cuando);
    }

    private static Task<JsonElement> ParametroAsync(HttpClient http, string token, string clave, string valor, DateOnly desde) =>
        CarteraE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/lending/parameters/{clave}/versions", new
        {
            scopeKind = 0, value = valor, validFrom = desde, reason = "Ensayo de la validación previa contable (e2e)",
        });

    /// <summary>El último hecho contable de ese tipo del crédito (<c>GET /api/lending/accounting/facts</c>).</summary>
    private static async Task<JsonElement> HechoAsync(HttpClient http, string token, Guid credito, string tipo)
    {
        var lista = (await CarteraE2E.GetAsync(http, token, $"{Hechos}?type={tipo}&loanPublicId={credito}&pageSize=50"))
            .GetProperty("items").EnumerateArray().ToList();
        lista.Should().NotBeEmpty($"el crédito emitió un «{tipo}»");
        return lista[0];
    }

    private static string? Estado(JsonElement hecho) => hecho.GetProperty("status").GetString();

    /// <summary>
    /// Pasadas del despachador hasta que el último hecho de ese tipo del crédito deje de estar <c>Pending</c> o <c>InBatch</c> (un lote que se
    /// ordena en una pasada se corre en la siguiente); devuelve el hecho.
    /// </summary>
    private async Task<JsonElement> DespacharHastaResolverAsync(ConductorDelDespachador despachador, Guid tenant, HttpClient http, string token,
        Guid credito, string tipo, DateOnly? dia = null)
    {
        var hecho = await HechoAsync(http, token, credito, tipo);
        for (var i = 0; i < 10 && Estado(hecho) is "Pending" or "InBatch"; i++)
        {
            using (dia is { } d ? RelojEn(fx, d) : null)
                await despachador.PasadasAsync(tenant, 3);
            hecho = await HechoAsync(http, token, credito, tipo);
        }
        if (Estado(hecho) is "Pending" or "InBatch")
        {
            var detalle = await CarteraE2E.GetAsync(http, token, $"/api/lending/messages/{hecho.GetProperty("messagePublicId").GetGuid()}");
            throw new Xunit.Sdk.XunitException($"El hecho {tipo} no se resolvió: {detalle}");
        }
        return hecho;
    }

    /// <summary>El comprobante de un hecho por su recibo (<c>GET /api/accounting/lending/postings?message=</c>).</summary>
    private static async Task<Guid> ComprobanteDelHechoAsync(HttpClient http, string token, Guid mensaje)
    {
        var recibos = (await CarteraE2E.GetAsync(http, token, $"/api/accounting/lending/postings?message={mensaje}"))
            .GetProperty("items").EnumerateArray().Where(r => r.GetProperty("messagePublicId").GetGuid() == mensaje).ToList();
        recibos.Should().ContainSingle($"el hecho {mensaje} tiene un recibo");
        return recibos[0].GetProperty("accountingDocumentPublicId").GetGuid();
    }
}

/// <summary>
/// Los ayudantes de las e2e de la integración contable de Cartera (feature 013, US7, T0632, T0633): una auxiliar por rol y la matriz cargada por la
/// plantilla de §3.7 —bajada con datos, con la cuenta del rol en cada fila sugerida y aplicada todo o nada—. (nuevo)
/// </summary>
public static class ContabilidadDeCarteraE2E
{
    public const string Reglas = "/api/accounting/lending/rules";


    /// <summary>La auxiliar de interés por cobrar: exige tercero, así el CA lleva una línea por crédito.</summary>
    public const string CuentaDeInteres = "14410502";

    /// <summary>
    /// Una auxiliar por rol del catálogo, habilitada para Cartera, bajo un padre del CUIF de la clase que corresponde a su naturaleza (débito → 1441,
    /// crédito → 2410, resultado crédito → 4150, resultado débito → 5115); la de interés exige tercero.
    /// </summary>
    public static async Task<Dictionary<string, string>> CuentasPorRolAsync(HttpClient http, string token)
    {
        var catalogo = await CarteraE2E.GetAsync(http, token, $"{Reglas}/catalog");
        var cuentas = new Dictionary<string, string>(StringComparer.Ordinal);
        var siguiente = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var rol in catalogo.GetProperty("roles").EnumerateArray())
        {
            var codigo = rol.GetProperty("code").GetString()!;
            var debito = rol.GetProperty("nature").GetString() == "Debit";
            var resultado = rol.GetProperty("family").GetString() == "Resultado";
            var padre = (resultado, debito) switch { (true, true) => "511501", (true, false) => "415010", (false, true) => "144105", _ => "241010" };
            var n = siguiente.GetValueOrDefault(padre, 1);
            siguiente[padre] = n + 1;
            var auxiliar = codigo == "InteresPorCobrar" ? CuentaDeInteres : $"{padre}{n + 10:00}";
            await ContabilidadE2E.CrearCuentaAsync(http, token, auxiliar, $"{codigo} (e2e)", padre, ["CNT", "CAR"], tercero: codigo == "InteresPorCobrar");
            cuentas[codigo] = auxiliar;
        }
        return cuentas;
    }

    public static async Task<Guid> CuentaPorRolAsync(HttpClient http, string token, string rol) =>
        await ContabilidadE2E.CuentaAsync(http, token, (await CuentasPorRolAsync(http, token))[rol]);

    /// <summary>
    /// La matriz por la plantilla (§3.7): baja la plantilla con datos, pone la cuenta del rol en cada fila sugerida —salvo los dos huecos— y la aplica
    /// todo o nada. Idempotente: si ya hay reglas de capital (la matriz ya está), no hace nada. Una regla que ya existía —la del recaudo por nómina que
    /// deja <see cref="CarteraE2E.RequisitosDeActivacionAsync"/> si la cooperativa se activó con líneas (decisión del dueño del 2026-10-18)— vuelve
    /// en la plantilla con su cuenta y se reimporta sin cambio.
    /// </summary>
    public static async Task MatrizPorPlantillaAsync(HttpClient http, string token, DateOnly hoy, bool conHuecos = true)
    {
        if ((await CarteraE2E.GetAsync(http, token, $"{Reglas}?role=Capital&pageSize=1")).GetProperty("totalCount").GetInt32() > 0) return;
        var cuentas = await CuentasPorRolAsync(http, token);

        using var pedido = new HttpRequestMessage(HttpMethod.Get, $"{Reglas}/template.xlsx?withData=true");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var bajada = await http.SendAsync(pedido);
        bajada.StatusCode.Should().Be(HttpStatusCode.OK, await bajada.Content.ReadAsStringAsync());

        byte[] llena;
        using (var libro = new XLWorkbook(new MemoryStream(await bajada.Content.ReadAsByteArrayAsync())))
        {
            var hoja = libro.Worksheet("Datos");
            var encabezados = hoja.Row(1).CellsUsed().ToDictionary(c => c.GetString(), c => c.Address.ColumnNumber, StringComparer.Ordinal);
            int Col(string nombre) => encabezados[nombre];
            var filas = 0;
            foreach (var fila in hoja.RowsUsed().Skip(1))
            {
                var rol = fila.Cell(Col("rol")).GetString();
                var clase = fila.Cell(Col("claseDeRecaudador")).GetString();
                var motivo = fila.Cell(Col("motivo")).GetString();
                // Los dos huecos deliberados: el puente del recaudador de prueba y el giro por transferencia.
                if (conHuecos && ((rol == "RecaudoPorAplicar" && clase == "Test") || (rol == "DesembolsoPorGirar" && motivo == "Transfer"))) continue;
                if (fila.Cell(Col("cuenta")).GetString().Length > 0) continue;
                fila.Cell(Col("cuenta")).Value = cuentas[rol];
                fila.Cell(Col("vigenteDesde")).Value = new DateOnly(hoy.Year, hoy.Month, 1).ToDateTime(TimeOnly.MinValue);
                fila.Cell(Col("notas")).Value = "Matriz del ensayo (e2e)";
                filas++;
            }
            filas.Should().BeGreaterThan(20, "la plantilla trae las filas sugeridas sin cuenta por modalidad y categoría");
            using var ms = new MemoryStream();
            libro.SaveAs(ms);
            llena = ms.ToArray();
        }

        foreach (var modo in new[] { "review", "apply" })
        {
            using var formulario = new MultipartFormDataContent();
            var archivo = new ByteArrayContent(llena);
            archivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            formulario.Add(archivo, "file", "matriz-de-cartera.xlsx");
            formulario.Add(new StringContent("Matriz del ensayo de la integración contable"), "reason");
            using var subida = CarteraE2E.ConClave(new HttpRequestMessage(HttpMethod.Post, $"{Reglas}/import?mode={modo}") { Content = formulario });
            subida.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await http.SendAsync(subida);
            resp.StatusCode.Should().Be(HttpStatusCode.OK, $"importar la matriz ({modo}): «{await resp.Content.ReadAsStringAsync()}»");
        }
        (await CarteraE2E.GetAsync(http, token, $"{Reglas}?pageSize=1")).GetProperty("totalCount").GetInt32().Should().BeGreaterThan(20);
    }
}
