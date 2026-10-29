using IngenIA365ERP.API.IntegrationTests.Identity.Documento;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0319 (US24 «Independent Test»; quickstart §4; FR-227): la lista de go/no-go de la cooperativa (<c>/api/core/go-live-checklists</c>, permisos
/// <c>Platform.GoLive.View</c>, <c>.Manage</c> y <c>.Override</c>). Las condiciones automáticas las verifica el sistema —los requisitos de la activación de Recaudo y
/// Tesorería, entre ellos la matriz contable al 100 %, los recibos de prueba, el segundo factor y los alcances— y se recalculan al verificar; una condición en rojo
/// se ve (con su evidencia y la alerta <c>Plataforma.GoLiveEnRojo</c>) y no se oculta nunca; «salir» con un rojo se niega
/// (<c>Platform.GoLive.RedWithoutOverride</c>) hasta que el dueño registra, con su permiso y su motivo, la aprobación de seguir con él; sin ese permiso la ruta
/// responde 404 (FR-017). Una condición manual exige su evidencia. La decisión se toma una vez. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ListaDeGoNoGoTests(CentralIdentityApiFixture fx)
{
    private const string Listas = "/api/core/go-live-checklists";

    [Fact]
    public async Task Las_condiciones_se_verifican_en_el_sistema_un_rojo_se_ve_y_seguir_exige_la_aprobacion_del_dueno()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "golive", contabilidadIniciada: true);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);

        // La lista con una condición manual (el cronograma del cambio y los escenarios de no-go parcial, P15).
        var creada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Listas, new
        {
            name = "Salida en vivo de Recaudo y Tesorería (e2e)", environment = "QA", targetDate = hoy.AddDays(30),
            manualItems = new[] { new { module = "Plataforma", code = "P15.Cronograma", description = "Cronograma del cambio y escenarios de no-go parcial con su plan de reversa" } },
        });
        creada.StatusCode.Should().Be(HttpStatusCode.Created, await creada.Content.ReadAsStringAsync());
        var lista = await RecaudoYTesoreriaE2E.LeerAsync(creada);
        var id = lista.GetProperty("publicId").GetGuid();
        RecaudoYTesoreriaE2E.Enum<GoLiveChecklistStatus>(lista.GetProperty("status")).Should().Be(GoLiveChecklistStatus.Open);

        // Las condiciones automáticas las pone el sistema: Recaudo y Tesorería sin configurar ni activar están en rojo, con lo que falta como evidencia.
        var items = Items(lista);
        items.Should().Contain(i => i.Modulo == "REC" && i.Codigo == "REC.REQ.AccountingMatrixComplete", "la matriz contable al 100 % es condición (FR-227)");
        items.Should().Contain(i => i.Modulo == "REC" && i.Codigo == "TestReceiptPerPointAndMeans");
        items.Should().Contain(i => i.Modulo == "TES");
        var punto = items.Single(i => i.Modulo == "REC" && i.Codigo == "REC.REQ.CollectionPoint");
        punto.Estado.Should().Be(GoLiveItemStatus.Red, "sin punto de recaudo");
        punto.Evidencia.Should().StartWith("Falta", "el rojo dice qué falta");
        items.Where(i => i.Tipo == GoLiveItemKind.Automatic).Should().OnlyContain(i => i.VerificadaEl != null, "cada condición automática se verificó al crear la lista");
        var manual = items.Single(i => i.Codigo == "P15.Cronograma");
        manual.Tipo.Should().Be(GoLiveItemKind.Manual);
        manual.Estado.Should().Be(GoLiveItemStatus.Red, "una condición manual nace en rojo hasta que alguien la verifica");
        lista.GetProperty("redWithoutOverride").GetInt32().Should().BeGreaterThan(0);

        // El rojo no se oculta: también avisa a quien puede aprobar seguir.
        var alertas = await InventarioE2E.GetAsync(http, admin, "/api/inventory/alerts?typeCode=Plataforma.GoLiveEnRojo&pageSize=50");
        alertas.GetProperty("items").EnumerateArray().Should().NotBeEmpty("la lista con rojos levanta Plataforma.GoLiveEnRojo");

        // Verificar en el sistema: con un punto de recaudo creado, volver a verificar la condición la pone en verde.
        await ConfiguracionDeRecaudoE2E.PuntoConCajaAsync(http, admin, coop.SucursalPrincipal, "GOLIVE");
        var verificada = await RecaudoYTesoreriaE2E.LeerAsync(await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Listas}/{id}/items/{punto.Id}/check", new { }));
        Items(verificada).Single(i => i.Codigo == "REC.REQ.CollectionPoint" && i.Modulo == "REC").Estado.Should().Be(GoLiveItemStatus.Green,
            "la condición automática se recalcula con lo que hay en el sistema");

        // La manual se marca con su evidencia; sin ella se niega.
        var sinEvidencia = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Listas}/{id}/items/{manual.Id}/check", new { status = "Green" });
        sinEvidencia.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await sinEvidencia.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinEvidencia)).Should().Be("Platform.GoLive.EvidenceRequired");
        var conEvidencia = await RecaudoYTesoreriaE2E.LeerAsync(await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Listas}/{id}/items/{manual.Id}/check",
            new { status = "Green", evidence = "Cronograma aprobado por la gerencia; escenarios de no-go parcial en el acta del comité (e2e)" }));
        Items(conEvidencia).Single(i => i.Codigo == "P15.Cronograma").Estado.Should().Be(GoLiveItemStatus.Green);

        // Con rojos sin excepción, «salir» se niega y dice cuáles.
        var rojos = Items(conEvidencia).Where(i => i.Estado == GoLiveItemStatus.Red).ToList();
        rojos.Should().NotBeEmpty("Recaudo y Tesorería siguen sin activar");
        var salir = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Listas}/{id}/decision", new { decision = "Go", reason = "Queremos salir (e2e)" });
        salir.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await salir.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(salir)).Should().Be("Platform.GoLive.RedWithoutOverride");
        (await RecaudoYTesoreriaE2E.LeerAsync(salir)).GetProperty("data").GetProperty("items").EnumerateArray().Select(x => x.GetString())
            .Should().Contain($"{rojos[0].Modulo}.{rojos[0].Codigo}");

        // Quien administra la lista pero no es el dueño: ve y verifica, pero aprobar seguir con un rojo le responde como una ruta que no existe (FR-017).
        var (gestor, _) = await UsuarioConPermisosAsync(fx, http, coop, "GOLGES", "Platform.GoLive.View", "Platform.GoLive.Manage");
        (await CarteraE2E.MandarGetAsync(http, gestor, $"{Listas}/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var sinPermiso = await CarteraE2E.ConClaveAsync(http, gestor, HttpMethod.Post, $"{Listas}/{id}/items/{rojos[0].Id}/override", new { reason = "Yo decido (e2e)" });
        sinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound, await sinPermiso.Content.ReadAsStringAsync());

        // Aprobar seguir exige motivo; sobre una condición en verde no aplica.
        var sinMotivo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Listas}/{id}/items/{rojos[0].Id}/override", new { reason = "" });
        ((int)sinMotivo.StatusCode).Should().BeOneOf(400, 422);
        var enVerde = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Listas}/{id}/items/{manual.Id}/override", new { reason = "No hace falta (e2e)" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(enVerde)).Should().Be("Platform.GoLive.ItemNotRed");

        // El dueño aprueba seguir con cada rojo, con su motivo: el rojo queda a la vista, aceptado y con quién lo aceptó.
        JsonElement ultima = conEvidencia;
        foreach (var rojo in rojos)
        {
            var aprobada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Listas}/{id}/items/{rojo.Id}/override",
                new { reason = $"Se sale sin «{rojo.Codigo}»: plan de reversa aprobado (e2e)" });
            aprobada.StatusCode.Should().Be(HttpStatusCode.OK, await aprobada.Content.ReadAsStringAsync());
            ultima = await RecaudoYTesoreriaE2E.LeerAsync(aprobada);
        }
        var aceptados = Items(ultima).Where(i => rojos.Any(r => r.Id == i.Id)).ToList();
        aceptados.Should().OnlyContain(i => i.Estado == GoLiveItemStatus.Red, "aprobar seguir no pinta de verde la condición");
        aceptados.Should().OnlyContain(i => i.AprobadaPor != null && i.MotivoDeLaExcepcion != null && i.MotivoDeLaExcepcion.StartsWith("Se sale sin"));
        ultima.GetProperty("redWithoutOverride").GetInt32().Should().Be(0);

        // Ahora «salir» procede con su motivo y queda quién decidió; la decisión no cambia.
        var decidida = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Listas}/{id}/decision",
            new { decision = "Go", reason = "Condiciones verificadas; rojos aceptados por el dueño (e2e)" });
        decidida.StatusCode.Should().Be(HttpStatusCode.OK, await decidida.Content.ReadAsStringAsync());
        var final = await RecaudoYTesoreriaE2E.LeerAsync(decidida);
        RecaudoYTesoreriaE2E.Enum<GoLiveChecklistStatus>(final.GetProperty("status")).Should().Be(GoLiveChecklistStatus.Go);
        final.GetProperty("decidedByName").GetString().Should().NotBeNullOrWhiteSpace();
        final.GetProperty("decisionReason").GetString().Should().StartWith("Condiciones verificadas");
        var otraVez = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Listas}/{id}/decision", new { decision = "NoGo", reason = "Cambio de opinión (e2e)" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Platform.GoLive.AlreadyDecided");
        (await InventarioE2E.GetAsync(http, admin, Listas)).EnumerateArray().Should().Contain(l => l.GetProperty("publicId").GetGuid() == id);
    }

    private sealed record Item(Guid Id, string Modulo, string Codigo, GoLiveItemKind Tipo, GoLiveItemStatus Estado, string? Evidencia, DateTime? VerificadaEl,
        string? AprobadaPor, string? MotivoDeLaExcepcion);

    private static List<Item> Items(JsonElement lista) => lista.GetProperty("items").EnumerateArray().Select(i => new Item(
        i.GetProperty("publicId").GetGuid(), i.GetProperty("module").GetString()!, i.GetProperty("code").GetString()!,
        RecaudoYTesoreriaE2E.Enum<GoLiveItemKind>(i.GetProperty("kind")), RecaudoYTesoreriaE2E.Enum<GoLiveItemStatus>(i.GetProperty("status")),
        Texto(i, "evidence"), i.GetProperty("checkedAt").ValueKind == JsonValueKind.Null ? null : i.GetProperty("checkedAt").GetDateTime(),
        Texto(i, "overrideApprovedByName"), Texto(i, "overrideReason"))).ToList();

    private static string? Texto(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Un usuario invitado a la cooperativa con un rol propio que sólo tiene <paramref name="permisos"/>. Devuelve su token y su <c>PublicId</c>.</summary>
    internal static async Task<(string Token, Guid Usuario)> UsuarioConPermisosAsync(CentralIdentityApiFixture fx, HttpClient http, CarteraE2E.CooperativaAislada coop,
        string codigoDelRol,
        params string[] permisos)
    {
        var admin = coop.TokenAdmin;
        var correo = $"{codigoDelRol.ToLowerInvariant()}.{Guid.NewGuid():N}@coop.recaudo.test";
        var invitacion = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/tenants/{coop.TenantPublicId}/invitations", DocumentoDePrueba.Invitacion(correo));
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, await invitacion.Content.ReadAsStringAsync());
        var token = await CarteraE2E.AceptarInvitacionAsync(fx, http, correo, "Ensayo-GoLive-2026!");
        var usuario = await CarteraE2E.UsuarioPorCorreoAsync(http, admin, correo);
        var rol = await OriginacionE2E.RolPropioAsync(http, admin, codigoDelRol, permisos);
        await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/roles", new { rolePublicId = rol });
        return (token, usuario);
    }
}
