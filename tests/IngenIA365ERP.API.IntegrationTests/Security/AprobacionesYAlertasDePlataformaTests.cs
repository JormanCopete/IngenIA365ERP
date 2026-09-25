using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// T024 (feature 012; decisiones-transversales T33, T34, T39; SC-022; contracts/api.md §15, §16): la plataforma de
/// aprobaciones y alertas por HTTP, en PostgreSQL y SQL Server.
///
/// <para>
/// Escrita en la fase 2 por la sección de aprobaciones (T079–T086), que pone los casos de políticas
/// (<c>/api/inventory/approval-policies</c>, con <c>Overlaps</c> y <c>LevelsInvalid</c>), montos máximos
/// (<c>/api/inventory/amount-limits</c>, con <c>NotLimitable</c>) y bandeja. Los casos de alertas (una alerta
/// levantada por <c>IEjecutorEnCooperativa</c> que llega a la bandeja y a las notificaciones de quien tiene el permiso,
/// <c>WithoutRecipient</c> a <c>CompanyAdmin</c>, atender y <c>Alerts.Alert.AlreadyAttended</c>) y el alcance vacío
/// de un usuario sin filas los agregan a este mismo archivo las secciones de alertas (T091–T095) y de alcance
/// (T087–T090).
/// </para>
///
/// <para>
/// Se ejecuta tras la migración <c>PlataformaParaInventario</c> (T186), que crea <c>COR_Approval*</c> y
/// <c>SEC_PermissionAmountLimits</c>, y con los permisos <c>Inventory.*</c> sembrados (fase 3, T48): sin ellos las
/// rutas responden 404 a todos salvo al maestro, y el rol <c>CompanyAdmin</c> no concede
/// <c>Inventory.Adjustments.Confirm</c>.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class AprobacionesYAlertasDePlataformaTests(CentralIdentityApiFixture fx)
{
    private const string Politicas = "/api/inventory/approval-policies";
    private const string Montos = "/api/inventory/amount-limits";

    private static async Task<HttpResponseMessage> PostAsync(HttpClient http, string token, string url, object cuerpo, bool conClave = true)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(cuerpo) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (conClave) InventarioE2E.ConClave(req);
        return await http.SendAsync(req);
    }

    private static object Politica(string validFrom, params object[] niveles) => new
    {
        subject = "DocumentConfirmation",
        validFrom,
        reason = "Acta 7 del consejo (T024)",
        levels = niveles,
    };

    private static async Task<JsonElement> DatosAsync(HttpResponseMessage resp) =>
        (await InventarioE2E.LeerAsync(resp)).GetProperty("data");

    // ----------------------------------------------------------------------------------- políticas §15.1 --

    [Fact]
    public async Task Una_politica_de_dos_niveles_se_registra_y_se_consulta()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "aprobpol");
        using var http = fx.CreateClient();

        var alta = await PostAsync(http, coop.TokenAdmin, Politicas, Politica("2026-10-01",
            new { order = 1, threshold = 0m, permissionCode = "Inventory.Approvals.Supervisor" },
            new { order = 2, threshold = 1_000_000m, permissionCode = "Inventory.Approvals.Management" }));

        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var creada = await InventarioE2E.LeerAsync(alta);
        creada.GetProperty("module").GetString().Should().Be("Inventory");
        creada.GetProperty("version").GetInt32().Should().Be(1);
        creada.GetProperty("levels").GetArrayLength().Should().Be(2);

        var vigentes = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{Politicas}?subject=DocumentConfirmation&asOf=2026-10-15");
        vigentes.EnumerateArray().Should().ContainSingle(p => p.GetProperty("publicId").GetGuid() == creada.GetProperty("publicId").GetGuid());
        var antes = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{Politicas}?subject=DocumentConfirmation&asOf=2026-09-15");
        antes.GetArrayLength().Should().Be(0, "no rige antes de su validFrom");
    }

    [Fact]
    public async Task Una_version_del_mismo_dia_o_anterior_es_Overlaps()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "aprobcruce");
        using var http = fx.CreateClient();
        var nivel = new { order = 1, threshold = 0m, permissionCode = "Inventory.Approvals.Supervisor" };
        (await PostAsync(http, coop.TokenAdmin, Politicas, Politica("2026-10-01", nivel))).StatusCode.Should().Be(HttpStatusCode.Created);

        var cruce = await PostAsync(http, coop.TokenAdmin, Politicas, Politica("2026-09-01", nivel));
        var siguiente = await PostAsync(http, coop.TokenAdmin, Politicas, Politica("2026-11-01"));

        cruce.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await InventarioE2E.CodigoDeErrorAsync(cruce)).Should().Be("Approvals.Policy.Overlaps");
        siguiente.StatusCode.Should().Be(HttpStatusCode.Created, "levels: [] desde una fecha posterior es una versión sin aprobación");

        var historia = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{Politicas}?subject=DocumentConfirmation&includeHistory=true");
        historia.EnumerateArray().Select(p => p.GetProperty("validTo").ValueKind == JsonValueKind.Null ? null : p.GetProperty("validTo").GetString())
            .Should().Equal("2026-10-31", null);
    }

    [Theory]
    [InlineData(1, 100, 2, 50, "umbral")]
    [InlineData(1, 0, 3, 100, "orden")]
    public async Task Niveles_mal_formados_son_LevelsInvalid(int orden1, int umbral1, int orden2, int umbral2, string motivo)
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "aprobniveles");
        using var http = fx.CreateClient();

        var resp = await PostAsync(http, coop.TokenAdmin, Politicas, Politica("2026-10-01",
            new { order = orden1, threshold = (decimal)umbral1, permissionCode = "Inventory.Approvals.Supervisor" },
            new { order = orden2, threshold = (decimal)umbral2, permissionCode = "Inventory.Approvals.Management" }));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var cuerpo = await InventarioE2E.LeerAsync(resp);
        cuerpo.GetProperty("code").GetString().Should().Be("Approvals.Policy.LevelsInvalid");
        cuerpo.GetProperty("data").GetProperty("reason").GetString().Should().Contain(motivo);
    }

    [Fact]
    public async Task Sin_Idempotency_Key_el_alta_es_Operation_KeyRequired()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "aprobpol");
        using var http = fx.CreateClient();

        var resp = await PostAsync(http, coop.TokenAdmin, Politicas, Politica("2027-01-01"), conClave: false);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InventarioE2E.CodigoDeErrorAsync(resp)).Should().Be("Operation.KeyRequired");
    }

    [Fact]
    public async Task Un_sujeto_fuera_del_catalogo_es_Validation_Invalid()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "aprobpol");
        using var http = fx.CreateClient();

        var resp = await PostAsync(http, coop.TokenAdmin, Politicas, new { subject = "Otro", validFrom = "2027-02-01", reason = "x", levels = Array.Empty<object>() });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InventarioE2E.CodigoDeErrorAsync(resp)).Should().Be("Validation.Invalid");
    }

    // ------------------------------------------------------------------------------ montos máximos §15.3 --

    [Fact]
    public async Task Un_monto_maximo_se_registra_y_se_consulta_y_un_permiso_sin_valor_es_NotLimitable()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "aprobmontos");
        using var http = fx.CreateClient();
        var roles = await InventarioE2E.GetAsync(http, coop.TokenAdmin, "/api/admin/roles?includeBuiltIn=true&pageSize=100");
        var admin = roles.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "CompanyAdmin");
        var rol = admin.GetProperty("publicId").GetGuid();

        var alta = await PostAsync(http, coop.TokenAdmin, Montos, new
        {
            rolePublicId = rol, permissionCode = "Inventory.Adjustments.Confirm", maxAmount = 2_000_000m, validFrom = "2026-10-01", reason = "Acta 7 (T024)",
        });
        var noLimitable = await PostAsync(http, coop.TokenAdmin, Montos, new
        {
            rolePublicId = rol, permissionCode = "Inventory.Catalog.Manage", maxAmount = 10m, validFrom = "2026-10-01", reason = "Acta 7 (T024)",
        });
        var rolInexistente = await PostAsync(http, coop.TokenAdmin, Montos, new
        {
            rolePublicId = Guid.NewGuid(), permissionCode = "Inventory.Adjustments.Confirm", maxAmount = 10m, validFrom = "2026-10-01", reason = "Acta 7 (T024)",
        });

        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var creado = await InventarioE2E.LeerAsync(alta);
        creado.GetProperty("currency").GetString().Should().Be("COP");
        creado.GetProperty("role").GetProperty("publicId").GetGuid().Should().Be(rol);

        noLimitable.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await InventarioE2E.CodigoDeErrorAsync(noLimitable)).Should().Be("Approvals.AmountLimit.NotLimitable");
        (await DatosAsync(noLimitable)).GetProperty("limitable").EnumerateArray().Select(e => e.GetString())
            .Should().Contain("Inventory.Adjustments.Confirm");

        rolInexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var vigentes = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{Montos}?rolePublicId={rol}&asOf=2026-10-15");
        vigentes.EnumerateArray().Should().ContainSingle().Which.GetProperty("maxAmount").GetDecimal().Should().Be(2_000_000m);
    }

    // ----------------------------------------------------------------------------------- bandeja §15.2 --

    [Fact]
    public async Task La_bandeja_sin_solicitudes_responde_una_pagina_vacia_y_una_solicitud_ajena_es_404()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "aprobpol");
        using var http = fx.CreateClient();

        var bandeja = await InventarioE2E.GetAsync(http, coop.TokenAdmin, "/api/inventory/approvals?mine=true");
        using var detalle = new HttpRequestMessage(HttpMethod.Get, $"/api/inventory/approvals/{Guid.NewGuid()}");
        detalle.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coop.TokenAdmin);
        var inexistente = await http.SendAsync(detalle);

        bandeja.GetProperty("totalCount").GetInt64().Should().Be(0);
        bandeja.GetProperty("items").GetArrayLength().Should().Be(0);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await InventarioE2E.CodigoDeErrorAsync(inexistente)).Should().Be("Approvals.Request.NotFound");
    }
}
