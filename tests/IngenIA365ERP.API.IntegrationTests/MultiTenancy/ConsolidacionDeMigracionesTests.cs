using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Initialization;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.IntegrationTests.MultiTenancy;

/// <summary>
/// Consolidación de migraciones (2026-11-15), en el motor de <c>DB_PROVIDER</c>, sobre una base propia del servidor de la
/// fixture: <see cref="ConsolidacionDeMigraciones"/> deja el mismo historial venga la base de donde venga, y se niega con una
/// base a medias sin escribir nada.
///
/// <para>Las absorbidas ya no están en el compilado, así que una «base vieja» se fabrica quitándole a una base nueva la fila de
/// la base: es exactamente lo que tiene una base que vino por las absorbidas (sus filas y su esquema, que la comparación de catálogos
/// del 2026-11-15 mostró idénticos).</para>
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class ConsolidacionDeMigracionesTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_historial_queda_igual_venga_la_base_de_donde_venga_y_una_base_a_medias_no_se_toca()
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var opciones = alcance.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var configurador = alcance.ServiceProvider.GetRequiredService<IDbProviderConfigurator>();
        var nombre = "consolidacion_" + Guid.NewGuid().ToString("N")[..8];
        var constructor = new DbContextOptionsBuilder<ApplicationDbContext>();
        configurador.Configure(constructor,
            ConnectionStringTargeting.ConCatalogo(opciones.GetActiveConnectionString(), nombre), MigrationsTarget.Application);
        await using var db = new ApplicationDbContext(constructor.Options);
        var registro = NullLogger.Instance;
        var consolidacion = ConsolidacionDeMigraciones.DelContexto(db)!;
        consolidacion.Should().NotBeNull("el árbol operativo está consolidado en los dos motores");
        var historial = db.GetService<IHistoryRepository>();

        // (1) Base nueva: EF aplica sólo la base y después se sellan las absorbidas.
        (await ConsolidacionDeMigraciones.AlinearAsync(db, nombre, registro, CancellationToken.None)).Should().Be(EstadoDeConsolidacion.BaseNueva);
        (await db.Database.GetPendingMigrationsAsync()).Should().Equal(consolidacion.IdDeLaBase);
        await db.Database.MigrateAsync();
        (await ConsolidacionDeMigraciones.AlinearAsync(db, nombre, registro, CancellationToken.None)).Should().Be(EstadoDeConsolidacion.PorSellar);
        (await db.Database.GetAppliedMigrationsAsync()).Should().HaveCount(consolidacion.Absorbidas.Count + 1);
        (await ConsolidacionDeMigraciones.DiagnosticarAsync(db, CancellationToken.None)).Estado.Should().Be(EstadoDeConsolidacion.Alineada);

        // (2) Base que vino por las absorbidas (sin la fila de la base): se marca y no queda nada pendiente. Migrar no hace nada.
        await db.Database.ExecuteSqlRawAsync(historial.GetDeleteScript(consolidacion.IdDeLaBase));
        (await db.Database.GetPendingMigrationsAsync()).Should().Equal(consolidacion.IdDeLaBase);
        (await ConsolidacionDeMigraciones.AlinearAsync(db, nombre, registro, CancellationToken.None)).Should().Be(EstadoDeConsolidacion.PorMarcar);
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty("la base quedó marcada sin ejecutar nada");
        await db.Database.MigrateAsync();
        (await ConsolidacionDeMigraciones.AlinearAsync(db, nombre, registro, CancellationToken.None)).Should().Be(EstadoDeConsolidacion.Alineada);

        // (3) Sello interrumpido (tiene la base, le faltan absorbidas): se completa.
        foreach (var id in consolidacion.Absorbidas.Take(10))
            await db.Database.ExecuteSqlRawAsync(historial.GetDeleteScript(id));
        (await ConsolidacionDeMigraciones.AlinearAsync(db, nombre, registro, CancellationToken.None)).Should().Be(EstadoDeConsolidacion.PorSellar);
        (await ConsolidacionDeMigraciones.DiagnosticarAsync(db, CancellationToken.None)).Estado.Should().Be(EstadoDeConsolidacion.Alineada);

        // (4) Base a medias (sin la base y sin la última absorbida): se niega y no escribe nada.
        await db.Database.ExecuteSqlRawAsync(historial.GetDeleteScript(consolidacion.IdDeLaBase));
        await db.Database.ExecuteSqlRawAsync(historial.GetDeleteScript(consolidacion.Absorbidas[^1]));
        var antes = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var fallo = await Record.ExceptionAsync(() => ConsolidacionDeMigraciones.AlinearAsync(db, nombre, registro, CancellationToken.None));
        fallo.Should().BeOfType<InvalidOperationException>().Which.Message
            .Should().Contain("[Database.ConsolidacionIncompleta]").And.Contain(consolidacion.Absorbidas[^1]);
        (await db.Database.GetAppliedMigrationsAsync()).Should().Equal(antes, "una base a medias no se toca");

        await db.Database.EnsureDeletedAsync();
    }
}
