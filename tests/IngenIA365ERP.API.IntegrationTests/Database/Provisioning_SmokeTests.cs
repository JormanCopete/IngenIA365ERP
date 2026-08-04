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

        // information_schema es ANSI — misma consulta en ambos motores.
        await using var conn = appDb.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables " +
            "WHERE table_schema = 'dbo' AND table_type = 'BASE TABLE'";
        var tables = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        tables.Should().BeGreaterThanOrEqualTo(286,
            $"el esquema operativo completo (270 entidades) debe existir en {CentralIdentityApiFixture.ProviderKey}");
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
