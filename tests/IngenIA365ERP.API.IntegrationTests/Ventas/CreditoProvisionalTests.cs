using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using static IngenIA365ERP.API.IntegrationTests.Ventas.EscenarioDeVentas;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// T650 (feature 012, I3; US6-6, SC-023 primera mitad, SC-002; quickstart §5.11; contracts/api.md §23), en el motor de
/// <c>DB_PROVIDER</c>, en la cooperativa aislada «credito» con la contabilidad iniciada (<see cref="ContabilidadDeVentasE2E"/>): la venta
/// de 300.000 con <c>CREDASOC</c> al asociado X desde el POS y desde la factura de oficina queda en aprobación, ni el cajero ni
/// <c>cajero.2</c> (límite 100.000) la aprueban y el supervisor (1.000.000) sí; el POS la ve confirmada al consultar el borrador; al
/// consumidor final y al asociado retirado Z el crédito se rechaza; el comprobante <c>FV</c> debita la cuenta de <c>CREDASOC</c> con el
/// tercero X y el cruce <c>FV</c> + número y la vista <c>pending-documents</c> la muestra; <c>/credit</c> dice que el destino todavía no
/// está (IC); toda venta a crédito deja su «VentaACreditoRegistrada» pendiente, sin intentos ni alertas; la nota crédito agrega un
/// «AjusteDeVentaACredito» dependiente del original; y el cobro repetido tres veces con la misma clave no duplica mensajes. Lo que
/// depende de Cartera (IC, T661–T667) no se prueba aquí. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CreditoProvisionalTests(CentralIdentityApiFixture fx)
{
    private const string Evaluaciones = "/api/inventory/sales/credit-evaluations";

    [Fact]
    public async Task La_venta_a_credito_provisional_se_aprueba_se_contabiliza_y_deja_sus_mensajes_para_Cartera_pendientes()
    {
        var esc = await ContabilidadDeVentasE2E.PrepararAsync(fx, "credito");
        using var http = fx.CreateClient();
        var cajero = esc.Cajero1.Token;
        var coop = esc.Coop.TenantPublicId;
        var sesion = await SesionAsync(http, cajero, esc.Caja1);
        var x = await ContabilidadDeVentasE2E.AsociadoAsync(http, esc.Admin, "Xiomara", "71000001");
        var z = await ContabilidadDeVentasE2E.AsociadoAsync(http, esc.Admin, "Zacarias", "71000002");
        var hoy = InventarioE2E.HoyEnColombia;
        await InventarioE2E.SqlEnLaCooperativaAsync(fx, esc.Coop,
            $"""UPDATE dbo."COR_Associates" SET "Status" = 'R', "WithdrawalDate" = '{hoy.AddDays(-10):yyyy-MM-dd}' WHERE "PersonId" = (SELECT "Id" FROM dbo."COR_People" WHERE "PublicId" = '{z}')""",
            $"UPDATE [dbo].[COR_Associates] SET [Status] = 'R', [WithdrawalDate] = '{hoy.AddDays(-10):yyyy-MM-dd}' WHERE [PersonId] = (SELECT [Id] FROM [dbo].[COR_People] WHERE [PublicId] = '{z}')");

        // (1) La evaluación: sin Cartera, crédito provisional con aprobación; Z no es elegible.
        var evaluacion = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Post, Evaluaciones,
            new { personPublicId = x, paymentMeansPublicId = esc.Medio("CREDASOC"), amount = 300_000m });
        evaluacion.GetProperty("lendingEnabled").GetBoolean().Should().BeFalse();
        evaluacion.GetProperty("requiresApproval").GetBoolean().Should().BeTrue();
        evaluacion.GetProperty("person").GetProperty("eligible").GetBoolean().Should().BeTrue();
        var deZ = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Post, Evaluaciones,
            new { personPublicId = z, paymentMeansPublicId = esc.Medio("CREDASOC"), amount = 300_000m });
        deZ.GetProperty("person").GetProperty("eligible").GetBoolean().Should().BeFalse("el asociado retirado no es elegible");
        deZ.GetProperty("person").GetProperty("reasons").EnumerateArray().Select(r => r.GetProperty("code").GetString())
            .Should().Contain("Inventory.Credit.NotAssociate");

        // (2) Al consumidor final y a Z el crédito se rechaza al cobrar.
        object Credito(decimal valor) => new
        {
            paymentMeansPublicId = esc.Medio("CREDASOC"), amount = valor, credit = new { installments = 3, termDays = 90, periodicityDays = 30 },
        };
        var anonima = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P7").CodigoDeBarras);
        var alFinal = await PedirCobrarAsync(http, cajero, VentaId(anonima), 100_000m, [Credito(100_000m)]);
        ((int)alFinal.StatusCode).Should().Be(422, await alFinal.Content.ReadAsStringAsync());
        (await InventarioE2E.CodigoDeErrorAsync(alFinal)).Should().BeOneOf("Payments.MeansNotAvailable", "Inventory.Credit.PersonNotIdentified");
        await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch, $"{Borradores}/{VentaId(anonima)}", new { customerPersonPublicId = z });
        var aZ = await PedirCobrarAsync(http, cajero, VentaId(anonima), 100_000m, [Credito(100_000m)]);
        ((int)aZ.StatusCode).Should().Be(422, await aZ.Content.ReadAsStringAsync());
        (await InventarioE2E.CodigoDeErrorAsync(aZ)).Should().BeOneOf("Inventory.Credit.NotAssociate", "Payments.MeansNotAvailable");
        await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Post, $"{Borradores}/{VentaId(anonima)}/discard", new { reason = "Crédito rechazado" });

        // (3) En el POS: 300.000 a X, cobrado tres veces con la misma clave → una sola venta en aprobación, sin número.
        var venta = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P7").CodigoDeBarras);
        var id = VentaId(venta);
        venta = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch, $"{Borradores}/{id}", new { customerPersonPublicId = x });
        venta = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch, $"{Borradores}/{id}/lines/{venta.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid()}",
            new { quantity = 3m });
        venta.GetProperty("availablePaymentMeans").EnumerateArray().Select(m => m.GetProperty("code").GetString()).Should().Contain("CREDASOC",
            "con cliente identificado y SellOnCredit se ofrece el crédito");
        var clave = Guid.NewGuid();
        var cobros = new List<JsonElement>();
        for (var i = 0; i < 3; i++)
        {
            var r = await PedirCobrarAsync(http, cajero, id, 300_000m, [Credito(300_000m)], clave);
            r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            cobros.Add(await InventarioE2E.LeerAsync(r));
        }
        cobros.Should().OnlyContain(c => c.GetProperty("status").GetInt32() == 1, "PendingApproval: el crédito provisional siempre se aprueba");
        var solicitud = cobros[0].GetProperty("approvalRequestPublicId").GetGuid();
        cobros.Select(c => c.GetProperty("approvalRequestPublicId").GetGuid()).Distinct().Should().ContainSingle();
        cobros[0].GetProperty("number").ValueKind.Should().Be(JsonValueKind.Null, "sin número hasta la última aprobación");

        // (4) Ni el cajero ni cajero.2 (límite 100.000) la aprueban; el supervisor (1.000.000) sí, y el POS la ve confirmada.
        (await InventarioE2E.DecidirAsync(http, esc.Admin, cajero, solicitud, aprobar: true)).IsSuccessStatusCode.Should().BeFalse("quien cobra no aprueba");
        (await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Cajero2.Token, solicitud, aprobar: true)).IsSuccessStatusCode.Should().BeFalse(
            "su monto máximo de SellOnCredit no alcanza");
        await AprobadaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Supervisor.Token, solicitud, aprobar: true));
        var estado = 0;
        for (var i = 0; i < 10 && estado != 2; i++)
        {
            estado = (await InventarioE2E.GetAsync(http, cajero, $"{Borradores}/{id}")).GetProperty("status").GetInt32();
            if (estado != 2) await Task.Delay(TimeSpan.FromSeconds(2));
        }
        estado.Should().Be(2, "la última aprobación la confirma: el POS consulta el borrador cada 2 s hasta Confirmed");

        // (5) /credit: pago pendiente de validar, sello de Contabilidad y el mensaje para Cartera pendiente sin destino (IC).
        var credito = await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{id}/credit");
        var pago = credito.GetProperty("payments").EnumerateArray().Single();
        pago.GetProperty("pendingValidation").GetBoolean().Should().BeTrue();
        pago.GetProperty("installments").GetInt32().Should().Be(3);
        credito.GetProperty("accountsReceivableRecordedBy").ToString().Should().Be("Contabilidad");
        var paraCartera = credito.GetProperty("lendingMessages").EnumerateArray().Single();
        paraCartera.GetProperty("destinationAvailable").GetBoolean().Should().BeFalse();
        paraCartera.GetProperty("validation").GetProperty("status").GetString().Should().Be("Pending");

        // (6) Desde la factura de oficina: la misma regla.
        var oficina = await InventarioE2E.BorradorAsync(http, cajero, "/api/inventory/sales/invoices", new
        {
            documentTypePublicId = esc.TipoRv, warehousePublicId = esc.Inv.Bodega("PV1"), counterpartyPersonPublicId = x,
            lines = new[] { new { productPublicId = esc.Inv.P("P7").Id, unitPublicId = esc.Inv.P("P7").Unidad, quantity = 3m } },
            payments = new[] { Credito(300_000m) },
        });
        var idOficina = oficina.GetProperty("documentPublicId").GetGuid();
        var confirmar = await InventarioE2E.MandarAsync(http, cajero, HttpMethod.Post, $"/api/inventory/sales/invoices/{idOficina}/confirm", new { expectedAmountDue = 300_000m });
        confirmar.StatusCode.Should().Be(HttpStatusCode.OK, await confirmar.Content.ReadAsStringAsync());
        var enAprobacion = await InventarioE2E.LeerAsync(confirmar);
        enAprobacion.GetProperty("status").GetInt32().Should().Be(1);
        await AprobadaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Supervisor.Token, enAprobacion.GetProperty("approval").GetProperty("requestPublicId").GetGuid(),
            aprobar: true));
        (await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{idOficina}")).GetProperty("status").GetInt32().Should().Be(2);

        // (7) SC-023 primera mitad y SC-002: una «VentaACreditoRegistrada» por venta, pendiente, sin intentos ni alertas.
        foreach (var documento in new[] { id, idOficina })
        {
            var mensajes = (await ContabilidadDeInventarioE2E.MensajesAsync(http, esc.Admin, documento))
                .Where(m => m.GetProperty("type").GetString() == "VentaACreditoRegistrada").ToList();
            mensajes.Should().ContainSingle($"el documento {documento} tiene su mensaje para Cartera, uno solo aunque el cobro se repitió");
            ContabilidadDeInventarioE2E.Estado(mensajes[0]).Should().Be(0, "Pending: el destino aún no está disponible (IC)");
        }
        await fx.Despachador.PasadasAsync(coop, 2);
        var intentos = await ContabilidadDeInventarioE2E.EnteroAsync(fx, esc.Inv,
            """SELECT COUNT(*) FROM dbo."COR_IntegrationDeliveryAttempts" a JOIN dbo."COR_IntegrationMessageDeliveries" d ON d."Id" = a."DeliveryId" WHERE d."Destination" = 'Lending'""",
            "SELECT COUNT(*) FROM [dbo].[COR_IntegrationDeliveryAttempts] a JOIN [dbo].[COR_IntegrationMessageDeliveries] d ON d.[Id] = a.[DeliveryId] WHERE d.[Destination] = 'Lending'");
        intentos.Should().Be(0, "el despachador salta lo de Cartera sin intentos");
        var alertas = await ContabilidadDeInventarioE2E.EnteroAsync(fx, esc.Inv,
            """SELECT COUNT(*) FROM dbo."COR_Alerts" WHERE "TypeCode" LIKE 'Integracion.%'""",
            "SELECT COUNT(*) FROM [dbo].[COR_Alerts] WHERE [TypeCode] LIKE 'Integracion.%'");
        alertas.Should().Be(0, "ni alertas");

        // (8) Con I2: el FV debita la cuenta de CREDASOC con el tercero X y el cruce FV + número; pending-documents la muestra.
        var facturada = await ContabilidadDeInventarioE2E.MensajeAsync(http, esc.Admin, id, "VentaFacturada");
        ContabilidadDeInventarioE2E.Estado(facturada).Should().Be(2, $"la venta a crédito se contabiliza: {facturada}");
        ContabilidadDeInventarioE2E.TipoDeComprobante(facturada).Should().Be("FV");
        var lineas = await ContabilidadDeVentasE2E.LineasDelComprobanteAsync(http, esc.Admin, ContabilidadDeInventarioE2E.Comprobante(facturada)!.Value);
        var porCobrar = lineas.Single(l => ContabilidadDeVentasE2E.Cuenta(l) == ContabilidadDeVentasE2E.CreditoAsociados);
        ContabilidadDeVentasE2E.Debito(porCobrar).Should().Be(300_000m);
        porCobrar.GetProperty("personPublicId").GetGuid().Should().Be(x);
        porCobrar.GetProperty("crossDocumentType").GetString().Should().Be("FV");
        var numero = (await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{id}")).GetProperty("number").GetInt64();
        porCobrar.GetProperty("crossDocumentNumber").GetString().Should().Contain(numero.ToString());
        var pendientes = await ContabilidadE2E.InformeAsync(http, esc.Admin, $"pending-documents?person={x}");
        pendientes.Filas.Should().Contain(f => ContabilidadE2E.Tabla.Texto(f, 0) == ContabilidadDeVentasE2E.CreditoAsociados,
            "la cartera provisional por persona se ve en los documentos pendientes");

        // (9) Una nota crédito sobre esa venta agrega «AjusteDeVentaACredito», pendiente y dependiente del original.
        var original = await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{id}");
        var linea = original.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid();
        object Nota(object[] reintegros) => new
        {
            originDocumentPublicId = id, documentTypePublicId = esc.TipoNv, reason = "Descuento posterior por averías", totalVoid = false, withReturn = false,
            lines = new[] { new { originLinePublicId = linea, amount = 50_000m } }, refunds = reintegros,
        };
        var nota = await InventarioE2E.BorradorAsync(http, cajero, "/api/inventory/sales/credit-notes", Nota([]));
        var idNota = nota.GetProperty("documentPublicId").GetGuid();
        var aReintegrar = nota.GetProperty("totals").GetProperty("amountDue").GetDecimal();
        await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Put, $"/api/inventory/sales/credit-notes/{idNota}",
            Nota([new { paymentMeansPublicId = esc.Medio("CREDASOC"), amount = aReintegrar }]));
        var confirmada = await InventarioE2E.MandarAsync(http, cajero, HttpMethod.Post, $"/api/inventory/sales/credit-notes/{idNota}/confirm",
            new { expectedAmountDue = aReintegrar });
        confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await confirmada.Content.ReadAsStringAsync());
        var ajuste = (await ContabilidadDeInventarioE2E.MensajesAsync(http, esc.Admin, idNota))
            .Where(m => m.GetProperty("type").GetString() == "AjusteDeVentaACredito").ToList();
        ajuste.Should().ContainSingle("la nota sobre una venta a crédito ajusta el crédito hacia Cartera");
        ContabilidadDeInventarioE2E.Estado(ajuste[0]).Should().Be(0);
        var dependencias = await ContabilidadDeInventarioE2E.EnteroAsync(fx, esc.Inv,
            $"""SELECT COUNT(*) FROM dbo."COR_IntegrationMessageDependencies" x JOIN dbo."COR_IntegrationMessages" m ON m."Id" = x."MessageId" WHERE m."PublicId" = '{ajuste[0].GetProperty("messagePublicId").GetGuid()}'""",
            $"SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessageDependencies] x JOIN [dbo].[COR_IntegrationMessages] m ON m.[Id] = x.[MessageId] WHERE m.[PublicId] = '{ajuste[0].GetProperty("messagePublicId").GetGuid()}'");
        dependencias.Should().BeGreaterThan(0, "depende del mensaje original");
    }
}
