using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// T418 (quickstart §3.13 pasos 3 y 4; US12-2, US12-6, US2-4; FR-009, FR-010), en el motor de <c>DB_PROVIDER</c>, sobre el
/// escenario aislado «multinivel» con los usuarios del ensayo (<see cref="UsuariosDelEnsayo"/>): un ajuste negativo de
/// 3.000.000 pasa por dos niveles —<c>aprobador</c> el 1, <c>jefe</c> el 2— y se numera al aprobarse el último; quien ya aprobó
/// un nivel no aprueba el siguiente; rechazar exige motivo y devuelve a borrador; una huella vieja invalida la solicitud; y el
/// comprador con límite de 5.000.000 confirma una compra de 8.000.000 que exige aprobación, o, con un tipo sin política, recibe
/// <c>Inventory.Approval.AmountExceedsLimit</c> con el máximo.
/// <para>
/// Una precisión frente a quickstart §3.13: <c>bodega.a</c> no tiene el permiso del nivel, y api.md §15.2 dice que decidir exige
/// ese permiso antes que la segregación («sin ellos, 404 igual que una solicitud inexistente»); por eso su intento es 404, no
/// <c>Approvals.SelfApprovalForbidden</c>. La segregación se prueba con quien sí tiene el permiso (el creador administrador y el
/// <c>jefe</c> que aprobó el nivel 1).
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class AprobacionMultinivelTests(CentralIdentityApiFixture fx)
{
    private const string Ajustes = "/api/inventory/adjustments";
    private const decimal Umbral2 = 1_000_000m;

    private async Task<(EscenarioDeInventario Esc, UsuariosDelEnsayo U)> PrepararAsync()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "multinivel");
        var u = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        var politicas = await InventarioE2E.GetAsync(http, esc.Admin, $"/api/inventory/approval-policies?documentTypePublicId={esc.Tipos["AJN"]}");
        if (politicas.GetArrayLength() == 0)
        {
            await InventarioE2E.PoliticaAsync(http, esc.Admin, esc.Tipos["AJN"], esc.Corte.AddDays(1),
                (0m, "Inventory.Adjustments.Approve"), (Umbral2, "Inventory.Approvals.Management"));
            await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 10_000, 2_000m)]);
        }
        return (esc, u);
    }

    [Fact]
    public async Task Un_ajuste_de_tres_millones_pasa_por_dos_niveles_y_se_numera_con_el_ultimo()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        var antes = await esc.FisicaAsync(http, esc.Admin, "P1", "PRIN");

        var id = await esc.AjusteAsync(http, u.BodegaA.Token, "AJN", "PRIN", [new("P1", 1_500)], causa: "MERMA");
        var enviado = await InventarioE2E.ConfirmarAsync(http, u.BodegaA.Token, Ajustes, id);
        enviado.GetProperty("status").GetInt32().Should().Be(1, "PendingApproval");
        enviado.GetProperty("number").ValueKind.Should().Be(JsonValueKind.Null);
        enviado.GetProperty("approval").GetProperty("levels").GetArrayLength().Should().Be(2);
        (await esc.FisicaAsync(http, esc.Admin, "P1", "PRIN")).Should().Be(antes, "en aprobación no toca la existencia");
        var solicitud = enviado.GetProperty("approval").GetProperty("requestPublicId").GetGuid();
        var alertas = await InventarioE2E.GetAsync(http, u.Aprobador.Token, "/api/inventory/alerts?typeCode=Aprobaciones.Pendiente&status=Pending&pageSize=50");
        alertas.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0, "la solicitud alerta a los titulares del nivel pendiente");

        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.BodegaA.Token, solicitud, aprobar: true)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "bodega.a no tiene el permiso del nivel: indistinguible de inexistente (api.md §15.2)");

        var nivel1 = await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, solicitud, aprobar: true);
        nivel1.StatusCode.Should().Be(HttpStatusCode.OK, await nivel1.Content.ReadAsStringAsync());
        (await InventarioE2E.GetAsync(http, esc.Admin, $"{Ajustes}/{id}")).GetProperty("status").GetInt32().Should().Be(1, "sigue en aprobación");
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, solicitud, aprobar: true)).StatusCode.Should().NotBe(HttpStatusCode.OK,
            "el aprobador no puede aprobar también el nivel 2");

        var nivel2 = await InventarioE2E.DecidirAsync(http, esc.Admin, u.Jefe.Token, solicitud, aprobar: true);
        nivel2.StatusCode.Should().Be(HttpStatusCode.OK, await nivel2.Content.ReadAsStringAsync());
        var confirmado = await InventarioE2E.GetAsync(http, esc.Admin, $"{Ajustes}/{id}");
        confirmado.GetProperty("status").GetInt32().Should().Be(2, "Confirmed");
        confirmado.GetProperty("number").ValueKind.Should().Be(JsonValueKind.Number, "se numera en la transacción de la última aprobación");
        (await esc.FisicaAsync(http, esc.Admin, "P1", "PRIN")).Should().Be(antes - 1_500m);
    }

    [Fact]
    public async Task Quien_aprobo_un_nivel_no_aprueba_el_siguiente_y_el_creador_no_decide()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        var id = await esc.AjusteAsync(http, esc.Admin, "AJN", "PRIN", [new("P1", 600)], causa: "MERMA");
        var solicitud = (await InventarioE2E.ConfirmarAsync(http, esc.Admin, Ajustes, id)).GetProperty("approval").GetProperty("requestPublicId").GetGuid();

        await InventarioE2E.FallaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Admin, solicitud, aprobar: true), "Approvals.SelfApprovalForbidden");
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.Jefe.Token, solicitud, aprobar: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        var otraVez = await InventarioE2E.DecidirAsync(http, esc.Admin, u.Jefe.Token, solicitud, aprobar: true);
        var datos = (await InventarioE2E.FallaAsync(otraVez, "Approvals.SelfApprovalForbidden")).GetProperty("data");
        datos.GetProperty("reason").ToString().Should().BeOneOf("PreviousLevel", "4");
    }

    [Fact]
    public async Task Rechazar_exige_motivo_y_devuelve_a_borrador_y_una_huella_vieja_invalida_la_solicitud()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();

        var id = await esc.AjusteAsync(http, u.BodegaA.Token, "AJN", "PRIN", [new("P1", 10)], causa: "MERMA");
        var solicitud = (await InventarioE2E.ConfirmarAsync(http, u.BodegaA.Token, Ajustes, id)).GetProperty("approval").GetProperty("requestPublicId").GetGuid();
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, solicitud, aprobar: false)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, solicitud, aprobar: false, motivo: "La merma no está soportada"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await InventarioE2E.GetAsync(http, esc.Admin, $"{Ajustes}/{id}")).GetProperty("status").GetInt32().Should().Be(0, "vuelve a borrador");

        var otro = await esc.AjusteAsync(http, u.BodegaA.Token, "AJN", "PRIN", [new("P1", 10)], causa: "MERMA");
        var otraSolicitud = (await InventarioE2E.ConfirmarAsync(http, u.BodegaA.Token, Ajustes, otro)).GetProperty("approval").GetProperty("requestPublicId").GetGuid();
        var vieja = await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, otraSolicitud, aprobar: true, huella: new string('0', 64));
        await InventarioE2E.FallaAsync(vieja, "Approvals.Request.ContentChanged");
    }

    [Fact]
    public async Task El_comprador_sobre_su_limite_pasa_por_aprobacion_y_sin_politica_recibe_el_maximo()
    {
        var (esc, u) = await PrepararAsync();
        using var http = fx.CreateClient();
        var proveedor = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, esc.Admin, "ProveedorGrande");
        await InventarioE2E.PoliticaAsync(http, esc.Admin, esc.Tipos["REC"], esc.Corte.AddDays(1), (20_000_000m, "Inventory.Purchases.Approve"));

        var compra = await CompraAsync(http, esc, u.Comprador.Token, esc.Tipos["REC"], proveedor, 8_000_000m);
        compra.StatusCode.Should().Be(HttpStatusCode.Created, await compra.Content.ReadAsStringAsync());
        var cuerpo = await InventarioE2E.LeerAsync(compra);
        cuerpo.GetProperty("status").GetInt32().Should().Be(1, "8.000.000 supera el límite de 5.000.000 del comprador: exige el nivel 1 aunque la política empiece en 20.000.000");
        cuerpo.GetProperty("approval").GetProperty("reason").ToString().Should().BeOneOf("AmountLimit", "2");

        var sinPolitica = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/document-types", new
        {
            code = "RECSP", name = "Recepción sin política", @class = "PurchaseReceipt", prefix = "RS",
        });
        var rechazada = await CompraAsync(http, esc, u.Comprador.Token, sinPolitica.GetProperty("publicId").GetGuid(), proveedor, 8_000_000m);
        var datos = (await InventarioE2E.FallaAsync(rechazada, "Inventory.Approval.AmountExceedsLimit")).GetProperty("data");
        datos.GetProperty("maxAmount").GetDecimal().Should().Be(UsuariosDelEnsayo.LimiteDelComprador);
    }

    private static int _factura = 5_000;

    private static Task<HttpResponseMessage> CompraAsync(HttpClient http, EscenarioDeInventario esc, string token, Guid tipoDeRecepcion, Guid proveedor, decimal total) =>
        InventarioE2E.MandarAsync(http, token, HttpMethod.Post, "/api/inventory/purchases/direct", new
        {
            receipt = new
            {
                documentTypePublicId = tipoDeRecepcion, warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor,
                lines = new[] { new { productPublicId = esc.P("P2").Id, unitPublicId = esc.P("P2").Unidad, quantity = 1_000m, unitPrice = total / 1_000m } },
            },
            invoice = new
            {
                documentTypePublicId = esc.Tipos["FCP"],
                supplier = new { prefix = "GR", number = Interlocked.Increment(ref _factura).ToString(), issueDate = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd"),
                    paymentForm = "Cash", isElectronic = false },
            },
        });
}
