using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using static IngenIA365ERP.API.IntegrationTests.Lending.VentasConCarteraE2E;
using static IngenIA365ERP.API.IntegrationTests.Ventas.EscenarioDeVentas;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0578 (feature 013, US6; quickstart §3.7; DT §2.25; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>), sobre la cooperativa de ventas
/// con Cartera activada (<see cref="VentasConCarteraE2E"/>):
/// <list type="bullet">
/// <item>la consulta de cupo y estado responde con el disponible y la línea comercial (US6-1); el cobro en el POS confirma sin aprobación provisional,
/// reserva el número y la v2 —consumida por el despachador— crea la obligación con ese número y la factura, y Comercial guarda
/// <c>ObligacionCreada</c> (US6-2, US6-3, SC-005 en su parte de consumo); el mismo mensaje otra vez no crea otro crédito (US6-7);</item>
/// <item>la venta provisional de antes de activar que no cumple a su fecha crea la obligación marcada, bloquea crédito nuevo sin anular la venta y,
/// confirmada por excepción, Comercial la ve validada (US6-4, B8);</item>
/// <item>una nota crédito parcial baja el capital y recalcula el plan (US6-5);</item>
/// <item>una venta no convertible queda para reproceso y, marcada no reprocesable, Comercial ve el rechazo (C95, C102);</item>
/// <item>un medio <c>CreditWithoutLending</c> no consulta, no reserva y no emite a Cartera (A6, D29).</item>
/// </list>
/// Lo que no se puede armar por HTTP lo cubren las pruebas de Application: la venta fechada hasta el corte (Comercial no admite ventas con fecha
/// anterior y la activación de prueba corta en hoy − 1; <c>VentasAnterioresAlCorteTests</c>, <c>ConciliacionDeVentasDeLaCargaTests</c>) y la venta que
/// excede la concentración (el patrimonio del doble es de mil millones; <c>DestinoCarteraTests</c>). <c>Ventas/CreditoProvisionalTests</c> sigue
/// igual. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class CreditoConCarteraTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    [Fact]
    public async Task La_consulta_la_reserva_y_la_v2_crean_la_obligacion_con_el_numero_y_Comercial_la_guarda()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();

        // US6-1: la consulta de cupo y estado.
        var reloj = Stopwatch.StartNew();
        var evaluacion = await InventarioE2E.ExitoAsync(http, esc.Cajero, HttpMethod.Post, Evaluaciones,
            new { personPublicId = esc.Ximena, paymentMeansPublicId = esc.Ventas.Medio("CREDASOC"), amount = 300_000m });
        reloj.Stop();
        salida.WriteLine($"Consulta de cupo y estado: {reloj.Elapsed.TotalMilliseconds:N0} ms.");
        evaluacion.GetProperty("lendingEnabled").GetBoolean().Should().BeTrue($"Cartera está activada: {evaluacion}");
        evaluacion.GetProperty("origin").GetInt32().Should().Be(3, "Validated");
        evaluacion.GetProperty("requiresApproval").GetBoolean().Should().BeFalse("un crédito validado no pide aprobación provisional");
        var cartera = evaluacion.GetProperty("lending");
        cartera.GetProperty("status").GetString().Should().Be("Active");
        cartera.GetProperty("availableQuota").GetDecimal().Should().BeGreaterThanOrEqualTo(300_000m);
        cartera.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("code").GetString()).Should().Contain(Linea);
        cartera.GetProperty("evidenceId").GetString().Should().NotBeNullOrWhiteSpace();

        // US6-2: el cobro confirma sin aprobación y reserva el número.
        var (venta, cobro) = await CobrarACreditoAsync(http, esc.Ventas, esc.Cajero, esc.Sesion, esc.Ximena, 3, Credito(esc.Ventas, 300_000m));
        cobro.StatusCode.Should().Be(HttpStatusCode.OK, await cobro.Content.ReadAsStringAsync());
        (await InventarioE2E.LeerAsync(cobro)).GetProperty("status").GetInt32().Should().Be(2, "Confirmed: sin aprobación provisional");
        var pago = await PagoACreditoAsync(http, esc.Cajero, venta);
        pago.GetProperty("creditOrigin").GetInt32().Should().Be(3);
        pago.GetProperty("pendingValidation").GetBoolean().Should().BeFalse();
        var numero = pago.GetProperty("reservedLoanNumber").GetString();
        numero.Should().NotBeNullOrWhiteSpace("la confirmación reserva el número en Cartera");
        var reserva = (await CarteraE2E.GetAsync(http, esc.Admin, $"{Reservas}?number={numero}")).GetProperty("items").EnumerateArray().Single();
        reserva.GetProperty("status").GetString().Should().Be("Reserved");
        reserva.GetProperty("amount").GetDecimal().Should().Be(300_000m);
        reserva.GetProperty("consultationEvidence").GetString().Should().NotBeNullOrWhiteSpace("la evidencia de la consulta viaja con la reserva");

        // US6-3: la v2 llega por el despachador; Cartera crea la obligación y Comercial guarda ObligacionCreada.
        reloj.Restart();
        await DespacharAsync(fx, esc);
        reloj.Stop();
        salida.WriteLine($"SC-005, consumo de la venta y de la respuesta (cuatro pasadas del despachador): {reloj.Elapsed.TotalMilliseconds:N0} ms.");
        pago = await PagoACreditoAsync(http, esc.Cajero, venta);
        var respuesta = pago.GetProperty("lendingObligation");
        respuesta.ValueKind.Should().Be(JsonValueKind.Object, $"Comercial guardó la respuesta: {pago}");
        respuesta.GetProperty("outcome").GetString().Should().Be("Created");
        respuesta.GetProperty("validationStatus").GetString().Should().Be("Validated");
        respuesta.GetProperty("obligationNumber").GetString().Should().Be(numero, "la obligación usa el número reservado");
        var credito = respuesta.GetProperty("obligationPublicId").GetGuid();
        pago.EnumerateObject().Select(p => p.Name).Should().NotContain(n => n.Contains("balance", StringComparison.OrdinalIgnoreCase),
            "Comercial nunca muestra saldos de Cartera");

        var detalle = await CarteraE2E.GetAsync(http, esc.Admin, $"{Creditos}/{credito}");
        detalle.GetProperty("number").GetString().Should().Be(numero);
        detalle.GetProperty("origin").GetString().Should().Be("Commercial");
        detalle.GetProperty("invoiceReference").GetString().Should().NotBeNullOrWhiteSpace("la obligación lleva la factura de la venta");
        (await CarteraE2E.GetAsync(http, esc.Admin, $"{Reservas}?number={numero}")).GetProperty("items").EnumerateArray().Single()
            .GetProperty("status").GetString().Should().Be("Used");
        var mensaje = (await CreditoDeLaVentaAsync(http, esc.Cajero, venta)).GetProperty("lendingMessages").EnumerateArray().Single();
        mensaje.GetProperty("validation").GetProperty("status").GetString().Should().Be("Validated");
        var recibo = await CarteraE2E.GetAsync(http, esc.Admin, $"{Recibos}/{mensaje.GetProperty("messagePublicId").GetGuid()}");
        recibo.GetProperty("receipt").GetProperty("status").GetString().Should().Be("Processed");
        recibo.GetProperty("receipt").GetProperty("response").GetProperty("type").GetString().Should().Be("ObligacionCreada");

        // US6-7: el mismo mensaje otra vez (un reproceso) no crea otro crédito.
        var otra = await new ConductorDelDespachador(fx).EnviarComoProcesoAsync(esc.TenantPublicId, $"Mensaje:{mensaje.GetProperty("messagePublicId").GetGuid():N}",
            new ConsumeCommercialCreditMessageCommand([mensaje.GetProperty("messagePublicId").GetGuid()]));
        otra.IsSuccess.Should().BeTrue();
        otra.Value.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        (await EnLaBaseAsync(fx, esc, db => db.Loans.AsNoTracking().CountAsync(l => l.Number == numero))).Should().Be(1);
        (await EnLaBaseAsync(fx, esc, db => db.LendingObligationResponses.AsNoTracking()
            .CountAsync(r => r.ObligationNumber == numero))).Should().Be(1, "una respuesta por obligación");
    }

    [Fact]
    public async Task La_venta_provisional_que_falla_su_validacion_crea_la_obligacion_marcada_bloquea_y_se_confirma_por_excepcion()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        await DespacharAsync(fx, esc);

        // US6-4: la obligación nace marcada (la venta supera el cupo de Valeria a su fecha) y la venta sigue confirmada.
        var pago = await PagoACreditoAsync(http, esc.Cajero, esc.VentaDeValeria);
        var respuesta = pago.GetProperty("lendingObligation");
        respuesta.ValueKind.Should().Be(JsonValueKind.Object, $"Comercial guardó la respuesta: {pago}");
        respuesta.GetProperty("outcome").GetString().Should().Be("Created");
        respuesta.GetProperty("validationStatus").GetString().Should().Be("ValidationFailed");
        respuesta.GetProperty("reason").GetString().Should().NotBeNullOrWhiteSpace();
        (await InventarioE2E.GetAsync(http, esc.Cajero, $"/api/inventory/sales/documents/{esc.VentaDeValeria}")).GetProperty("status").GetInt32()
            .Should().Be(2, "la validación fallida no anula la venta");
        var credito = respuesta.GetProperty("obligationPublicId").GetGuid();
        var detalle = await CarteraE2E.GetAsync(http, esc.Admin, $"{Creditos}/{credito}");
        detalle.GetProperty("marks").GetProperty("validationFailed").GetBoolean().Should().BeTrue();
        detalle.GetProperty("validationFailure").GetProperty("reason").GetString().Should().NotBeNullOrWhiteSpace();
        var recibos = await CarteraE2E.GetAsync(http, esc.Admin, $"{Recibos}?status=ValidationFailed");
        recibos.GetProperty("items").EnumerateArray().Should().Contain(r => r.GetProperty("loan").ValueKind == JsonValueKind.Object
            && r.GetProperty("loan").GetProperty("publicId").GetGuid() == credito);

        // Bloquea crédito nuevo: la consulta de Comercial dice que no admite la venta.
        var evaluacion = await InventarioE2E.ExitoAsync(http, esc.Cajero, HttpMethod.Post, Evaluaciones,
            new { personPublicId = esc.Valeria, paymentMeansPublicId = esc.Ventas.Medio("CREDASOC"), amount = 100_000m });
        var cartera = evaluacion.GetProperty("lending");
        cartera.GetProperty("allowsSale").GetBoolean().Should().BeFalse($"la validación fallida bloquea crédito nuevo: {evaluacion}");
        var nueva = await CobrarACreditoAsync(http, esc.Ventas, esc.Cajero, esc.Sesion, esc.Valeria, 1, Credito(esc.Ventas, 100_000m));
        await InventarioE2E.FallaAsync(nueva.Cobro, "Inventory.Credit.LendingBlocked");
        await InventarioE2E.ExitoAsync(http, esc.Cajero, HttpMethod.Post, $"{Borradores}/{nueva.Venta}/discard", new { reason = "Crédito bloqueado" });

        // B8: confirmada por excepción, el bloqueo se levanta y Comercial la ve validada.
        var confirmar = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Creditos}/{credito}/validation-failure/confirm",
            new { reason = "La gerencia respalda la venta del almacén (e2e)" });
        confirmar.StatusCode.Should().Be(HttpStatusCode.OK, await confirmar.Content.ReadAsStringAsync());
        var confirmado = await CarteraE2E.LeerAsync(confirmar);
        confirmado.GetProperty("marks").GetProperty("validationFailed").GetBoolean().Should().BeFalse();
        confirmado.GetProperty("validationFailure").GetProperty("confirmedByExceptionAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        var mensaje = (await CreditoDeLaVentaAsync(http, esc.Cajero, esc.VentaDeValeria)).GetProperty("lendingMessages").EnumerateArray()
            .Single(m => m.GetProperty("type").GetString() == "VentaACreditoRegistrada");
        mensaje.GetProperty("validation").GetProperty("status").GetString().Should().Be("Validated", "confirmada por excepción");
        (await CarteraE2E.GetAsync(http, esc.Admin, $"{Recibos}/{mensaje.GetProperty("messagePublicId").GetGuid()}"))
            .GetProperty("receipt").GetProperty("status").GetString().Should().Be("ConfirmedByException");
    }

    [Fact]
    public async Task La_nota_credito_parcial_baja_el_capital_y_recalcula_el_plan()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (venta, cobro) = await CobrarACreditoAsync(http, esc.Ventas, esc.Cajero, esc.Sesion, esc.Ximena, 2, Credito(esc.Ventas, 200_000m));
        cobro.StatusCode.Should().Be(HttpStatusCode.OK, await cobro.Content.ReadAsStringAsync());
        await DespacharAsync(fx, esc);
        var credito = (await PagoACreditoAsync(http, esc.Cajero, venta)).GetProperty("lendingObligation").GetProperty("obligationPublicId").GetGuid();
        var versionesAntes = (await CarteraE2E.GetAsync(http, esc.Admin, $"{Creditos}/{credito}/schedule/versions")).GetArrayLength();

        // US6-5: la nota crédito de 50.000 reintegrada al mismo crédito.
        var original = await InventarioE2E.GetAsync(http, esc.Cajero, $"/api/inventory/sales/documents/{venta}");
        var linea = original.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid();
        object Nota(object[] reintegros) => new
        {
            originDocumentPublicId = venta, documentTypePublicId = esc.Ventas.TipoNv, reason = "Descuento posterior por averías", totalVoid = false, withReturn = false,
            lines = new[] { new { originLinePublicId = linea, amount = 50_000m } }, refunds = reintegros,
        };
        var nota = await InventarioE2E.BorradorAsync(http, esc.Cajero, "/api/inventory/sales/credit-notes", Nota([]));
        var idNota = nota.GetProperty("documentPublicId").GetGuid();
        var aReintegrar = nota.GetProperty("totals").GetProperty("amountDue").GetDecimal();
        await InventarioE2E.ExitoAsync(http, esc.Cajero, HttpMethod.Put, $"/api/inventory/sales/credit-notes/{idNota}",
            Nota([new { paymentMeansPublicId = esc.Ventas.Medio("CREDASOC"), amount = aReintegrar }]));
        var confirmada = await InventarioE2E.MandarAsync(http, esc.Cajero, HttpMethod.Post, $"/api/inventory/sales/credit-notes/{idNota}/confirm",
            new { expectedAmountDue = aReintegrar });
        confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await confirmada.Content.ReadAsStringAsync());
        await DespacharAsync(fx, esc);

        var ajuste = (await CreditoDeLaVentaAsync(http, esc.Cajero, venta)).GetProperty("lendingMessages").EnumerateArray()
            .Where(m => m.GetProperty("type").GetString() == "AjusteDeVentaACredito").ToList();
        ajuste.Should().ContainSingle("la nota sobre una venta a crédito ajusta el crédito");
        (await CarteraE2E.GetAsync(http, esc.Admin, $"{Recibos}/{ajuste[0].GetProperty("messagePublicId").GetGuid()}"))
            .GetProperty("receipt").GetProperty("status").GetString().Should().Be("Processed", "Cartera aplicó el ajuste");
        (await CarteraE2E.GetAsync(http, esc.Admin, $"{Creditos}/{credito}/schedule/versions")).GetArrayLength()
            .Should().BeGreaterThan(versionesAntes, "la nota recalcula el plan");
        var operaciones = await CarteraE2E.GetAsync(http, esc.Admin, $"{Creditos}/{credito}/operations");
        operaciones.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("kind").GetString())
            .Should().Contain("CommercialAdjustment", "la nota es una operación del crédito");
    }

    [Fact]
    public async Task Una_venta_no_convertible_queda_para_reproceso_y_marcada_no_reprocesable_Comercial_ve_el_rechazo()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        // Quincenal: Comercial la emite como «Biweekly», que no tiene equivalencia en Cartera.
        var (venta, cobro) = await CobrarACreditoAsync(http, esc.Ventas, esc.Cajero, esc.Sesion, esc.Rodrigo, 1, Credito(esc.Ventas, 100_000m, cuotas: 2, plazo: 30, cada: 15));
        cobro.StatusCode.Should().Be(HttpStatusCode.OK, await cobro.Content.ReadAsStringAsync());
        await DespacharAsync(fx, esc);

        var mensaje = (await CreditoDeLaVentaAsync(http, esc.Cajero, venta)).GetProperty("lendingMessages").EnumerateArray().Single();
        var id = mensaje.GetProperty("messagePublicId").GetGuid();
        var recibo = (await CarteraE2E.GetAsync(http, esc.Admin, $"{Recibos}/{id}")).GetProperty("receipt");
        recibo.GetProperty("status").GetString().Should().Be("Rejected", "no convertible: queda para reproceso");
        recibo.GetProperty("response").ValueKind.Should().Be(JsonValueKind.Null, "mientras sea reprocesable no se responde (C95)");
        (await PagoACreditoAsync(http, esc.Cajero, venta)).GetProperty("lendingObligation").ValueKind.Should().Be(JsonValueKind.Null);

        var marcar = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Recibos}/{id}/mark-not-retryable",
            new { reason = "La periodicidad quincenal no se financia (e2e)" });
        marcar.StatusCode.Should().Be(HttpStatusCode.OK, await marcar.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(marcar)).GetProperty("response").GetProperty("type").GetString().Should().Be("ObligacionRechazada");
        await DespacharAsync(fx, esc);

        var respuesta = (await PagoACreditoAsync(http, esc.Cajero, venta)).GetProperty("lendingObligation");
        respuesta.GetProperty("outcome").GetString().Should().Be("Rejected");
        respuesta.GetProperty("reasonCode").GetString().Should().Be("NoConvertible");
        respuesta.GetProperty("retryable").GetBoolean().Should().BeFalse();
        respuesta.GetProperty("obligationNumber").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Un_medio_CreditWithoutLending_no_consulta_no_reserva_ni_llega_a_Cartera()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var medio = await MedioSinCarteraAsync(http, esc);

        var evaluacion = await InventarioE2E.ExitoAsync(http, esc.Cajero, HttpMethod.Post, Evaluaciones,
            new { personPublicId = esc.Ximena, paymentMeansPublicId = medio, amount = 100_000m });
        evaluacion.GetProperty("origin").GetInt32().Should().Be(1, "ProvisionalCredit: no le pregunta a Cartera");
        evaluacion.GetProperty("lending").ValueKind.Should().Be(JsonValueKind.Null);

        var (venta, cobro) = await CobrarACreditoAsync(http, esc.Ventas, esc.Cajero, esc.Sesion, esc.Ximena, 1, new
        {
            paymentMeansPublicId = medio, amount = 100_000m, credit = new { installments = 1, termDays = 30, periodicityDays = 30 },
        });
        cobro.StatusCode.Should().Be(HttpStatusCode.OK, await cobro.Content.ReadAsStringAsync());
        var enAprobacion = await InventarioE2E.LeerAsync(cobro);
        if (enAprobacion.GetProperty("status").GetInt32() == 1)
            await AprobadaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Ventas.Supervisor.Token,
                enAprobacion.GetProperty("approvalRequestPublicId").GetGuid(), aprobar: true));
        var pago = await PagoACreditoAsync(http, esc.Cajero, venta);
        pago.GetProperty("reservedLoanNumber").ValueKind.Should().Be(JsonValueKind.Null, "no reserva número");
        (await ContabilidadDeInventarioE2E.MensajesAsync(http, esc.Admin, venta))
            .Should().NotContain(m => m.GetProperty("type").GetString() == "VentaACreditoRegistrada", "A6: no llega a Cartera");
    }

    /// <summary>El medio <c>CREDSERV</c> (crédito a asociados que no pasa a Cartera, D29), creado una vez por escenario.</summary>
    private static async Task<Guid> MedioSinCarteraAsync(HttpClient http, Escenario esc)
    {
        var existentes = await InventarioE2E.GetAsync(http, esc.Admin, "/api/core/payment-means");
        var ya = existentes.EnumerateArray().FirstOrDefault(m => m.GetProperty("code").GetString() == "CREDSERV");
        if (ya.ValueKind == JsonValueKind.Object) return ya.GetProperty("paymentMeansPublicId").GetGuid();
        var resp = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, "/api/core/payment-means", new
        {
            code = "CREDSERV", name = "Cuotas de servicios", displayOrder = 7, @class = "AssociateCredit", countMethod = "None", allowsPartial = true,
            dianPaymentMeansCode = "1", creditWithoutLending = true, validFrom = esc.Hoy.AddDays(-1).ToString("yyyy-MM-dd"),
            creditDefaults = new { termDays = 30, maxInstallments = 1, periodicityDays = 30, maxTermDays = 30 },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var cuerpo = await InventarioE2E.LeerAsync(resp);
        // La venta se contabiliza (validación previa): la cuenta por cobrar del medio, como la de CREDASOC.
        await ContabilidadDeInventarioE2E.ReglaAsync(http, esc.Admin, "Venta", "MedioDePago", IngenIA365ERP.API.IntegrationTests.Ventas.ContabilidadDeVentasE2E.CreditoAsociados,
            new DateOnly(esc.Hoy.Year, 1, 1), new { paymentMeansCode = "CREDSERV" });
        return cuerpo.ValueKind == JsonValueKind.Object ? cuerpo.GetProperty("paymentMeansPublicId").GetGuid() : cuerpo.GetGuid();
    }
}
