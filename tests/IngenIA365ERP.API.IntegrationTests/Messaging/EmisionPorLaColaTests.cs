using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using IngenIA365ERP.Application.Common.Mensajeria;
using IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;
using static IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing.EscenarioDeFacturacionElectronica;

namespace IngenIA365ERP.API.IntegrationTests.Messaging;

/// <summary>
/// Feature 023 (quickstart §1–§3; US1, US2, US3, US5, US6): la emisión electrónica por RabbitMQ de verdad, sobre <c>CanalSimulado</c> y el
/// motor de <c>DB_PROVIDER</c>. La factura de oficina y la venta del POS se emiten solas por la cola (sin pasadas), un aviso repetido no emite
/// dos veces, el seguimiento guarda la representación detrás, y un documento que agota su tope queda muerto, alertado y se reprocesa con
/// motivo y permiso. Cada caso en su cooperativa. (nuevo)
/// </summary>
[Collection(MensajeriaCollection.Nombre)]
public class EmisionPorLaColaTests(ApiConMensajeriaFixture fx)
{
    private const int Validado = 2;

    private static async Task<JsonElement> HastaAsync(Func<Task<JsonElement>> leer, Func<JsonElement, bool> listo, string que, int segundos = 30)
    {
        var reloj = Stopwatch.StartNew();
        JsonElement ultimo;
        do
        {
            ultimo = await leer();
            if (listo(ultimo)) return ultimo;
            await Task.Delay(250);
        }
        while (reloj.Elapsed < TimeSpan.FromSeconds(segundos));
        throw new Xunit.Sdk.XunitException($"{que}: no ocurrió en {segundos} s. Último estado: {ultimo}");
    }

    private static bool Validada(JsonElement detalle) => Estado(detalle) is Validado or 3;

    [Fact]
    public async Task La_factura_de_oficina_se_emite_por_la_cola_y_el_seguimiento_guarda_la_representacion_detras()
    {
        await fx.EsperarConexionAsync();
        var esc = await PrepararAsync(fx, "colaofi");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var reloj = Stopwatch.StartNew();
        var factura = await esc.FacturaConfirmadaAsync(http, esc.Ventas.Supervisor, await PersonaAsync(http, t, "Cola", 7));
        reloj.Stop();
        var electronico = await ElectronicoAsync(http, t, Id(factura));

        var detalle = await HastaAsync(() => DetalleAsync(http, t, electronico), Validada, "validación por la cola");
        detalle.GetProperty("transmissions").GetArrayLength().Should().Be(1, "un solo intento, hecho por el consumidor");
        await HastaAsync(() => DetalleAsync(http, t, electronico),
            d => d.GetProperty("versions")[0].GetProperty("artifacts").EnumerateArray()
                .Any(a => a.GetProperty("artifact").GetString() == "GraphicRepresentation"),
            "representación gráfica por el seguimiento");
    }

