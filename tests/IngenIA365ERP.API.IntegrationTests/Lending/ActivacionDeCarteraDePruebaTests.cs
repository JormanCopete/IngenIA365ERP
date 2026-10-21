using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Feature 013, T0148 (C107, C60, C61; api.md §19; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>, cooperativa aislada):
/// <see cref="CarteraE2E.ActivarCarteraAsync"/> deja una cooperativa sin cartera previa activada por las rutas reales
/// (<c>POST /api/lending/migration/loads</c>, <c>…/{id}/apply</c>, <c>POST /api/lending/activation</c>). <c>GET /api/lending/activation</c> la
/// muestra activada; <c>CarteraActivada</c> está en la bandeja; el destino <c>Lending</c> queda habilitado en el registro de esa cooperativa y
/// sigue inhabilitado en otra cooperativa aislada del mismo host (la de <c>CarteraDesplegadaSinActivarTests</c>); y repetir la activación
/// responde <c>Lending.Activation.AlreadyActivated</c>.
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ActivacionDeCarteraDePruebaTests(CentralIdentityApiFixture fx)
{
    private const string Participantes = "/api/core/integration-participants";

    [Fact]
    public async Task Una_cooperativa_sin_cartera_previa_se_activa_por_las_rutas_reales_una_sola_vez()
    {
        var hoy = CarteraE2E.Hoy(fx);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "activacion", primerEjercicioContable: hoy.Year);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;

        // Antes de activar: sin activar y con la lista de requisitos.
        var antes = await CarteraE2E.GetAsync(http, admin, "/api/lending/activation");
        antes.GetProperty("status").GetString().Should().Be("NotActivated");

        var activada = await CarteraE2E.ActivarCarteraAsync(fx, coop);
        activada.ActivationDate.Should().Be(hoy);

        var estado = await CarteraE2E.GetAsync(http, admin, "/api/lending/activation");
        estado.GetProperty("status").GetString().Should().Be("Activated");
        estado.GetProperty("activationPublicId").GetGuid().Should().Be(activada.ActivationPublicId);
        estado.GetProperty("activationDate").GetString().Should().Be(hoy.ToString("yyyy-MM-dd"));

        var carga = await CarteraE2E.GetAsync(http, admin, $"/api/lending/migration/loads/{activada.MigrationLoadPublicId}");
        carga.GetProperty("status").GetInt32().Should().Be(4, "la carga queda Activated");
        carga.GetProperty("loanCount").GetInt32().Should().Be(0);

        // CarteraActivada en la bandeja, con origen la activación; y la carga aplicada, también.
        await using (var db = await ContextoDeLaCooperativaAsync(coop.TenantPublicId))
        {
            var mensajes = await db.Db.IntegrationMessages.AsNoTracking()
                .Where(m => m.OriginModule == "CAR").Select(m => new { m.Type, m.OriginPublicId }).ToListAsync();
            mensajes.Should().ContainSingle(m => m.Type == "CarteraActivada" && m.OriginPublicId == activada.ActivationPublicId);
            mensajes.Should().ContainSingle(m => m.Type == "CargaDeCarteraAplicada" && m.OriginPublicId == activada.MigrationLoadPublicId);
            var activacion = await db.Db.LendingActivations.AsNoTracking().SingleAsync();
            activacion.SessionItemId.Should().BePositive("el acta de activación es obligatoria (C61)");
            (await db.Db.LendingSetups.AsNoTracking().SingleAsync()).ActivationId.Should().Be(activacion.Id);
        }

        // El destino Lending habilitado desde hoy en esta cooperativa.
        DestinoDeCartera(await CarteraE2E.GetAsync(http, admin, $"{Participantes}?destination=Lending&activeOn={hoy:yyyy-MM-dd}"))
            .GetProperty("isEnabled").GetBoolean().Should().BeTrue();

        // En otra cooperativa aislada del mismo host todo sigue igual.
        var otra = await CarteraE2E.CooperativaAisladaAsync(fx, "sinactivar");
        (await CarteraE2E.GetAsync(http, otra.TokenAdmin, "/api/lending/activation")).GetProperty("status").GetString().Should().Be("NotActivated");
        DestinoDeCartera(await CarteraE2E.GetAsync(http, otra.TokenAdmin, $"{Participantes}?destination=Lending&activeOn={hoy:yyyy-MM-dd}"))
            .GetProperty("isEnabled").GetBoolean().Should().BeFalse("la activación de una cooperativa no toca a otra");

        // Repetir la activación.
        var repetir = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/activation", new
        {
            migrationLoadPublicId = activada.MigrationLoadPublicId, activationDate = hoy, sessionItemPublicId = activada.SessionItemPublicId,
            acknowledgeWarnings = true, reason = "Otra vez",
        });
        repetir.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(repetir)).Should().Be("Lending.Activation.AlreadyActivated");

        // Y con Cartera activada no se crean cargas.
        var otraCarga = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/migration/loads", new { cutoffDate = hoy });
        (await CarteraE2E.CodigoDeErrorAsync(otraCarga)).Should().Be("Lending.Activation.AlreadyActivated");
    }

    /// <summary>La fila vigente del destino <c>Lending</c> (código <c>CAR</c>).</summary>
    private static JsonElement DestinoDeCartera(JsonElement registro) =>
        registro.EnumerateArray().Single(p => p.GetProperty("code").GetString() == "CAR" && p.GetProperty("validTo").ValueKind == JsonValueKind.Null);

    private async Task<ContextoDeCooperativa> ContextoDeLaCooperativaAsync(Guid tenantPublicId)
    {
        var alcance = fx.Factory.Services.CreateScope();
        var entrada = (await alcance.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == tenantPublicId);
        var ambito = alcance.ServiceProvider.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        return new ContextoDeCooperativa(alcance, ambito);
    }

    /// <summary>El contexto real de la base de una cooperativa, con su alcance de DI.</summary>
    private sealed class ContextoDeCooperativa(IServiceScope alcance, ITenantDbScope ambito) : IAsyncDisposable
    {
        public ApplicationDbContext Db => (ApplicationDbContext)ambito.Db;

        public async ValueTask DisposeAsync()
        {
            await ambito.DisposeAsync();
            alcance.Dispose();
        }
    }
}
