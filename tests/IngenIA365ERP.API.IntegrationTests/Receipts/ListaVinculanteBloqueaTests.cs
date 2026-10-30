using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Treasury;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0165 (parte de Tesorería; FR-203; SC-025): un beneficiario en lista vinculante no entra a un lote aprobado: su orden queda pendiente con
/// <c>BindingListMatch</c> y la alerta <c>Tesoreria.ListaVinculante</c> sale de inmediato. El canal de listas es el simulado (documento terminado en 9 = coincidencia
/// exacta). La parte de Recaudo (T0237; quickstart recorrido J paso 4): el pagador en lista vinculante en la ventanilla deja la operación bloqueada desde que nace —no
/// se le ponen medios, no se confirma, la sesión no recibe su dinero— y el oficial recibe la alerta <c>Recaudo.ListaVinculante</c> de inmediato; quien no tiene
/// <c>Receipts.Compliance.Release</c> ni ve la ruta de liberar (404, FR-017).
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ListaVinculanteBloqueaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_beneficiario_en_lista_vinculante_no_entra_a_un_lote_aprobado()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "listas");
        var orden = await TesoreriaE2E.OrdenDeDesembolsoAsync(fx, http, t, 1_900_000m);
        var ordenId = orden.GetProperty("publicId").GetGuid();
        // Las listas de la cooperativa por el canal simulado (sin configurar, el vigente es CARGA-PROPIA y nada coincide; corrida del 2026-10-26).
        await CarteraE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, "/api/core/external-channels", new
        {
            kind = "RestrictiveLists", channelCode = "SIMULADO", validFrom = t.Escenario.Hoy, reason = "Listas simuladas para la orden de pago (e2e)",
        });
        await DocumentoEnListaAsync(http, t.Admin, orden.GetProperty("beneficiaryPersonPublicId").GetGuid());

        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-orders/{ordenId}/revalidate"), "revalidar");
        var revalidada = await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/payment-orders/{ordenId}");
        revalidada.GetProperty("status").GetInt32().Should().Be(2, "pendiente");
        revalidada.GetProperty("pendingReasons").EnumerateArray().Select(r => r.GetString()).Should().Contain(IngenIA365ERP.Application.Treasury.Common.ErroresDeTesoreria.CodigoListaVinculante, "los motivos llevan el código completo, como los demás");

        var lote = await InventarioE2E.MandarAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/payment-batches", new
        {
            bankAccountPublicId = t.CuentaDePagos, form = 1, payDate = DateOnly.FromDateTime(DateTime.Today), orderPublicIds = new[] { ordenId },
        });
        var texto = await lote.Content.ReadAsStringAsync();
        if (lote.IsSuccessStatusCode) JsonNode.Parse(texto)!["orderCount"]!.GetValue<int>().Should().Be(0, "cero órdenes del beneficiario en lista en el lote (SC-025)");
        // La bandeja común de alertas (contracts/api.md §16.1 de la 012); /api/alerts no existe.
        var alertas = await CarteraE2E.GetAsync(http, t.Admin, "/api/inventory/alerts?typeCode=Tesoreria.ListaVinculante");
        alertas.ToString().Should().Contain("Tesoreria.ListaVinculante");
    }

    [Fact]
    public async Task El_pagador_en_lista_vinculante_no_entrega_su_dinero_en_la_ventanilla()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "listacaja");
        var t = c.Token;
        await CarteraE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/core/external-channels", new
        {
            kind = "RestrictiveLists", channelCode = "SIMULADO", validFrom = c.Escenario.Hoy, reason = "Listas simuladas para la caja (e2e)",
        });
        var vetado = await CarteraE2E.PersonaAsync(http, t, "Vetado", "Ventanilla");
        await DocumentoEnListaAsync(http, t, vetado);

        var creada = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/receipts/cash-operations",
            new { sessionPublicId = c.Sesion, payerPersonPublicId = vetado });
        TesoreriaE2E.EsExito(creada, "la operación nace aunque quede bloqueada");
        var operacion = await RecaudoYTesoreriaE2E.LeerAsync(creada);
        var id = operacion.GetProperty("publicId").GetGuid();
        RecaudoYTesoreriaE2E.Enum<IngenIA365ERP.Domain.Enums.Receipts.CashOperationState>(operacion.GetProperty("status"))
            .Should().Be(IngenIA365ERP.Domain.Enums.Receipts.CashOperationState.Blocked, "la coincidencia exacta es vinculante: bloquea desde que nace la operación");

        // La alerta al oficial es inmediata: ya está antes de intentar nada más.
        var alertas = await CarteraE2E.GetAsync(http, t, "/api/inventory/alerts?typeCode=Recaudo.ListaVinculante");
        alertas.ToString().Should().Contain("Recaudo.ListaVinculante");

        var medios = await InventarioE2E.MandarAsync(http, t, HttpMethod.Put, $"/api/receipts/cash-operations/{id}/means",
            new { means = new[] { CajaE2E.Medio(c.Efectivo, 50_000m) } });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(medios)).Should().Be("Receipts.Compliance.BindingListMatch", "no se recibe su dinero");
        var confirmar = await CajaE2E.ConfirmarAsync(http, c, id, Guid.NewGuid());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(confirmar)).Should().Be("Receipts.Compliance.BindingListMatch");
        (await InventarioE2E.GetAsync(http, t, $"/api/receipts/receipts?session={c.Sesion}")).GetArrayLength().Should().Be(0, "la sesión no tiene recibos");

        // Liberar es del oficial: el asesor de Cartera, sin permisos de Recaudo, ni ve la ruta.
        var asesor = await UsuariosDeCartera.IngresarAsync(http, c.Escenario.Usuarios.AsesorO1);
        var liberar = await InventarioE2E.MandarAsync(http, asesor, HttpMethod.Post, $"/api/receipts/cash-operations/{id}/release",
            new { supportAttachmentPublicId = Guid.NewGuid(), reason = "No soy el oficial (e2e)" });
        liberar.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound, "sin Receipts.Compliance.Release la ruta no existe para quien pide (FR-017)");
    }

    /// <summary>
    /// Segunda ronda del 2026-10-27 (D-e, T0491; FR-230): el falso positivo lo libera el oficial de cumplimiento desde su bandeja, con motivo y soporte de la
    /// operación; el cajero de la sesión no puede aunque tenga el permiso, y después la caja confirma sin que la misma coincidencia la vuelva a detener.
    /// </summary>
    [Fact]
    public async Task El_oficial_libera_el_falso_positivo_y_la_caja_confirma()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "listalibera");
        var t = c.Token;
        await CarteraE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/core/external-channels", new
        {
            kind = "RestrictiveLists", channelCode = "SIMULADO", validFrom = c.Escenario.Hoy, reason = "Listas simuladas para la caja (e2e)",
        });
        await DocumentoEnListaAsync(http, t, c.Persona);
        var operacion = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/receipts/cash-operations",
            new { sessionPublicId = c.Sesion, payerPersonPublicId = c.Persona }));
        var id = operacion.GetProperty("publicId").GetGuid();
        RecaudoYTesoreriaE2E.Enum<IngenIA365ERP.Domain.Enums.Receipts.CashOperationState>(operacion.GetProperty("status"))
            .Should().Be(IngenIA365ERP.Domain.Enums.Receipts.CashOperationState.Blocked);

        // El oficial: la plantilla de cumplimiento a un usuario que no es el cajero.
        var admin = c.Escenario.Coop.TokenAdmin;
        var rol = (await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/admin/roles/from-template",
            new { templateKey = "cumplimiento.oficial", code = "OFCE2E", name = "Oficial de cumplimiento (e2e)" })).GetProperty("rolePublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{c.Escenario.Usuarios.Oficial.PublicId}/roles", new { rolePublicId = rol });
        var oficial = await UsuariosDeCartera.IngresarAsync(http, c.Escenario.Usuarios.Oficial);

        var bandeja = await CarteraE2E.GetAsync(http, oficial, "/api/receipts/compliance/blocked");
        bandeja.EnumerateArray().Select(o => o.GetProperty("operationPublicId").GetGuid()).Should().Contain(id, "la operación bloqueada está en la bandeja del oficial");
        var soporte = await AdjuntosE2E.SubirAsync(http, oficial, id, System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 homónimo verificado (e2e)"), "homonimo.pdf",
            "application/pdf", "ReceiptsScreeningReleaseSupport");
        soporte.EstadoDelAlmacen.Should().Be(System.Net.HttpStatusCode.NoContent);

        // El cajero de la sesión (el administrador, con todos los permisos) no libera.
        var porElCajero = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/cash-operations/{id}/release-binding-match",
            new { supportAttachmentPublicId = soporte.Adjunto, reason = "Lo libero yo (e2e)" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(porElCajero)).Should().Be("Receipts.Compliance.ReleaseByCashier");

        var liberada = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, oficial, HttpMethod.Post,
            $"/api/receipts/cash-operations/{id}/release-binding-match", new { supportAttachmentPublicId = soporte.Adjunto, reason = "Homónimo: otro documento (e2e)" }));
        RecaudoYTesoreriaE2E.Enum<IngenIA365ERP.Domain.Enums.Receipts.CashOperationState>(liberada.GetProperty("status"))
            .Should().Be(IngenIA365ERP.Domain.Enums.Receipts.CashOperationState.Preparing);

        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, t, HttpMethod.Put, $"/api/receipts/cash-operations/{id}/items",
            new { items = new[] { CajaE2E.Cuota(c.CreditoA, 50_000m) } }), "ítems");
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, t, HttpMethod.Put, $"/api/receipts/cash-operations/{id}/means",
            new { means = new[] { CajaE2E.Medio(c.Efectivo, 50_000m) } }), "medios");
        var recibo = await CajaE2E.ReciboAsync(http, c, id);
        recibo.GetProperty("number").GetString().Should().NotBeNullOrEmpty("la misma coincidencia, liberada, no vuelve a detener la operación");
    }

    /// <summary>Cambia el documento de la persona para que el canal simulado la encuentre en lista (termina en 9).</summary>
    private static async Task DocumentoEnListaAsync(HttpClient http, string admin, Guid persona)
    {
        var actual = JsonNode.Parse((await CarteraE2E.GetAsync(http, admin, $"/api/core/people/{persona}")).GetRawText())!.AsObject();
        var documento = actual["taxId"]?.GetValue<string>() ?? "900100";
        actual["taxId"] = documento[..^1] + "9";
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Put, $"/api/core/people/{persona}", actual), "cambiar el documento");
    }
}