    [Fact]
    public async Task El_cobro_del_POS_entrega_siempre_la_tirilla_sin_esperar_mas_que_el_punto_de_venta()
    {
        await fx.EsperarConexionAsync();
        var esc = await PrepararAsync(fx, "colapos");
        using var http = fx.CreateClient();
        var cajero = esc.Ventas.Cajero1;
        var sesion = await EscenarioDeVentas.SesionAsync(http, cajero.Token, esc.CajaElectronica);

        var venta = await EscenarioDeVentas.NuevaVentaAsync(http, cajero.Token, sesion, esc.Inv.P("P7").CodigoDeBarras);
        var id = EscenarioDeVentas.VentaId(venta);
        var aPagar = EscenarioDeVentas.AmountDue(venta);
        var reloj = Stopwatch.StartNew();
        var cobro = await EscenarioDeVentas.CobrarAsync(http, cajero.Token, id, aPagar, [esc.Ventas.Efectivo(aPagar)]);
        reloj.Stop();

        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(6), "la espera del punto de venta es de 2 s; el resto es la venta misma en el contenedor");
        cobro.GetProperty("ticket").ValueKind.Should().Be(JsonValueKind.Object, $"D7: nunca hay cobro sin tirilla. {cobro}");
        var bloque = cobro.GetProperty("electronic");
        var electronico = bloque.GetProperty("electronicDocumentPublicId").GetGuid();
        if (bloque.GetProperty("deliverable").GetBoolean())
            cobro.GetProperty("ticket").GetProperty("electronic").GetProperty("uniqueCode").GetString().Should().HaveLength(96);
        else
            cobro.GetProperty("ticket").GetProperty("electronic").GetProperty("legend").GetString().Should().Contain("pendiente de validación DIAN");
        await HastaAsync(() => DetalleAsync(http, esc.Admin, electronico), Validada, "validación por la cola");
    }

    [Fact]
    public async Task Un_aviso_repetido_no_emite_dos_veces()
    {
        await fx.EsperarConexionAsync();
        var esc = await PrepararAsync(fx, "colarep");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var factura = await esc.FacturaConfirmadaAsync(http, esc.Ventas.Supervisor, await PersonaAsync(http, t, "Repetido", 7));
        var electronico = await ElectronicoAsync(http, t, Id(factura));

        for (var i = 0; i < 4; i++)
            (await fx.Cola.PublicarAsync(Aviso.De(TiposDeAviso.FeEmitir, esc.Coop.TenantPublicId, electronico, DateTime.UtcNow, "prueba"), default))
                .Should().BeTrue("el broker confirma");

        var detalle = await HastaAsync(() => DetalleAsync(http, t, electronico), Validada, "validación");
        await Task.Delay(1500); // que terminen los avisos repetidos
        detalle = await DetalleAsync(http, t, electronico);
        detalle.GetProperty("transmissions").GetArrayLength().Should().Be(1, "el arrendamiento por fila y la relectura dejan un solo envío");
    }

    [Fact]
    public async Task Agotado_el_tope_queda_muerto_alertado_y_el_reproceso_exige_permiso_y_motivo()
    {
        await fx.EsperarConexionAsync();
        var esc = await PrepararAsync(fx, "colatope");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/EINV/Dian.TopeDeIntentos/versions", new
        {
            scopeKind = "None", value = "2", validFrom = esc.Desde.ToString("yyyy-MM-dd"), reason = "Ensayo del tope de intentos",
        });

        // El dígito 5 hace que el canal simulado no responda (ChannelUnavailable): un intento por la cola y otro a mano.
        var factura = await esc.FacturaConfirmadaAsync(http, esc.Ventas.Supervisor, await PersonaAsync(http, t, "Caido", 5));
        var electronico = await ElectronicoAsync(http, t, Id(factura));
        await HastaAsync(() => DetalleAsync(http, t, electronico), d => d.GetProperty("document").GetProperty("attemptCount").GetInt32() >= 1,
            "el primer intento por la cola");
        await EmitirAsync(http, t, electronico);

        var muerto = await HastaAsync(() => DetalleAsync(http, t, electronico),
            d => d.GetProperty("document").TryGetProperty("deadAt", out var m) && m.ValueKind == JsonValueKind.String, "la muerte");
        muerto.GetProperty("document").GetProperty("displayStatus").GetString().Should().Be("Dead");
        (await esc.AlertasAsync(http, "Dian.DocumentoSinEnviar")).Should().NotBeEmpty();

        var lista = await InventarioE2E.GetAsync(http, t, "/api/platform/dead-letters?kind=1");
        lista.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("publicId").GetGuid()).Should().Contain(electronico);

        var cuerpo = new { items = new[] { new { kind = 1, publicId = electronico } }, reason = "El canal volvió a responder" };
        var sinPermiso = await InventarioE2E.MandarAsync(http, esc.Ventas.Cajero1.Token, HttpMethod.Post, "/api/platform/dead-letters/reprocess", cuerpo);
        sinPermiso.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var sinMotivo = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/platform/dead-letters/reprocess",
            new { items = cuerpo.items, reason = "corto" });
        sinMotivo.IsSuccessStatusCode.Should().BeFalse("el motivo tiene entre 10 y 500 caracteres");

        var hecho = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/platform/dead-letters/reprocess", cuerpo);
        hecho.GetProperty("reprocessed").GetInt32().Should().Be(1);
        var vuelto = await DetalleAsync(http, t, electronico);
        vuelto.GetProperty("document").GetProperty("deadAt").ValueKind.Should().Be(JsonValueKind.Null, vuelto.ToString());
    }
}

/// <summary>Feature 023 (quickstart §2; FR-010, FR-012): sin broker la API vende, informa salud degradada y el barrido emite. (nuevo)</summary>
[Collection(SinBrokerCollection.Nombre)]
public class SinBrokerTests(ApiSinBrokerFixture fx)
{
    [Fact]
    public async Task Sin_broker_la_API_confirma_informa_salud_degradada_y_el_barrido_emite()
    {
        var esc = await PrepararAsync(fx, "sinbroker");
        using var http = fx.CreateClient();
        var t = esc.Admin;

        var salud = await http.GetAsync("/health/ready");
        salud.StatusCode.Should().Be(HttpStatusCode.OK, "un broker caído no saca la réplica del Service");
        (await salud.Content.ReadAsStringAsync()).Should().Contain("rabbitmq").And.Contain("Degraded");

        var factura = await esc.FacturaConfirmadaAsync(http, esc.Ventas.Supervisor, await PersonaAsync(http, t, "SinBroker", 7));
        var electronico = await ElectronicoAsync(http, t, Id(factura));
        Estado(await DetalleAsync(http, t, electronico)).Should().Be(0, "nadie la emitió todavía");

        await ProcesarAsync(fx, esc.Coop.TenantPublicId);
        Estado(await DetalleAsync(http, t, electronico)).Should().BeOneOf(2, 3);
    }
}
