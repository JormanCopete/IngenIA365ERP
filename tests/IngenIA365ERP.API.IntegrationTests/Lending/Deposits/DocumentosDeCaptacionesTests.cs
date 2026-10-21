using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T200 (US13, escenarios 1 y 2 de la spec; contracts/documentos.md §2.1, §3, §4; api.md §17): los documentos al titular de captaciones por
/// HTTP en una cooperativa aislada con captaciones activadas y el firmante configurado. Ana tiene una cuenta de ahorro con dos consignaciones:
/// <list type="number">
/// <item>US13-1: el extracto del período se emite con número y código; pedirlo otra vez sin cambios devuelve la misma copia (no consume número) y, tras
/// una consignación nueva del período, sale otro.</item>
/// <item>US13-2: un usuario de la cooperativa verifica el código y ve el documento vigente; reversada desde la Cartera una consignación que el extracto
/// vio, el mismo código muestra el documento anulado con su motivo.</item>
/// <item>El certificado de saldo a una fecha futura responde <c>Lending.Document.FutureDateNotAllowed</c>, y el título del CDAT por la ruta de emitir,
/// <c>Lending.Document.IssuedAutomatically</c>.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase. Exige la entrega de captaciones desplegada
/// (<c>EntregaVigente</c> ≥ K8, research E127).
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class DocumentosDeCaptacionesTests(CentralIdentityApiFixture fx)
{
    private const string Documentos = DocumentosE2E.Documentos;
    private const string Cuentas = "/api/lending/accounts";

    private sealed record Escenario(CaptacionesE2E.CooperativaDeCaptaciones Coop, DateOnly Hoy, Guid Ana, Guid Cuenta, Guid PrimeraConsignacion);

    private async Task<Escenario> ArmarAsync(string nombre)
    {
        using var http = fx.CreateClient();
        var hoy = CarteraE2E.Hoy(fx);
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, $"documentos_{nombre}");
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        await DocumentosE2E.FirmanteAsync(fx, coop.TenantPublicId, coop.TokenAdmin, hoy);
        var admin = coop.TokenAdmin;
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, $"AHD{nombre[..Math.Min(3, nombre.Length)].ToUpperInvariant()}");
        var ana = await MovimientosTests.AsociadoAsync(http, admin, $"Titular{nombre}");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, ana);
        var primera = await MovimientosTests.ConsignarAsync(http, admin, cuenta, ana, 800_000m, hoy);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, ana, 200_000m, hoy);
        return new Escenario(coop, hoy, ana, cuenta, primera.GetProperty("value").GetProperty("operationPublicId").GetGuid());
    }

    private static object Extracto(Escenario e) => new
    {
        kind = "AccountStatement", personPublicId = e.Ana, accountPublicId = e.Cuenta, period = new { from = e.Hoy.AddDays(-10), to = e.Hoy },
        delivery = "Download",
    };

    [Fact]
    public async Task US13_1_el_mismo_extracto_sin_cambios_devuelve_la_misma_copia_y_con_un_movimiento_nuevo_sale_otro()
    {
        var e = await ArmarAsync("copia");
        using var http = fx.CreateClient();

        var primero = await DocumentosE2E.EmitidoAsync(http, e.Coop.TokenAdmin, Extracto(e));
        primero.GetProperty("number").GetString().Should().StartWith("EXT-");
        primero.GetProperty("alreadyIssued").GetBoolean().Should().BeFalse();

        var copia = await DocumentosE2E.EmitidoAsync(http, e.Coop.TokenAdmin, Extracto(e));
        copia.GetProperty("documentPublicId").GetGuid().Should().Be(primero.GetProperty("documentPublicId").GetGuid());
        copia.GetProperty("alreadyIssued").GetBoolean().Should().BeTrue("el mismo período sin operaciones nuevas es la misma copia guardada");

        await MovimientosTests.ConsignarAsync(http, e.Coop.TokenAdmin, e.Cuenta, e.Ana, 50_000m, e.Hoy);
        var otro = await DocumentosE2E.EmitidoAsync(http, e.Coop.TokenAdmin, Extracto(e));
        otro.GetProperty("documentPublicId").GetGuid().Should().NotBe(primero.GetProperty("documentPublicId").GetGuid());
        otro.GetProperty("number").GetString().Should().NotBe(primero.GetProperty("number").GetString());

        var lista = await CarteraE2E.EnviarAsync(http, e.Coop.TokenAdmin, HttpMethod.Get, $"{Documentos}?accountPublicId={e.Cuenta}", null);
        lista.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CarteraE2E.LeerAsync(lista)).GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task US13_2_la_verificacion_muestra_el_original_vigente_y_anulado_tras_el_reverso_de_lo_que_vio()
    {
        var e = await ArmarAsync("verifica");
        using var http = fx.CreateClient();
        var emitido = await DocumentosE2E.EmitidoAsync(http, e.Coop.TokenAdmin, Extracto(e));
        var codigo = emitido.GetProperty("verificationCode").GetString()!;

        var vigente = await CarteraE2E.EnviarAsync(http, e.Coop.TokenAdmin, HttpMethod.Get, $"{Documentos}/verify/{codigo}", null);
        vigente.StatusCode.Should().Be(HttpStatusCode.OK, await vigente.Content.ReadAsStringAsync());
        var v = await CarteraE2E.LeerAsync(vigente);
        v.GetProperty("authentic").GetBoolean().Should().BeTrue();
        v.GetProperty("status").GetString().Should().Be("Valid");
        v.GetProperty("kind").GetString().Should().Be("AccountStatement");

        var reverso = await CarteraE2E.ConClaveAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, $"{Cuentas}/{e.Cuenta}/operations/{e.PrimeraConsignacion}/reverse",
            new { reason = "Consignación digitada dos veces (e2e)" });
        reverso.StatusCode.Should().Be(HttpStatusCode.OK, await reverso.Content.ReadAsStringAsync());

        var anulado = await CarteraE2E.LeerAsync(await CarteraE2E.EnviarAsync(http, e.Coop.TokenAdmin, HttpMethod.Get, $"{Documentos}/verify/{codigo}", null));
        anulado.GetProperty("status").GetString().Should().Be("Voided");
        anulado.GetProperty("voided").GetProperty("reason").GetString().Should().Contain("Se reversó la operación");
    }

    [Fact]
    public async Task El_certificado_de_saldo_futuro_y_el_titulo_por_la_ruta_de_emitir_se_rechazan()
    {
        var e = await ArmarAsync("rechazos");
        using var http = fx.CreateClient();

        var futuro = await CarteraE2E.ConClaveAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, Documentos, new
        {
            kind = "AccountBalanceCertificate", personPublicId = e.Ana, accountPublicId = e.Cuenta, cutoffDate = e.Hoy.AddDays(5), delivery = "Download",
        });
        futuro.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(futuro)).Should().Be("Lending.Document.FutureDateNotAllowed");

        var saldo = await DocumentosE2E.EmitidoAsync(http, e.Coop.TokenAdmin, new
        {
            kind = "AccountBalanceCertificate", personPublicId = e.Ana, accountPublicId = e.Cuenta, cutoffDate = e.Hoy, includeRestrictions = true,
            delivery = "Download",
        });
        saldo.GetProperty("number").GetString().Should().StartWith("CSA-");

        var titulo = await CarteraE2E.ConClaveAsync(http, e.Coop.TokenAdmin, HttpMethod.Post, Documentos, new
        {
            kind = "TermDepositTitle", personPublicId = e.Ana, delivery = "Download",
        });
        titulo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(titulo)).Should().Be("Lending.Document.IssuedAutomatically");
    }
}
