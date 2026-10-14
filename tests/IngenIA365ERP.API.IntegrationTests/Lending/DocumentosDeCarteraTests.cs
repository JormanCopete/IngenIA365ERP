using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Domain.Enums.Lending;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DocumentosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0769 (feature 013, US13; quickstart §3.13; FR-131, FR-132; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): los documentos al
/// deudor por <c>/api/lending/documents</c> en una cooperativa activada (<see cref="DocumentosE2E"/>):
/// <list type="bullet">
/// <item>el plan de pagos, el certificado de saldo y el paz y salvo de una obligación se emiten en un paso, con descarga (el PDF del enlace es el
/// guardado, con su SHA-256) y con correo (el mensaje <c>DocumentDelivery</c>); el paz y salvo con saldo se niega con la obligación y, cancelado el
/// crédito, sale sujeto a firmeza; el extracto responde <c>KindNotAvailable</c> (K5);</item>
/// <item>verificar el código: auténtico con sus datos esenciales para quien tiene <c>Lending.Documents.Verify</c> (US13-2), 404 sin el permiso;</item>
/// <item>reimprimir da el mismo SHA-256; la anulación manual lo deja anulado con su motivo, no se reimprime ni se envía y no se anula dos veces;</item>
/// <item>la misma <c>Idempotency-Key</c> deja un solo documento;</item>
/// <item>el paz y salvo general se niega con saldo (US13-1) y con una venta a crédito en reproceso nombrando su mensaje (US13-4, en la cooperativa de
/// las ventas con Cartera). El general emitido mencionando la venta aceptada sin migrar lo recorre
/// <see cref="MigracionYActivacionDeCarteraTests"/>, la única que tiene una carga con ventas fuera de Cartera.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class DocumentosDeCarteraTests(CentralIdentityApiFixture fx)
{
    private const string FormatoDeCodigo = "^[0-9A-HJKMNP-TV-Z]{4}(-[0-9A-HJKMNP-TV-Z]{4}){3}$";

    private static Guid Credito(JsonElement desembolso) => desembolso.GetProperty("loanPublicId").GetGuid();

    [Fact]
    public async Task Plan_certificado_y_paz_y_salvo_se_emiten_en_un_paso_y_el_codigo_se_verifica()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var admin = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var (_, persona, d) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "DocEmite");
        var credito = Credito(d);
        var numero = (await CarteraE2E.GetAsync(http, admin, $"{DesembolsoE2E.Creditos}/{credito}")).GetProperty("number").GetString()!;

        // El plan de pagos con descarga: número PP-, código en cuatro grupos, vigente, y el PDF del enlace es el guardado.
        var plan = await EmitidoAsync(http, admin, Pedido("PaymentPlan", persona, credito));
        plan.GetProperty("number").GetString().Should().StartWith("PP-");
        plan.GetProperty("verificationCode").GetString().Should().MatchRegex(FormatoDeCodigo);
        plan.GetProperty("status").GetString().Should().Be("Valid");
        var pdf = await BajarAsync(http, plan.GetProperty("download").GetProperty("url").GetString()!);
        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        Sha256(pdf).Should().Be(await HuellaGuardadaAsync(fx, esc, plan.GetProperty("documentPublicId").GetGuid()));

        // El paz y salvo con saldo se niega con la obligación (FR-131).
        var conSaldo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Documentos, Pedido("LoanClearance", persona, credito));
        var negado = await InventarioE2E.FallaAsync(conSaldo, "Lending.Document.ClearanceDenied");
        negado.GetProperty("data").GetProperty("obligations").EnumerateArray().Should()
            .ContainSingle(o => o.GetProperty("number").GetString() == numero && o.GetProperty("balance").GetDecimal() > 0m);

        // El certificado de saldo por correo: el mensaje DocumentDelivery queda en la cola (con la autorización de tratamiento general).
        await OriginacionE2E.AutorizarAsync(http, esc.Coop.TokenAdmin, persona, "GeneralTreatment");
        var certificado = await EmitidoAsync(http, admin, Pedido("BalanceCertificate", persona, credito, "Email", esc.Hoy));
        certificado.GetProperty("number").GetString().Should().StartWith("CS-");
        certificado.GetProperty("debtorMessagePublicId").ValueKind.Should().Be(JsonValueKind.String, "con correo nace el mensaje al deudor");
        certificado.GetProperty("download").ValueKind.Should().Be(JsonValueKind.Null, "con correo no hay enlace de descarga");

        // El extracto llega con K5.
        var extracto = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Documentos, Pedido("Statement", persona, credito));
        (await InventarioE2E.FallaAsync(extracto, "Lending.Document.KindNotAvailable")).GetProperty("data").GetProperty("availableFrom").GetString()
            .Should().Be("K5");

        var dia = esc.Hoy.AddDays(3);
        using (RelojEn(fx, dia))
        {
            await CancelarAsync(http, esc, persona, credito, dia);

            // Cancelado: el paz y salvo sale sujeto a firmeza (los días hábiles de la clase de medio) con el pago en la línea.
            var paz = await EmitidoAsync(http, admin, Pedido("LoanClearance", persona, credito));
            paz.GetProperty("number").GetString().Should().StartWith("PS-");
            paz.GetProperty("status").GetString().Should().Be("SubjectToFinality");
            paz.GetProperty("finalityUntil").ValueKind.Should().Be(JsonValueKind.String);

            // Verificar (US13-2): auténtico, con su estado y los datos esenciales; el titular enmascarado.
            var codigo = paz.GetProperty("verificationCode").GetString()!;
            var verificado = await VerificarAsync(http, admin, codigo.ToLowerInvariant().Replace("-", " "));
            verificado.GetProperty("authentic").GetBoolean().Should().BeTrue();
            verificado.GetProperty("status").GetString().Should().Be("SubjectToFinality");
            verificado.GetProperty("kind").GetString().Should().Be("LoanClearance");
            verificado.GetProperty("number").GetString().Should().Be(paz.GetProperty("number").GetString());
            verificado.GetProperty("holder").GetProperty("documentNumberMasked").GetString().Should().StartWith("*");
            verificado.GetProperty("essentialData").GetProperty("loans").EnumerateArray().Should()
                .ContainSingle(l => l.GetProperty("number").GetString() == numero);

            // Sin Lending.Documents.Verify, el mismo 404 que si no existiera; un código ajeno, NotFound con su mensaje.
            var asesor = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.AsesorO1);
            (await CarteraE2E.MandarGetAsync(http, asesor, $"{Documentos}/verify/{codigo}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            var ajeno = await CarteraE2E.MandarGetAsync(http, admin, $"{Documentos}/verify/0000-0000-0000-0000");
            (await InventarioE2E.FallaAsync(ajeno, "Lending.Document.NotFound", HttpStatusCode.NotFound)).GetProperty("message").GetString()
                .Should().Contain("no corresponde a un documento de esta cooperativa");
        }

        // La lista por crédito trae los tres emitidos, el más reciente primero.
        var lista = await CarteraE2E.GetAsync(http, admin, $"{Documentos}?loanPublicId={credito}");
        lista.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("kind").GetString())
            .Should().BeEquivalentTo(["LoanClearance", "BalanceCertificate", "PaymentPlan"], o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Reimprimir_da_el_mismo_pdf_y_la_anulacion_manual_lo_deja_anulado()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var admin = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var (_, persona, d) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "DocReimprime");
        var plan = await EmitidoAsync(http, admin, Pedido("PaymentPlan", persona, Credito(d)));
        var id = plan.GetProperty("documentPublicId").GetGuid();
        var original = await BajarAsync(http, plan.GetProperty("download").GetProperty("url").GetString()!);

        // Reimprimir es descargar el mismo PDF guardado (un enlace nuevo, sin clave, auditado).
        var enlace = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Documentos}/{id}/download-link", null);
        enlace.StatusCode.Should().Be(HttpStatusCode.OK, await enlace.Content.ReadAsStringAsync());
        var reimpreso = await BajarAsync(http, (await CarteraE2E.LeerAsync(enlace)).GetProperty("url").GetString()!);
        Sha256(reimpreso).Should().Be(Sha256(original), "reimprimir no vuelve a pintar");

        // La anulación manual con motivo; otra vez, AlreadyVoided.
        var anular = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Documentos}/{id}/void", new { reason = "Se emitió a la persona equivocada (e2e)" });
        anular.StatusCode.Should().Be(HttpStatusCode.NoContent, await anular.Content.ReadAsStringAsync());
        var otraVez = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Documentos}/{id}/void", new { reason = "Otra vez" });
        await InventarioE2E.FallaAsync(otraVez, "Lending.Document.AlreadyVoided");

        // Anulado: la verificación lo dice con el motivo; no se reimprime ni se envía.
        var verificado = await VerificarAsync(http, admin, plan.GetProperty("verificationCode").GetString()!);
        verificado.GetProperty("authentic").GetBoolean().Should().BeTrue();
        verificado.GetProperty("status").GetString().Should().Be("Voided");
        verificado.GetProperty("voided").GetProperty("reason").GetString().Should().Be("Se emitió a la persona equivocada (e2e)");
        await InventarioE2E.FallaAsync(await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Documentos}/{id}/download-link", null),
            "Lending.Document.VoidedCannotReissue");
        await InventarioE2E.FallaAsync(await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Documentos}/{id}/send", new { }),
            "Lending.Document.VoidedCannotReissue");

        var detalle = await CarteraE2E.GetAsync(http, admin, $"{Documentos}/{id}");
        detalle.GetProperty("document").GetProperty("status").GetString().Should().Be("Voided");
        detalle.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("kind").GetString()).Should().Equal("Issued", "Voided");
        (await CarteraE2E.GetAsync(http, admin, $"{Documentos}?status=Voided&loanPublicId={Credito(d)}")).GetProperty("items").EnumerateArray()
            .Should().ContainSingle(x => x.GetProperty("publicId").GetGuid() == id);
    }

    [Fact]
    public async Task La_misma_clave_deja_un_solo_documento()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var admin = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var (_, persona, d) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "DocClave");
        var clave = Guid.NewGuid();

        var primero = await EmitidoAsync(http, admin, Pedido("PaymentPlan", persona, Credito(d)), clave);
        var repetido = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Documentos, Pedido("PaymentPlan", persona, Credito(d)), clave);
        repetido.IsSuccessStatusCode.Should().BeTrue(await repetido.Content.ReadAsStringAsync());

        (await CarteraE2E.LeerAsync(repetido)).GetProperty("documentPublicId").GetGuid().Should().Be(primero.GetProperty("documentPublicId").GetGuid());
        (await CuantosAsync(fx, esc, persona, IssuedDocumentKind.PaymentPlan)).Should().Be(1);
    }

    [Fact]
    public async Task El_paz_y_salvo_general_se_niega_con_saldo_y_con_una_venta_en_reproceso()
    {
        // Con saldo (US13-1): la obligación viva lo niega.
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var admin = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Administrador);
        var (_, persona, d) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "DocGeneral");
        var conSaldo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Documentos, Pedido("GeneralClearance", persona, null));
        (await InventarioE2E.FallaAsync(conSaldo, "Lending.Document.ClearanceDenied")).GetProperty("data").GetProperty("obligations")
            .EnumerateArray().Should().ContainSingle(o => o.GetProperty("loanPublicId").GetGuid() == Credito(d));

        // Con una venta en reproceso (US13-4): una venta quincenal no convertible queda Rejected y reprocesable, y niega el general con su mensaje.
        var ventas = await VentasConCarteraE2E.EscenarioAsync(fx);
        await FirmanteAsync(fx, ventas.TenantPublicId, ventas.Admin, ventas.Hoy);
        var (venta, cobro) = await VentasConCarteraE2E.CobrarACreditoAsync(http, ventas.Ventas, ventas.Cajero, ventas.Sesion, ventas.Rodrigo, 1,
            VentasConCarteraE2E.Credito(ventas.Ventas, 100_000m, cuotas: 2, plazo: 30, cada: 15));
        cobro.StatusCode.Should().Be(HttpStatusCode.OK, await cobro.Content.ReadAsStringAsync());
        await VentasConCarteraE2E.DespacharAsync(fx, ventas);
        var mensaje = (await VentasConCarteraE2E.CreditoDeLaVentaAsync(http, ventas.Cajero, venta)).GetProperty("lendingMessages").EnumerateArray()
            .Single().GetProperty("messagePublicId").GetGuid();
        (await CarteraE2E.GetAsync(http, ventas.Admin, $"{VentasConCarteraE2E.Recibos}/{mensaje}")).GetProperty("receipt").GetProperty("status")
            .GetString().Should().Be("Rejected");

        var enReproceso = await CarteraE2E.ConClaveAsync(http, ventas.Admin, HttpMethod.Post, Documentos, Pedido("GeneralClearance", ventas.Rodrigo, null));
        var negado = await InventarioE2E.FallaAsync(enReproceso, "Lending.Document.ClearanceDenied");
        negado.GetProperty("data").GetProperty("pendingSales").EnumerateArray().Should()
            .Contain(v => v.GetProperty("messagePublicId").GetGuid() == mensaje, "la negativa nombra la venta en reproceso");
    }
}
