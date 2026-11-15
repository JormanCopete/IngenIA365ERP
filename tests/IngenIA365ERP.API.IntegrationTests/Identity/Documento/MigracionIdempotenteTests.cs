using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T091 y T022), en el motor de <c>DB_PROVIDER</c> y sobre una cooperativa aislada:
/// <list type="number">
///   <item>volver a aplicar las migraciones no cambia nada (no quedan pendientes y migrar otra vez no falla);</item>
///   <item>la cooperativa nueva lleva sellada en su historial <c>UsuarioDeLaPersona</c>, absorbida por <c>EsquemaBase</c> en la
///         consolidación de migraciones del 2026-11-15. El relleno de <c>COR_People.Username</c> y su reversa, que esta prueba
///         recorría bajando la base, se retiraron con la migración: ya corrieron en todas las bases que existían.</item>
/// </list>
/// La tarea de datos <c>identidad-documento</c> vive en el DbMigrator, que este proyecto no referencia: su
/// idempotencia queda para el recorrido a mano (quickstart).
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class MigracionIdempotenteTests(CentralIdentityApiFixture fx)
{
    private const string Migracion = "UsuarioDeLaPersona";

    [Fact]
    public async Task Las_migraciones_se_reaplican_sin_cambios_y_la_absorbida_queda_sellada()
    {
        var esc = new Escenario017(fx);
        using var http = Escenario017.Cliente(fx);
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coop = await esc.CooperativaAsync(http, maestro, "tenant_f017_migra", "900171601");

        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;

        // (1) La base administrativa: nada pendiente y migrar otra vez no hace nada.
        var admin = servicios.GetRequiredService<AdminDbContext>();
        Assert.Empty(await admin.Database.GetPendingMigrationsAsync());
        await admin.Database.MigrateAsync();

        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == coop);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (ApplicationDbContext)ambito.Db;
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await db.Database.MigrateAsync();

        // (2) Consolidación de migraciones (2026-11-15): UsuarioDeLaPersona quedó absorbida por EsquemaBase. La cooperativa
        // nueva nació de la base y lleva sellada la absorbida en su historial, igual que una que vino por el camino viejo.
        Assert.Equal(EstadoDeConsolidacion.Alineada, (await ConsolidacionDeMigraciones.DiagnosticarAsync(db, CancellationToken.None)).Estado);
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_" + Migracion, StringComparison.Ordinal));
    }
}
