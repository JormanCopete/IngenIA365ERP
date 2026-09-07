using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Database;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Database;

/// <summary>
/// Feature 004 (T053, SC-008 / clarificación #2): smoke de aprovisionamiento
/// por motor. La fixture ya arrancó el host real (inicializador → migraciones
/// EF del proveedor + seed paramétrico); aquí se verifica que el esquema quedó
/// COMPLETO y los catálogos sembrados — idéntico en ambos motores (correr con
/// DB_PROVIDER=PostgreSql y DB_PROVIDER=SqlServer).
/// </summary>
public class Provisioning_SmokeTests(CentralIdentityApiFixture fixture)
    : IClassFixture<CentralIdentityApiFixture>
{
    [Fact]
    public async Task Esquema_completo_provisionado_en_el_motor_activo()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Se compara contra el MODELO, no contra un número escrito a mano.
        //
        // El número se quedó viejo en cuanto el corte a base-por-cooperativa
        // sacó las tablas ADM_* del contexto operativo: la prueba pedía 286 y
        // encontraba 277, y lo que delataba no era un esquema incompleto sino
        // su propia constante. Preguntándole al modelo no puede volver a pasar.
        var esperadas = appDb.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        esperadas.Count.Should().BeGreaterThan(250,
            "si el modelo viniera vacío esta prueba pasaría sin comprobar nada");

        // information_schema es ANSI — misma consulta en ambos motores.
        await using var conn = appDb.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'dbo' AND table_type = 'BASE TABLE'";

        var presentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var lector = await cmd.ExecuteReaderAsync())
        {
            while (await lector.ReadAsync()) presentes.Add(lector.GetString(0));
        }

        esperadas.Except(presentes).Should().BeEmpty(
            $"el esquema operativo completo debe existir en {CentralIdentityApiFixture.ProviderKey}");

        // Principio IV: el catálogo administrativo vive FUERA de toda base de
        // cooperativa. Una tabla ADM_* aquí dentro no daría error nunca — daría
        // dos catálogos, uno por cooperativa, divergiendo en silencio.
        presentes.Where(t => t.StartsWith("ADM_", StringComparison.OrdinalIgnoreCase))
            .Should().BeEmpty("la base administrativa es una sola y no se replica por cooperativa");
    }

    [Fact]
    public async Task Catalogos_parametricos_sembrados_y_demo_ausente()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await appDb.SystemSettings.CountAsync()).Should().BeGreaterThanOrEqualTo(5);
        (await appDb.ListParameters.CountAsync(p => p.ListType == "MONE")).Should().BeGreaterThanOrEqualTo(3);
        (await appDb.ListParameters.CountAsync(p => p.ListType == "TDOC")).Should().BeGreaterThanOrEqualTo(6);
        (await appDb.ChartOfAccounts.CountAsync(a => a.Level == 1)).Should().Be(9);

        // RunTestSeed=false en la fixture ⇒ cero datos demo (FR-017/SC-005).
        (await appDb.People.CountAsync(p => p.CreatedBy == "system:seed-demo")).Should().Be(0);
    }

    [Fact]
    public async Task Seed_parametrico_reejecutado_no_duplica_nada()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IDataSeedRunner>();

        // El arranque ya sembró; una segunda pasada completa debe insertar 0.
        var results = await runner.RunAsync("Parametric", null, null, false, CancellationToken.None);

        results.Should().NotBeEmpty();
        results.Sum(r => r.Inserted).Should().Be(0, "la idempotencia exige que re-ejecutar no duplique (SC-006)");
    }
}
