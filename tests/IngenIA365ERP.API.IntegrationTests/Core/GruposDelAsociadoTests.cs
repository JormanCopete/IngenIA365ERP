using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Payroll;
using static IngenIA365ERP.API.IntegrationTests.Payroll.NominaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Core;

/// <summary>
/// Grupos del asociado, por HTTP sobre la cooperativa compartida de nómina: alta con código único (y la consulta de
/// código repetido), asociado nuevo con grupo en un paso, filtro del listado por grupo, y un grupo en uso que no se inactiva.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class GruposDelAsociadoTests(CentralIdentityApiFixture fx)
{
    private static int _documento = 810_000_000;

    private static async Task<HttpResponseMessage> EscribirAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo)
    {
        using var req = new HttpRequestMessage(metodo, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
        return await http.SendAsync(req);
    }

    [Fact]
    public async Task Editar_un_centro_de_costo_sin_enviar_codigo_no_lo_borra()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var codigo = "C" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var alta = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/core/cost-centers", new { code = codigo.ToLowerInvariant(), name = "Centro de prueba" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var id = (await LeerAsync(alta)).GetGuid();

        var edicion = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Put, $"/api/core/cost-centers/{id}", new { name = "Centro renombrado" });
        edicion.StatusCode.Should().Be(HttpStatusCode.NoContent, await edicion.Content.ReadAsStringAsync());

        var leido = await GetAsync(http, ctx.TokenAdmin, $"/api/core/cost-centers/{id}");
        leido.GetProperty("code").GetString().Should().Be(codigo);
        leido.GetProperty("name").GetString().Should().Be("Centro renombrado");
        (await GetAsync(http, ctx.TokenAdmin, $"/api/core/cost-centers?SearchTerm={codigo.ToLowerInvariant()}")).GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task El_asociado_se_registra_con_su_grupo_y_el_listado_filtra_por_el()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var codigo = "G" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var alta = await EscribirAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/core/associate-groups", new { code = codigo.ToLowerInvariant(), name = "Gremio de prueba" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var grupo = await LeerAsync(alta);
        grupo.GetProperty("code").GetString().Should().Be(codigo, "se guarda en mayúsculas");
        var grupoId = grupo.GetProperty("publicId").GetGuid();

        var repetido = await EscribirAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/core/associate-groups", new { code = codigo, name = "Otro" });
        repetido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await repetido.Content.ReadAsStringAsync());
        (await LeerAsync(repetido)).GetProperty("code").GetString().Should().Be("Catalogo.CodigoDuplicado");

        var existe = await GetAsync(http, ctx.TokenAdmin, $"/api/catalogos/grupos-del-asociado/codigo/{codigo}");
        existe.GetProperty("existe").GetBoolean().Should().BeTrue();

        var documento = Interlocked.Increment(ref _documento).ToString();
        var asociado = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new { idType = "C", taxId = documento, firstName = "Gina", lastName = "Grupal", email = $"gina.{documento}@coop.nomina.test" },
            associate = new { joinDate = new DateOnly(2026, 9, 15), contributionRate = 5m, associateGroupPublicId = grupoId },
        });
        asociado.StatusCode.Should().Be(HttpStatusCode.Created, await asociado.Content.ReadAsStringAsync());
        var personaId = (await LeerAsync(asociado)).GetProperty("personPublicId").GetGuid();

        (await GetAsync(http, ctx.TokenAdmin, $"/api/core/associates/by-person/{personaId}"))
            .GetProperty("associateGroupPublicId").GetGuid().Should().Be(grupoId);

        var filtrado = await GetAsync(http, ctx.TokenAdmin, $"/api/core/associates?PageNumber=1&PageSize=20&AssociateGroupPublicId={grupoId}");
        filtrado.GetProperty("items").GetArrayLength().Should().Be(1);
        filtrado.GetProperty("items")[0].GetProperty("associateGroupCode").GetString().Should().Be(codigo);

        // En uso por un asociado: no se inactiva.
        var inactivar = await EscribirAsync(http, ctx.TokenAdmin, HttpMethod.Post, $"/api/core/associate-groups/{grupoId}/deactivate", new { reason = "prueba" });
        inactivar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await inactivar.Content.ReadAsStringAsync());
        (await LeerAsync(inactivar)).GetProperty("code").GetString().Should().Be("AssociateGroup.InUse");
    }
}