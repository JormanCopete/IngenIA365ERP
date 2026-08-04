using System.Data.Common;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DotNet.Testcontainers.Containers;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Fixture de integración de Identidad Central, PARAMETRIZADA POR PROVEEDOR
/// desde el feature 004 (T052, D-11): la variable <c>DB_PROVIDER</c>
/// (PostgreSql | SqlServer; default PostgreSql — clarificación #5) decide el
/// contenedor. El esquema YA NO sale de los DDL congelados: lo aprovisiona el
/// propio <c>DatabaseInitializerHostedService</c> de la aplicación con las
/// migraciones EF del proveedor (la fuente única de verdad) + seed paramétrico
/// — con lo cual cada corrida de la suite valida también el arranque real.
/// </summary>
public sealed class CentralIdentityApiFixture : IAsyncLifetime
{
    public const string MasterEmail = "master@integration.test";
    public const string MasterPassword = "Master-Integration-2026!";

    public static readonly string ProviderKey =
        (Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "PostgreSql")
            .Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
        ? "SqlServer"
        : "PostgreSQL";

    private readonly IDatabaseContainer _db = ProviderKey == "SqlServer"
        ? new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("IngenIA365_Test2026!")
            .Build()
        : new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithUsername("ingenia")
            .WithPassword("IngenIA365_Test2026!")
            .WithDatabase("ingenia365erp_test")
            .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:7")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public CapturingEmailSender Emails { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            ((DotNet.Testcontainers.Containers.IContainer)_db).StartAsync(),
            _mongo.StartAsync(),
            _redis.StartAsync());

        var operationalConnection = _db.GetConnectionString();
        var adminConnection = WithDatabaseName(operationalConnection,
            ProviderKey == "SqlServer" ? "IngenIA365ERP_AdminTest" : "ingenia365erp_admin_test");

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Feature 004: la seccion Database gobierna el motor. AutoMigrate
            // aprovisiona el esquema al arrancar el host de test (fuente de
            // verdad = migraciones EF del proveedor).
            builder.UseSetting("Database:Provider", ProviderKey);
            builder.UseSetting($"Database:ConnectionStrings:{ProviderKey}", operationalConnection);
            builder.UseSetting($"Database:AdminConnectionStrings:{ProviderKey}", adminConnection);
            builder.UseSetting("Database:AutoMigrate", "true");
            builder.UseSetting("Database:Seed:RunParametricSeed", "true");
            builder.UseSetting("Database:Seed:RunTestSeed", "false");

            // Claves legacy que otras piezas (health, herramientas Fase 0) aun leen.
            builder.UseSetting("ConnectionStrings:DefaultConnection", operationalConnection);
            builder.UseSetting("ConnectionStrings:TenantConnection", adminConnection);
            builder.UseSetting("ConnectionStrings:SqlServer", operationalConnection);
            builder.UseSetting("ConnectionStrings:SqlServerAdmin", adminConnection);
            builder.UseSetting("MongoDb:ConnectionString", _mongo.GetConnectionString());
            builder.UseSetting("MongoDb:DatabaseName", "IngenIA365ERP_Audit_Test");
            builder.UseSetting("ConnectionStrings:MongoDB", _mongo.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Redis:ConnectionString", _redis.GetConnectionString());
            // Pwned check apagado: los tests no deben depender de un servicio externo.
            builder.UseSetting("PwnedPassword:Enabled", "false");

            // Las claves RS256 se cargan con File.Exists sobre un path RELATIVO
            // ("Keys/dev_private.pem"). Bajo el test host el cwd es el bin de
            // tests → path absoluto a las claves reales del API.
            var apiKeys = Path.Combine(FindRepoRoot(),
                "src", "Presentation", "IngenIA365ERP.API", "Keys");
            builder.UseSetting("JwtSettings:PrivateKeyPath",
                Path.Combine(apiKeys, "dev_private.pem"));
            builder.UseSetting("JwtSettings:PublicKeyPath",
                Path.Combine(apiKeys, "dev_public.pem"));

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails));
            });
        });

        // Forzar arranque del host (dispara el DatabaseInitializerHostedService:
        // espera BD → lock → migra admin + operativa → seed parametrico).
        _ = Factory.Server;

        await SeedMasterAdminAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    private static string WithDatabaseName(string connectionString, string database)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var key = builder.ContainsKey("Database") ? "Database"
                : builder.ContainsKey("Initial Catalog") ? "Initial Catalog" : "Database";
        builder[key] = database;
        return builder.ConnectionString;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IngenIA365ERP.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repo (IngenIA365ERP.slnx).");
    }

    private async Task SeedMasterAdminAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<CentralUserIdentity>>();

        if (await userManager.FindByEmailAsync(MasterEmail) is not null) return;

        var master = new CentralUserIdentity
        {
            Id = Guid.NewGuid(),
            UserName = MasterEmail,
            Email = MasterEmail,
            EmailConfirmed = true,
            IsGlobalMasterAdmin = true,
        };
        var created = await userManager.CreateAsync(master, MasterPassword);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo sembrar el master admin: " +
                string.Join("; ", created.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        await Task.WhenAll(
            ((DotNet.Testcontainers.Containers.IContainer)_db).DisposeAsync().AsTask(),
            _mongo.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask());
    }
}

/// <summary>
/// <see cref="IEmailSender"/> de prueba: captura los mensajes en memoria para
/// que los tests extraigan tokens de invitación / reset sin servidor SMTP.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent
    {
        get { lock (_sent) return _sent.ToList(); }
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        lock (_sent) _sent.Add(message);
        return Task.CompletedTask;
    }
}
