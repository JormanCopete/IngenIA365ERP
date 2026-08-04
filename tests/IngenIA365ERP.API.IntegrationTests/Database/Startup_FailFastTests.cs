using FluentAssertions;
using IngenIA365ERP.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Database;

/// <summary>
/// Feature 004 — T022. Verifica el contrato fail-fast de la seccion Database
/// (FR-003, SC-004): un host con configuracion invalida NO arranca y el error
/// nombra el codigo Database.* exacto. No requiere contenedores: la validacion
/// ocurre via ValidateOnStart al iniciar el host generico.
/// </summary>
public class Startup_FailFastTests
{
    private static IHost BuildHost(Dictionary<string, string?> settings) =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(settings))
            .ConfigureServices((ctx, services) => services.AddPersistenceServices(ctx.Configuration))
            .Build();

    [Fact]
    public async Task ProviderInvalido_ElHostNoArranca_YNombraElCodigo()
    {
        using var host = BuildHost(new()
        {
            ["Database:Provider"] = "Oracle",
            ["Database:ConnectionStrings:PostgreSQL"] = "Host=x;Database=y;Username=u;Password=p"
        });

        var act = () => host.StartAsync();

        var ex = await act.Should().ThrowAsync<OptionsValidationException>();
        ex.Which.Message.Should().Contain("Database.InvalidProvider")
            .And.Contain("Oracle")
            .And.Contain("PostgreSQL, SqlServer");
    }

    [Fact]
    public async Task ConnectionStringFaltante_ElHostNoArranca_NombrandoLaClave()
    {
        using var host = BuildHost(new()
        {
            ["Database:Provider"] = "PostgreSQL"
            // Sin Database:ConnectionStrings:PostgreSQL
        });

        var act = () => host.StartAsync();

        var ex = await act.Should().ThrowAsync<OptionsValidationException>();
        ex.Which.Message.Should().Contain("Database.ConnectionStringMissing")
            .And.Contain("Database:ConnectionStrings:PostgreSQL");
    }

    [Fact]
    public async Task BdInaccesible_AgotadaLaVentana_FallaConDatabaseUnreachable()
    {
        // T032/FR-010: servidor inexistente + ventana corta → el inicializador
        // reintenta y termina con Database.Unreachable (no MigrationFailed).
        using var host = BuildHost(new()
        {
            ["Database:Provider"] = "SqlServer",
            ["Database:ConnectionStrings:SqlServer"] =
                "Server=localhost,59999;Database=Nope;Trusted_Connection=true;TrustServerCertificate=true;Connect Timeout=1",
            ["Database:Startup:RetryWindowSeconds"] = "3",
            ["Database:Startup:RetryIntervalSeconds"] = "1"
        });

        var act = () => host.StartAsync();

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("Database.Unreachable");
    }

    [Fact]
    public async Task ConnectionStringDelProviderNoActivo_Ausente_ElHostArranca()
    {
        // FR-004: la cadena del proveedor NO seleccionado nunca es obligatoria.
        using var host = BuildHost(new()
        {
            ["Database:Provider"] = "SqlServer",
            ["Database:ConnectionStrings:SqlServer"] = "Server=localhost;Database=Fake;Trusted_Connection=true"
            // Sin cadena PostgreSQL — no debe importar.
        });

        await host.StartAsync();
        await host.StopAsync();
    }
}
