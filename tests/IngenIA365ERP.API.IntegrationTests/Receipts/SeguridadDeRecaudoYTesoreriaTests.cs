using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0056 (US12 «Independent Test»; SC-018, SC-019): el alcance falla cerrado y responde igual que lo inexistente; una ruta sin permiso responde
/// 403; la auditoría de Recaudo queda en la cadena de sellos y se verifica sin incidentes. El aprobador presente con la misma clave lo recorre la e2e de la
/// caja (<c>CajaAbrirArquearYCerrarTests</c>, la diferencia de arqueo) y la alteración de un evento la cubre <c>IntegridadDeAuditoriaTests</c> para todo
/// módulo encadenado. Se corre en la fase 22.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class SeguridadDeRecaudoYTesoreriaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_alcance_falla_cerrado_la_ruta_sin_permiso_es_403_y_la_auditoria_verifica()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "seguridad");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var (puntoA, _) = await ConfiguracionDeRecaudoE2E.PuntoConCajaAsync(http, admin, coop.SucursalPrincipal, "PTA");
        var (_, cajaB) = await ConfiguracionDeRecaudoE2E.PuntoConCajaAsync(http, admin, coop.SucursalPrincipal, "PTB");

        // Un supervisor de un solo punto (sin Receipts.Scope.All).
        var correo = $"supervisor.{Guid.NewGuid():N}@coop.co";
        var invitacion = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/tenants/{coop.TenantPublicId}/invitations", new { email = correo });
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, await invitacion.Content.ReadAsStringAsync());
        var token = await CarteraE2E.AceptarInvitacionAsync(fx, http, correo, "Ensayo-Recaudo-2026!");
        var usuario = await CarteraE2E.UsuarioPorCorreoAsync(http, admin, correo);
        var rol = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/admin/roles/from-template",
            new { templateKey = "recaudo.supervisor", code = "RSUP", name = "Supervisor de prueba" });
        await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/roles", new { rolePublicId = rol.GetProperty("rolePublicId").GetGuid() });
        await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Put, $"/api/receipts/users/{usuario}/scope",
            new { collectionPointPublicIds = new[] { puntoA }, cashBoxPublicIds = Array.Empty<Guid>(), paymasterPersonPublicIds = Array.Empty<Guid>() });

        // Lista sólo su punto; la caja del otro punto responde como la inexistente.
        var puntos = await InventarioE2E.GetAsync(http, token, "/api/receipts/collection-points");
        puntos.EnumerateArray().Select(p => p.GetProperty("code").GetString()).Should().Equal("PTA");
        var fuera = await CarteraE2E.MandarGetAsync(http, token, $"/api/receipts/cash-boxes/{cajaB}/cashiers");
        var inexistente = await CarteraE2E.MandarGetAsync(http, token, $"/api/receipts/cash-boxes/{Guid.NewGuid()}/cashiers");
        fuera.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(fuera)).Should().Be(await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(inexistente));

        // Sin el permiso de asignar alcances, 403.
        var sinPermiso = await CarteraE2E.MandarGetAsync(http, token, $"/api/receipts/users/{usuario}/scope");
        sinPermiso.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // La configuración quedó en la cadena de sellos y verifica.
        await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);
        var auditoria = await InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/audit/integrity/verify",
            new { from = DateTime.UtcNow.AddHours(-2), to = DateTime.UtcNow.AddHours(1) });
        auditoria.GetProperty("incidents").GetArrayLength().Should().Be(0);
        auditoria.GetProperty("checked").GetInt32().Should().BeGreaterThan(0);
    }
}
