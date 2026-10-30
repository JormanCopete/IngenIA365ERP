using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0069 (US1 «Independent Test», US1-1 a US1-4): un punto con dos cajas que ofrece efectivo y transferencia, un cajero asignado, la clase de
/// crédito rechazada, el código duplicado avisado antes de guardar y el cambio de base guardado para la próxima sesión. Se corre en la fase 22.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ConfiguracionDeRecaudoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Un_punto_con_dos_cajas_efectivo_y_transferencia_y_su_cajero()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "configuracion");
        using var http = fx.CreateClient();
        var t = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var efectivo = await ConfiguracionDeRecaudoE2E.MedioAsync(http, t, "EFREC", "Cash", hoy.AddDays(-1));
        var transferencia = await ConfiguracionDeRecaudoE2E.MedioAsync(http, t, "TRREC", "Transfer", hoy.AddDays(-1));

        var (punto, caja1) = await ConfiguracionDeRecaudoE2E.PuntoConCajaAsync(http, t, coop.SucursalPrincipal, "NORTE", efectivo, transferencia);
        var caja2 = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/receipts/cash-boxes",
            new { collectionPointPublicId = punto, code = "C2", name = "Caja 2", baseMode = "FixedFund", baseAmount = 300_000m, isMainCashBox = false });
        caja2.StatusCode.Should().Be(HttpStatusCode.Created);

        var medios = await InventarioE2E.GetAsync(http, t, $"/api/receipts/collection-points/{punto}/payment-means?asOf={hoy:yyyy-MM-dd}");
        medios.GetArrayLength().Should().Be(2);

        var usuario = await CarteraE2E.UsuarioPorCorreoAsync(http, t, coop.CorreoAdmin!);
        await CarteraE2E.AsociarPersonaAsync(http, t, usuario, coop.CorreoAdmin!, await CarteraE2E.PersonaAsync(http, t, "Carla", "Cajera"));
        var cajero = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/cash-boxes/{caja1}/cashiers", new { userPublicId = usuario });
        cajero.StatusCode.Should().Be(HttpStatusCode.OK, await cajero.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task La_clase_de_credito_se_rechaza_y_el_codigo_duplicado_se_avisa_antes_de_guardar()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "configuracion");
        using var http = fx.CreateClient();
        var t = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var (punto, caja) = await ConfiguracionDeRecaudoE2E.PuntoConCajaAsync(http, t, coop.SucursalPrincipal, "SUR");
        var credito = await ConfiguracionDeRecaudoE2E.MedioAsync(http, t, "CREDASOR", "AssociateCredit", hoy.AddDays(-1));

        var r = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/collection-points/{punto}/payment-means", new { paymentMeansPublicId = credito });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(r)).Should().Be("Receipts.PaymentMeans.CreditClassNotAllowed");

        var duplicado = await InventarioE2E.GetAsync(http, t, "/api/catalogos/puntos-de-recaudo/codigo/sur");
        duplicado.GetProperty("existe").GetBoolean().Should().BeTrue();

        var cambio = await InventarioE2E.MandarAsync(http, t, HttpMethod.Put, $"/api/receipts/cash-boxes/{caja}",
            new { name = "Caja 1", baseMode = "DailyBase", baseAmount = 800_000m, isMainCashBox = false });
        (await RecaudoYTesoreriaE2E.LeerAsync(cambio)).GetProperty("baseAmount").GetDecimal().Should().Be(800_000m);
    }
}
