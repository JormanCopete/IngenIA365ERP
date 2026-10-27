using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, US19, T0224 (US19 «Independent Test»; quickstart.md recorridos H paso 2 e I paso 1; FR-035, FR-105, FR-113, FR-134 a FR-136, FR-219): dos órdenes de
/// forma cheque que llegan de Cartera salen numeradas de la chequera con su hecho <c>ChequeGiradoEmitido</c>; entre una y otra se anula por dañado el cheque en blanco
/// siguiente, y la numeración no tiene huecos sin explicar; el primero se entrega con su constancia y su orden queda «girada». Después, la remesa de efectivo de la
/// bóveda al banco con doble custodia, acta y sello de bolsa: hecho <c>RemesaDespachada</c>, queda en tránsito y la bóveda baja lo que salió. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ChequesGiradosYRemesasTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_cheque_sale_numerado_el_danado_explica_su_numero_la_entrega_lo_deja_girado_y_la_remesa_queda_en_transito()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "cheqrem");
        var esc = t.Escenario;
        var jefe = esc.Usuarios.Jefe.Token;
        await TesoreriaE2E.AprobadorDeTesoreriaAsync(http, esc);

        // Dos desembolsos de Cartera por cheque: las órdenes llegan listas, de forma cheque.
        var ordenA = await ChequesYRemesasE2E.OrdenPorChequeAsync(fx, http, esc, 1_150_000m);
        var ordenB = await ChequesYRemesasE2E.OrdenPorChequeAsync(fx, http, esc, 1_270_000m);
        foreach (var o in new[] { ordenA, ordenB })
        {
            o.GetProperty("form").GetInt32().Should().Be((int)PaymentForm.Check);
            o.GetProperty("status").GetInt32().Should().Be((int)PaymentOrderState.Ready, o.GetRawText());
        }

        var chequera = await ChequesYRemesasE2E.ChequeraAsync(http, t.Admin, t.CuentaDePagos, 700_001, 700_050);

        // 1. El cheque de A sale con el primer número de la chequera y nace su ChequeGiradoEmitido.
        var chequeA = (await ChequesYRemesasE2E.EmitirAsync(http, t.Admin, jefe, t.CuentaDePagos, chequera, [Id(ordenA)])).Should().ContainSingle().Subject;
        chequeA.GetProperty("checkNumber").GetInt64().Should().Be(700_001);
        chequeA.GetProperty("status").GetInt32().Should().Be((int)IssuedCheckState.Issued);
        chequeA.GetProperty("amount").GetDecimal().Should().Be(1_150_000m);
        (await ChequesYRemesasE2E.MensajesAsync(fx, esc, ChequeGiradoEmitidoV1.Type, Id(chequeA))).Should().Be(1, "el hecho nace al emitir (FR-136)");
        (await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/payment-orders/{Id(ordenA)}")).GetProperty("status").GetInt32()
            .Should().Be((int)PaymentOrderState.Sent, "emitido, todavía no entregado");

        // 2. El cheque en blanco siguiente se dañó: se anula con su número y su motivo.
        var danado = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/checkbooks/{chequera}/spoiled",
            new { reason = "Se manchó al imprimir (e2e)" });
        danado.GetProperty("checkNumber").GetInt64().Should().Be(700_002);
        danado.GetProperty("status").GetInt32().Should().Be((int)IssuedCheckState.SpoiledBlank);

        // 3. El cheque de B toma el número que sigue: el dañado no se reutiliza.
        var chequeB = (await ChequesYRemesasE2E.EmitirAsync(http, t.Admin, jefe, t.CuentaDePagos, chequera, [Id(ordenB)])).Should().ContainSingle().Subject;
        chequeB.GetProperty("checkNumber").GetInt64().Should().Be(700_003);

        // La numeración no tiene huecos sin explicar: cada número usado de la chequera es un cheque girado o un dañado con su motivo.
        var talonario = (await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/checkbooks?account={t.CuentaDePagos}")).EnumerateArray()
            .Single(k => Id(k) == chequera);
        talonario.GetProperty("nextNumber").GetInt64().Should().Be(700_004);
        talonario.GetProperty("remaining").GetInt32().Should().Be(47);
        var cheques = (await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/issued-checks?checkbook={chequera}")).EnumerateArray().ToList();
        cheques.Select(c => c.GetProperty("checkNumber").GetInt64()).Order().Should().Equal(700_001, 700_002, 700_003);
        cheques.Where(c => c.GetProperty("status").GetInt32() == (int)IssuedCheckState.SpoiledBlank).Should().ContainSingle()
            .Which.GetProperty("voidReason").GetString().Should().NotBeNullOrWhiteSpace("el dañado explica su número");
        // Cada emisión queda en la auditoría con su nombre (hasta esta e2e se auditaba como «Treasury.Checks.Printd»).
        await fx.ReenviarAuditoriaAsync(esc.Coop.TenantPublicId);
        await fx.Factory.Services.GetRequiredService<IAuditService>().FlushAsync();
        var emisiones = await CarteraE2E.GetAsync(http, t.Admin,
            $"/api/audit/logs?action=Treasury.Checks.Issued&from={DateTime.UtcNow.AddHours(-1):O}&to={DateTime.UtcNow.AddMinutes(1):O}&pageSize=100");
        emisiones.GetProperty("items").GetArrayLength().Should().Be(2, "una por lote emitido");

        // 4. Entregar A con la constancia firmada: el cheque queda entregado y la orden «girada», con su resultado.
        var entregado = await ChequesYRemesasE2E.EntregarAsync(http, t.Admin, Id(chequeA), esc.Hoy);
        entregado.GetProperty("status").GetInt32().Should().Be((int)IssuedCheckState.Delivered);
        entregado.GetProperty("deliveredOn").GetString().Should().Be(esc.Hoy.ToString("yyyy-MM-dd"));
        (await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/payment-orders/{Id(ordenA)}")).GetProperty("status").GetInt32()
            .Should().Be((int)PaymentOrderState.Disbursed, "la constancia de entrega informa «girada» (FR-113)");
        var resultados = await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/payment-orders/{Id(ordenA)}/results");
        resultados.GetArrayLength().Should().Be(1);
        (await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/payment-orders/{Id(ordenB)}")).GetProperty("status").GetInt32()
            .Should().Be((int)PaymentOrderState.Sent, "B sigue emitido sin entregar");

        // 5. La remesa de la bóveda al banco: sin segundo custodio, la doble custodia la rechaza; con él, acta y sello, queda en tránsito.
        var boveda = await ChequesYRemesasE2E.BovedaConEfectivoAsync(http, esc, 20);
        var acta = await ChequesYRemesasE2E.ActaAsync(http, t.Admin, boveda.Boveda);
        var sinCustodio = await InventarioE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/receipts/vaults/{boveda.Boveda}/remittances", new
        {
            ownBankAccountPublicId = t.CuentaDePagos, denominations = new[] { new { denominationPublicId = boveda.Billete, quantity = 10 } }, bagSeal = "SELLO-E2E-0001",
            minutesAttachmentPublicId = acta, reason = "Remesa al banco (e2e)",
        });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinCustodio)).Should().Be("Receipts.Movement.DoubleCustodyRequired");

        var despacho = await ChequesYRemesasE2E.RemesarAsync(http, esc, boveda, t.CuentaDePagos, acta, 10, "SELLO-E2E-0001");
        var remesaId = despacho.GetProperty("remittancePublicId").GetGuid();
        despacho.GetProperty("movement").GetProperty("amount").GetDecimal().Should().Be(-500_000m);
        despacho.GetProperty("movement").GetProperty("bagSeal").GetString().Should().Be("SELLO-E2E-0001");

        var remesa = (await CarteraE2E.GetAsync(http, t.Admin, "/api/treasury/remittances?state=InTransit")).EnumerateArray().Single(r => Id(r) == remesaId);
        remesa.GetProperty("status").GetInt32().Should().Be((int)RemittanceState.InTransit);
        remesa.GetProperty("direction").GetInt32().Should().Be((int)RemittanceDirection.VaultToBank);
        remesa.GetProperty("amount").GetDecimal().Should().Be(500_000m);
        remesa.GetProperty("bagSeal").GetString().Should().Be("SELLO-E2E-0001");
        remesa.GetProperty("minutesAttachmentPublicId").GetGuid().Should().Be(acta);
        remesa.GetProperty("bankAccountPublicId").GetGuid().Should().Be(t.CuentaDePagos);
        remesa.GetProperty("vaultPublicId").GetGuid().Should().Be(boveda.Boveda);
        (await ChequesYRemesasE2E.MensajesAsync(fx, esc, RemesaDespachadaV1.Type)).Should().Be(1, "Recaudo emite RemesaDespachada en la misma transacción (FR-105)");

        var detalle = await CarteraE2E.GetAsync(http, t.Admin, $"/api/receipts/vaults/{boveda.Boveda}");
        detalle.GetProperty("balance").GetDecimal().Should().Be(500_000m, "entró 1.000.000 y salieron 500.000 en la remesa");
    }

    private static Guid Id(JsonElement e) => e.GetProperty("publicId").GetGuid();
}

