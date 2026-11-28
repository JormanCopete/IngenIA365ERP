using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales.Conecta;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using Microsoft.Extensions.DependencyInjection;
using static IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing.EscenarioDeFacturacionElectronica;

namespace IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;

/// <summary>
/// Feature 028, T050 (plan.md §7): el canal <c>CONECTA-PRUEBAS</c> de punta a punta por HTTP, contra un IngenIA Conecta falso en memoria
/// (<see cref="ConectaStubHandler"/>), en el motor de <c>DB_PROVIDER</c>. Cubre:
/// <list type="bullet">
/// <item>la configuración con cooperativa y servicio, y la verificación;</item>
/// <item>la factura numerada por el ERP y validada con sus artefactos, y su nota crédito con CUDE;</item>
/// <item>el reintento que no duplica en Conecta;</item>
/// <item>el modo real, que no existe fuera de producción;</item>
/// <item>la credencial que es de su cooperativa.</item>
/// </list>
/// Cada caso en su cooperativa. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CanalConectaE2E(CentralIdentityApiFixture fx)
{
    public const string Codigo = "CONECTA-PRUEBAS";
    private const int Validado = 2;

    /// <summary>Las opciones del canal en la fixture: un extremo de prueba, espera corta y la compuerta de D1 encendida.</summary>
    public static ElectronicInvoicingOptions Opciones()
    {
        var o = new ElectronicInvoicingOptions();
        o.Channels[Codigo] = new OpcionesDeCanal
        {
            SyncWaitSeconds = 1,
            PollIntervalMs = 50,
            ConectaAceptaNumeroDelErp = true,
            Endpoints = new(StringComparer.OrdinalIgnoreCase) { ["QA"] = "https://conecta-qa.prueba" },
        };
        return o;
    }

    private static readonly Guid TenantConecta = Guid.Parse("266f0ee4-20b4-4676-8d76-946ade8e62b4");

    private ConectaStubHandler Stub => fx.Factory.Services.GetRequiredService<ConectaStubHandler>();

    private static void CredencialDeConecta(CentralIdentityApiFixture fx, Guid tenant) =>
        File.WriteAllText(Path.Combine(fx.DirectorioDeCredenciales, $"{tenant:D}.{Codigo}.json"),
            """{ "clientId": "erp-coop", "clientSecret": "sin-valor-real" }""");

    /// <summary>Configura CONECTA-PRUEBAS desde hoy, con las resoluciones en uso asociadas, y verifica la credencial.</summary>
    private static async Task ConfigurarConectaAsync(CentralIdentityApiFixture fx, HttpClient http, EscenarioDeFacturacionElectronica esc)
    {
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        foreach (var prefijo in new[] { "FE", "DE", "DS", "CDE", "CFE", "FX" })
            await AsociarAsync(http, t, esc.Resoluciones[prefijo], Codigo, hoy, prefijo is "FE" or "FX" ? "clave-tecnica-conecta-ab12" : null);
        var vigencia = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings", new
        {
            mode = "TechnologyProvider", channelCode = Codigo, environment = "Testing", emailDeliveryBy = "Erp", isEnabled = true,
            validFrom = hoy.ToString("yyyy-MM-dd"), reason = "Emisión por IngenIA Conecta en pruebas",
            channelTenantId = TenantConecta, channelEndpoint = "qa",
        });
        vigencia.GetProperty("channelEndpoint").GetString().Should().Be("QA");
        CredencialDeConecta(fx, esc.Coop.TenantPublicId);
        // La verificación compara el NIT del tenant de Conecta con el del emisor: el Conecta falso responde el de esta cooperativa.
        fx.Factory.Services.GetRequiredService<ConectaStubHandler>().NitDelTenant = vigencia.GetProperty("issuerTaxId").GetString()!;
        var verificacion = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings/verify-credential",
            new { channelCode = Codigo });
        verificacion.GetProperty("verified").GetBoolean().Should().BeTrue(verificacion.ToString());
    }

    [Fact]
    public async Task La_factura_sale_por_Conecta_con_el_numero_del_ERP_y_se_valida_con_sus_artefactos_y_su_nota_tambien()
    {
        var esc = await PrepararAsync(fx, "feconecta");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var supervisor = esc.Ventas.Supervisor;
        await ConfigurarConectaAsync(fx, http, esc);

        var comprador = await PersonaAsync(http, t, "Conecta", 7);
        var factura = await esc.FacturaConfirmadaAsync(http, supervisor, comprador);
        var electronico = await ElectronicoAsync(http, t, Id(factura));
        var antes = await DetalleAsync(http, t, electronico);
        var numero = antes.GetProperty("document").GetProperty("number").GetString()!;
        antes.GetProperty("document").GetProperty("channelCode").GetString().Should().Be(Codigo);

        await EmitirAsync(http, t, electronico);
        var detalle = await DetalleAsync(http, t, electronico);

        Estado(detalle).Should().Be(Validado, detalle.ToString());
        detalle.GetProperty("document").GetProperty("uniqueCode").GetString().Should().NotBeNullOrWhiteSpace();
        var enConecta = Stub.PorClave.Values.Single(d => d.Numero == numero);
        enConecta.Numero.Should().Be(numero, "D1: Conecta registra el número que asignó el ERP");

        // Reintentar un documento ya validado no vuelve a crear nada en Conecta.
        var emisiones = Stub.EmisionesPorNumero[numero];
        await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Documentos}/{electronico}/retry");
        Stub.PorClave.Values.Count(d => d.Numero == numero).Should().Be(1);
        Stub.EmisionesPorNumero[numero].Should().BeGreaterThanOrEqualTo(emisiones);

        // El cuerpo enviado lleva el tenant de Conecta, el número y los nombres separados del comprador.
        var cuerpo = JsonNode.Parse(Stub.Cuerpos.Last(c => c.Contains("\"tenantId\"", StringComparison.Ordinal)
            && c.Contains($"\"erpNumber\":\"{numero}\"", StringComparison.Ordinal)))!;
        cuerpo["tenantId"]!.GetValue<Guid>().Should().Be(TenantConecta);
        (cuerpo["prefix"]!.GetValue<string>() + cuerpo["number"]!.GetValue<long>()).Should().Be(numero);

        // La nota crédito total sale por el canal vigente, referida al CUFE de la factura.
        var nota = await InventarioE2E.BorradorAsync(http, supervisor.Token, Notas, new
        {
            originDocumentPublicId = Id(factura), reason = "Anulación de la venta por devolución", totalVoid = true, withReturn = true,
            lines = Array.Empty<object>(), refunds = Array.Empty<object>(), documentTypePublicId = esc.Tipo("NC"), correctionConceptCode = "2",
        });
        var confirmada = await InventarioE2E.ExitoAsync(http, supervisor.Token, HttpMethod.Post, $"{Notas}/{Id(nota)}/confirm",
            new { expectedAmountDue = AmountDue(nota) });
        var notaElectronica = await ElectronicoAsync(http, t, Id(confirmada));
        await EmitirAsync(http, t, notaElectronica);
        var detalleNota = await DetalleAsync(http, t, notaElectronica);
        detalleNota.GetProperty("document").GetProperty("channelCode").GetString().Should().Be(Codigo);
        Estado(detalleNota).Should().Be(Validado, detalleNota.ToString());
        var cuerpoNota = JsonNode.Parse(Stub.Cuerpos.Last(c => c.Contains("\"referencedInvoice\"", StringComparison.Ordinal)))!;
        cuerpoNota["referencedInvoice"]!["cufe"]!.GetValue<string>().Should().Be(detalle.GetProperty("document").GetProperty("uniqueCode").GetString());
    }

    [Fact]
    public async Task El_modo_real_no_existe_fuera_de_produccion_y_el_de_pruebas_no_admite_produccion()
    {
        var esc = await PrepararAsync(fx, "feconectareal");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");

        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings", new
        {
            mode = "TechnologyProvider", channelCode = "CONECTA", environment = "Production", emailDeliveryBy = "Erp", isEnabled = true,
            validFrom = hoy, reason = "Intento de producción", channelTenantId = TenantConecta, channelEndpoint = "QA", confirmProduction = "PRODUCCION",
        }), "ElectronicInvoicing.Settings.ChannelUnknown");
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings", new
        {
            mode = "TechnologyProvider", channelCode = Codigo, environment = "Production", emailDeliveryBy = "Erp", isEnabled = true,
            validFrom = hoy, reason = "Pruebas en producción", channelTenantId = TenantConecta, channelEndpoint = "QA",
        }), "ElectronicInvoicing.Settings.ChannelEnvironmentMismatch");

        var config = await InventarioE2E.GetAsync(http, t, "/api/electronic-invoicing/settings");
        var conecta = config.GetProperty("availableChannels").EnumerateArray().Single(c => c.GetProperty("channelCode").GetString() == Codigo);
        conecta.GetProperty("requiredEnvironment").GetString().Should().Be("Testing");
        conecta.GetProperty("endpoints").EnumerateArray().Select(e => e.GetString()).Should().Equal("QA");
        conecta.GetProperty("isProduction").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task La_credencial_de_Conecta_es_de_su_cooperativa()
    {
        var esc = await PrepararAsync(fx, "feconectacred");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        foreach (var prefijo in new[] { "FE", "DE", "DS", "CDE", "CFE", "FX" })
            await AsociarAsync(http, t, esc.Resoluciones[prefijo], Codigo, hoy, prefijo is "FE" or "FX" ? "clave-tecnica-conecta-ab12" : null);
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings", new
        {
            mode = "TechnologyProvider", channelCode = Codigo, environment = "Testing", emailDeliveryBy = "Erp", isEnabled = true,
            validFrom = hoy.ToString("yyyy-MM-dd"), reason = "Conecta sin credencial", channelTenantId = TenantConecta, channelEndpoint = "QA",
        });
        // La credencial de OTRA cooperativa no le sirve: el archivo se busca por la cooperativa resuelta.
        CredencialDeConecta(fx, Guid.NewGuid());

        var verificacion = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/electronic-invoicing/settings/verify-credential",
            new { channelCode = Codigo });
        var cuerpo = await verificacion.Content.ReadAsStringAsync();
        cuerpo.Should().NotContain("\"verified\":true");
    }
}
