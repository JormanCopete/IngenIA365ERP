using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0531 (feature 013, US5; FR-089, T20, C35; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): los dobles de prueba por HTTP.
/// <list type="bullet">
/// <item>la tesorería de prueba ve la orden que emitió un desembolso; «rechazada» se consume con <c>ConsumeTreasuryResultCommand</c>, reversa el
/// desembolso (<c>DesembolsoReversado</c>) y deja el crédito <c>PendingDisbursement</c>: el «transferencia rechazada» que necesita US3;</item>
/// <item>«girada» después de pedir la anulación de la orden no reversa: la anulación no tuvo efecto;</item>
/// <item>sin <c>Lending.TestDoubles.Operate</c>, el 404 genérico; el servidor dice que el recaudador de prueba está;</item>
/// <item>en una cooperativa activada a la que se le retira la fila <c>TST</c> del registro, el recaudador de prueba responde
/// <c>Lending.Contract.CollectorNotRegistered</c> (el recaudador fuera del registro de T0527).</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class DoblesDePruebaDeCarteraTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_tesoreria_de_prueba_ve_la_orden_y_rechazada_reversa_el_desembolso()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (_, _, hecho) = await CreditoDesembolsadoAsync(http, esc, "DobleRechazo");
        var credito = hecho.GetProperty("loanPublicId").GetGuid();
        var orden = hecho.GetProperty("order").GetProperty("publicId").GetGuid();

        var recibidas = await CarteraE2E.GetAsync(http, esc.Usuarios.Pruebas.Token, TesoreriaDePrueba);
        recibidas.EnumerateArray().Should().Contain(o => o.GetProperty("orderPublicId").GetGuid() == orden);

        var mensaje = await ResultadoDelGiroAsync(http, esc, orden, "Rejected");
        // US6 (bloque 10B): el despachador entrega el resultado del giro a Cartera sin que nadie lo consuma a mano.
        (await EntregarPorElDespachadorAsync(fx, esc, mensaje)).Should().Be(IngenIA365ERP.Domain.Enums.Integration.DeliveryStatus.Processed);
        (await EstadoAsync(fx, esc, credito)).Should().Be(LoanStatus.PendingDisbursement);
        (await EnLaBaseAsync(fx, esc, db => db.IntegrationMessages.AsNoTracking()
            .CountAsync(m => m.Type == "DesembolsoReversado" && m.RelatedPublicId == hecho.GetProperty("operationPublicId").GetGuid()))).Should().Be(1);
    }

    [Fact]
    public async Task Girada_despues_de_pedir_la_anulacion_no_reversa()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (_, _, hecho) = await CreditoDesembolsadoAsync(http, esc, "DobleAnulada");
        var credito = hecho.GetProperty("loanPublicId").GetGuid();
        var orden = hecho.GetProperty("order").GetProperty("publicId").GetGuid();

        var anular = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, $"{Ordenes}/{orden}/void",
            new { reason = "El deudor pidió esperar (e2e)" });
        anular.IsSuccessStatusCode.Should().BeTrue(await anular.Content.ReadAsStringAsync());
        var recibidas = await CarteraE2E.GetAsync(http, esc.Usuarios.Pruebas.Token, TesoreriaDePrueba);
        recibidas.EnumerateArray().Should().Contain(o => o.GetProperty("orderPublicId").GetGuid() == orden);

        var mensaje = await ResultadoDelGiroAsync(http, esc, orden, "Disbursed");
        (await ConsumirAsync(fx, esc, mensaje)).Should().BeOfType<Application.Common.Integration.ResultadoDeConsumo.Processed>();
        (await CarteraE2E.GetAsync(http, esc.Usuarios.Jefe.Token, $"{Ordenes}/{orden}")).GetProperty("status").GetString().Should().Be("Disbursed");
        (await EstadoAsync(fx, esc, credito)).Should().Be(LoanStatus.Active, "la anulación llegó tarde: el giro salió y el crédito sigue vivo");
        (await OperacionesAsync(fx, esc, credito)).Should().NotContain(o => o.Clase == LoanOperationKind.DisbursementReversal);
    }

    [Fact]
    public async Task Sin_el_permiso_de_los_dobles_el_404_y_con_el_el_servidor_dice_que_estan()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var jefe = esc.Usuarios.Jefe.Token;
        foreach (var ruta in new[] { "/api/lending/test-doubles/", $"{Recaudador}/reversals", TesoreriaDePrueba })
        {
            using var resp = await CarteraE2E.MandarGetAsync(http, jefe, ruta);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ruta} sin Lending.TestDoubles.Operate");
        }
        var sinPermiso = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Recaudador}/payments", new { items = Array.Empty<object>(), interactive = false });
        sinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var hay = await CarteraE2E.GetAsync(http, esc.Usuarios.Pruebas.Token, "/api/lending/test-doubles/");
        hay.GetProperty("treasury").GetBoolean().Should().BeTrue();
        hay.GetProperty("collector").GetBoolean().Should().BeTrue("fuera de producción y con Lending:TestDoubles:Enabled el recaudador está registrado");
    }

    [Fact]
    public async Task Sin_la_fila_TST_el_recaudador_de_prueba_no_esta_en_el_registro()
    {
        var hoy = CarteraE2E.Hoy(fx);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "sin_dobles", primerEjercicioContable: hoy.Year);
        // La activación exige los dobles registrados (o Recaudo y Tesorería reales): se activa con ellos y se retira la fila TST desde ayer.
        await CarteraE2E.ActivarCarteraAsync(fx, coop);
        using var http = fx.CreateClient();
        var tst = (await CarteraE2E.GetAsync(http, coop.TokenAdmin, "/api/core/integration-participants?class=Test")).EnumerateArray()
            .Single(p => p.GetProperty("code").GetString() == "TST" && p.GetProperty("validTo").ValueKind == JsonValueKind.Null);
        var retiro = await CarteraE2E.ConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/core/integration-participants/{tst.GetProperty("publicId").GetGuid()}/retire",
            new { validTo = hoy.AddDays(-1), reason = "Prueba del recaudador fuera del registro (e2e)" });
        retiro.IsSuccessStatusCode.Should().BeTrue(await retiro.Content.ReadAsStringAsync());
        var persona = await CarteraE2E.PersonaAsync(http, coop.TokenAdmin, "SinRegistro");

        var resp = await CarteraE2E.ConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{Recaudador}/payments", new
        {
            items = new[] { Pago(Guid.NewGuid(), persona, "FreeAmount", 10_000m, hoy) }, interactive = false,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var item = (await CarteraE2E.LeerAsync(resp)).GetProperty("payments")[0];
        Rechazada(item, "Lending.Contract.CollectorNotRegistered");

        // Listar no escribe y también lo dice (Answered vacío no: el recaudador no existe para esta cooperativa).
        var listado = await CarteraE2E.GetAsync(http, coop.TokenAdmin, $"{Recaudador}/obligations?personPublicId={persona}");
        listado.GetProperty("result").GetInt32().Should().BeOneOf(Respondido, Rechazado);
    }
}