/// <summary>
/// Los ayudantes de las e2e de cheques girados, remesas y conciliación de Tesorería (feature 015, T0224 y T0274): la orden de forma cheque que llega de Cartera, la
/// chequera, la emisión de un lote de cheques aprobado por otra persona, la entrega con constancia, la bóveda con efectivo y su remesa con doble custodia, el rol del
/// conciliador y el conteo de los hechos que quedaron en la bandeja de la cooperativa. (nuevo)
/// </summary>
public static class ChequesYRemesasE2E
{
    public sealed record BovedaConBillete(Guid Boveda, Guid Billete, Guid SegundoCustodio);

    /// <summary>Cartera desembolsa por cheque y el despachador entrega la orden a Tesorería; devuelve la orden de pago de Tesorería por su valor neto.</summary>
    public static async Task<JsonElement> OrdenPorChequeAsync(CentralIdentityApiFixture fx, HttpClient http, DesembolsoE2E.Escenario esc, decimal monto)
    {
        await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, $"Q{Guid.NewGuid():N}"[..8], monto);
        await CarteraE2E.DespacharAsync(fx, esc.Coop.TenantPublicId, 3);
        var ordenes = await CarteraE2E.GetAsync(http, esc.Coop.TokenAdmin, "/api/treasury/payment-orders");
        return ordenes.EnumerateArray().Single(o => o.GetProperty("netAmount").GetDecimal() == monto);
    }

    /// <summary>Registra la chequera de la cuenta con su rango; devuelve su <c>PublicId</c>.</summary>
    public static async Task<Guid> ChequeraAsync(HttpClient http, string token, Guid cuenta, long desde, long hasta) =>
        (await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, "/api/treasury/checkbooks",
            new { bankAccountPublicId = cuenta, fromNumber = desde, toNumber = hasta, custodianUserPublicId = (Guid?)null, custodyAttachmentPublicId = (Guid?)null }))
        .GetProperty("publicId").GetGuid();

    /// <summary>Un lote de forma «cheques» con esas órdenes, enviado a aprobación, aprobado por <paramref name="aprobador"/> y sus cheques emitidos de la chequera.</summary>
    public static async Task<List<JsonElement>> EmitirAsync(HttpClient http, string token, string aprobador, Guid cuenta, Guid chequera, Guid[] ordenes)
    {
        var lote = await TesoreriaE2E.LoteAsync(http, token, cuenta, ordenes, forma: 3);
        var id = lote.GetProperty("publicId").GetGuid();
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, token, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/submit"), "enviar el lote a aprobación");
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, aprobador, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/approve", new { }), "aprobar el lote");
        var emitidos = await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/treasury/issued-checks/batches/{id}",
            new { checkbookPublicId = chequera, crossed = true, firstBeneficiaryOnly = true });
        return emitidos.EnumerateArray().ToList();
    }

    /// <summary>Entrega el cheque con su constancia firmada (<c>TreasuryDeliveryReceipt</c>); devuelve el cheque.</summary>
    public static async Task<JsonElement> EntregarAsync(HttpClient http, string token, Guid cheque, DateOnly fecha)
    {
        var constancia = await AdjuntosE2E.SubirAsync(http, token, cheque, Encoding.UTF8.GetBytes("%PDF-1.4 constancia de entrega del cheque (e2e)"),
            "constancia.pdf", "application/pdf", "TreasuryDeliveryReceipt");
        return await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/treasury/issued-checks/{cheque}/deliver",
            new { deliveryReceiptAttachmentPublicId = constancia.Adjunto, deliveredOn = fecha });
    }

    /// <summary>
    /// La bóveda de la sucursal principal con dos custodios (el administrador y el jefe: la doble custodia exige dos personas) y <paramref name="billetes"/> de
    /// 50.000 recibidos con el segundo custodio.
    /// </summary>
    public static async Task<BovedaConBillete> BovedaConEfectivoAsync(HttpClient http, DesembolsoE2E.Escenario esc, int billetes)
    {
        var admin = esc.Coop.TokenAdmin;
        var boveda = (await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, "/api/receipts/vaults",
            new { branchPublicId = esc.Coop.SucursalPrincipal, mode = "Vault" }))).GetProperty("publicId").GetGuid();
        var administrador = await CarteraE2E.UsuarioPorCorreoAsync(http, admin, esc.Coop.CorreoAdmin!);
        var segundo = esc.Usuarios.Jefe.PublicId;
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, $"/api/receipts/vaults/{boveda}/custodians",
            new { userPublicIds = new[] { administrador, segundo } }), "los custodios de la bóveda");
        var billete = await CajaE2E.BilleteAsync(http, admin, 50_000m);
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, $"/api/receipts/vaults/{boveda}/movements", new
        {
            kind = "RemittanceIn", denominations = new[] { new { denominationPublicId = billete, quantity = billetes } }, reason = "Efectivo recibido del banco (e2e)",
            secondCustodianUserPublicId = segundo,
        }), "el efectivo que entra a la bóveda");
        return new BovedaConBillete(boveda, billete, segundo);
    }

    /// <summary>El acta de la remesa, subida con la bóveda como dueña (<c>ReceiptsCashSupport</c>).</summary>
    public static async Task<Guid> ActaAsync(HttpClient http, string token, Guid boveda) =>
        (await AdjuntosE2E.SubirAsync(http, token, boveda, Encoding.UTF8.GetBytes("%PDF-1.4 acta de la remesa (e2e)"), "acta-remesa.pdf", "application/pdf",
            "ReceiptsCashSupport")).Adjunto;

    /// <summary>Despacha <paramref name="billetes"/> de 50.000 de la bóveda a la cuenta con el segundo custodio, el acta y el sello; devuelve la remesa despachada.</summary>
    public static async Task<JsonElement> RemesarAsync(HttpClient http, DesembolsoE2E.Escenario esc, BovedaConBillete boveda, Guid cuenta, Guid acta, int billetes,
        string sello) =>
        await InventarioE2E.ExitoAsync(http, esc.Coop.TokenAdmin, HttpMethod.Post, $"/api/receipts/vaults/{boveda.Boveda}/remittances", new
        {
            ownBankAccountPublicId = cuenta, denominations = new[] { new { denominationPublicId = boveda.Billete, quantity = billetes } }, bagSeal = sello,
            minutesAttachmentPublicId = acta, secondCustodianUserPublicId = boveda.SegundoCustodio, reason = "Remesa al banco (e2e)",
        });

    /// <summary>Cuántos hechos de ese tipo hay en la bandeja de la cooperativa (los de un origen, si se indica).</summary>
    public static Task<int> MensajesAsync(CentralIdentityApiFixture fx, DesembolsoE2E.Escenario esc, string tipo, Guid? origen = null) =>
        DesembolsoE2E.EnLaBaseAsync(fx, esc, db => db.IntegrationMessages.AsNoTracking()
            .CountAsync(m => m.Type == tipo && (origen == null || m.OriginPublicId == origen)));

    private static readonly Dictionary<Guid, Task> Conciliadores = [];

    /// <summary>
    /// El usuario «pruebas» con el rol de la plantilla <c>tesoreria.conciliador</c> más <c>Treasury.Scope.All</c> (una vez por cooperativa): no preparó ni aprobó pagos,
    /// así que puede conciliar la cuenta (FR-217).
    /// </summary>
    public static Task ConciliadorAsync(HttpClient http, DesembolsoE2E.Escenario esc)
    {
        lock (Conciliadores)
        {
            if (!Conciliadores.TryGetValue(esc.Coop.TenantPublicId, out var tarea))
            {
                tarea = DarAsync(http, esc);
                Conciliadores[esc.Coop.TenantPublicId] = tarea;
            }
            return tarea;
        }

        static async Task DarAsync(HttpClient http, DesembolsoE2E.Escenario esc)
        {
            var admin = esc.Coop.TokenAdmin;
            const string nombre = "Conciliador bancario (e2e)";
            var creado = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/admin/roles/from-template",
                new { templateKey = "tesoreria.conciliador", code = "TCON", name = nombre });
            var rol = creado.GetProperty("rolePublicId").GetGuid();
            var catalogo = (await CarteraE2E.GetAsync(http, admin, "/api/admin/permissions")).EnumerateArray()
                .ToDictionary(p => p.GetProperty("code").GetString()!, p => p.GetProperty("publicId").GetGuid(), StringComparer.OrdinalIgnoreCase);
            var permisos = (await CarteraE2E.GetAsync(http, admin, $"/api/admin/roles/{rol}")).GetProperty("permissions").EnumerateArray()
                .Select(p => p.GetProperty("code").GetString()!).Append("Treasury.Scope.All").Distinct(StringComparer.OrdinalIgnoreCase).Select(c => catalogo[c]).ToList();
            var put = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/admin/roles/{rol}",
                new { name = nombre, description = (string?)null, isAssignable = true, permissionPublicIds = permisos });
            put.IsSuccessStatusCode.Should().BeTrue($"el alcance total del conciliador: {await put.Content.ReadAsStringAsync()}");
            await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{esc.Usuarios.Pruebas.PublicId}/roles", new { rolePublicId = rol });
        }
    }
}
