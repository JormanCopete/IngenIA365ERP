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
    // HostBuilder PURO a proposito: CreateDefaultBuilder cargaria los
    // appsettings de la API copiados al bin de tests y rompe la premisa de
    // "configuracion faltante" de estos escenarios.
    private static IHost BuildHost(Dictionary<string, string?> settings, bool withInitializer = false) =>
        new HostBuilder()
            .ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(settings))
            .ConfigureServices((ctx, services) =>
            {
                // Dependencias de app que los interceptores de Persistence
                // esperan (la validacion DI de Development las exige).
                services.AddSingleton(NSubstituteOrStub.CurrentUser());
                services.AddSingleton(NSubstituteOrStub.CurrentTenant());
                services.AddSingleton(NSubstituteOrStub.Audit());
                services.AddPersistenceServices(ctx.Configuration);

                if (!withInitializer)
                {
                    // Estos tests validan SOLO la configuracion (ValidateOnStart);
                    // sin esto el inicializador intentaria migrar contra la
                    // cadena ficticia (p. ej. localhost) y contaminaria BDs.
                    var initializer = services.Single(d =>
                        d.ImplementationType == typeof(IngenIA365ERP.Persistence.Initialization.DatabaseInitializerHostedService));
                    services.Remove(initializer);
                }
            })
            .Build();

    /// <summary>Stubs minimos (el proyecto no referencia NSubstitute).</summary>
    private static class NSubstituteOrStub
    {
        public static IngenIA365ERP.Application.Common.Interfaces.ICurrentUserService CurrentUser() => new StubUser();
        public static IngenIA365ERP.Application.Common.Interfaces.ICurrentTenantService CurrentTenant() => new StubTenant();
        public static IngenIA365ERP.Application.Common.Interfaces.IAuditService Audit() => new StubAudit();

        private sealed class StubUser : IngenIA365ERP.Application.Common.Interfaces.ICurrentUserService
        {
            public int? UserId => null;
            public string? UserName => "system:test";
            public string? TenantId => null;
            public IReadOnlyList<string> Roles => [];
            public bool IsAuthenticated => false;
        }

        private sealed class StubTenant : IngenIA365ERP.Application.Common.Interfaces.ICurrentTenantService
        {
            public string? TenantId => null;
            public string? TenantName => null;
            public string? Schema => null;
            public string? ConnectionString => null;
        }

        private sealed class StubAudit : IngenIA365ERP.Application.Common.Interfaces.IAuditService
        {
            public Task LogAsync(string action, string entityType, string entityId, object? oldValues, object? newValues, CancellationToken ct = default) => Task.CompletedTask;
            public Task LogAsync(IngenIA365ERP.Application.Common.Interfaces.AuditLogCommand command, CancellationToken ct = default) => Task.CompletedTask;
            public Task LogAccessAsync(IngenIA365ERP.Application.Common.Interfaces.AccessLogCommand command, CancellationToken ct = default) => Task.CompletedTask;
            public Task FlushAsync() => Task.CompletedTask;
            public Task<IngenIA365ERP.Application.Common.Models.PagedList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>> QueryAsync(IngenIA365ERP.Application.Common.Interfaces.AuditQueryParameters query, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>> GetByEntityAsync(string entityType, string entityId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>>([]);
            public Task<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>> GetByUserAsync(string userId, DateTime from, DateTime to, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>>([]);
            public Task<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AccessLogEntry>> GetAccessLogsAsync(string? userId, DateTime from, DateTime to, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AccessLogEntry>>([]);
            public Task<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>> GetLogsAsync(string entityType, string entityId, int page = 1, int pageSize = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>>([]);
        }
    }

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
        }, withInitializer: true);

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
