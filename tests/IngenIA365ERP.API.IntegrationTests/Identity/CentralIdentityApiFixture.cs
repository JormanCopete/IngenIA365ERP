using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Identity;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Fixture para los integration tests del Feature 002 (Identidad Central).
/// A diferencia de <c>ApiTestFixture</c> (Fase 0), esta fixture:
/// <list type="bullet">
///   <item>Configura la BD Admin (<c>ConnectionStrings:SqlServerAdmin</c>)
///         contra el contenedor SQL en una base separada y le crea el schema
///         <c>ADM_*</c> vía <c>AdminDbContext.EnsureCreated</c>.</item>
///   <item>Siembra un master admin (<c>IsGlobalMasterAdmin = true</c>) vía
///         <c>UserManager&lt;CentralUserIdentity&gt;</c> — ejercita el
///         <c>BcryptPasswordHasher</c> real.</item>
///   <item>Reemplaza <see cref="IEmailSender"/> por un capturador en memoria
///         para poder extraer el token de invitación del correo sin SMTP.</item>
/// </list>
/// </summary>
public sealed class CentralIdentityApiFixture : IAsyncLifetime
{
    public const string MasterEmail = "master@integration.test";
    public const string MasterPassword = "Master-Integration-2026!";

    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("IngenIA365_Test2026!")
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
            _sql.StartAsync(),
            _mongo.StartAsync(),
            _redis.StartAsync());

        // BD admin SEPARADA en el mismo contenedor: si compartiera la BD de los
        // contextos de tenant, EnsureCreated vería tablas existentes y saltaría
        // la creación del schema ADM_*.
        var adminConnection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            _sql.GetConnectionString())
        {
            InitialCatalog = "IngenIA365ERP_AdminTest",
        }.ConnectionString;

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _sql.GetConnectionString());
            // TenantConnection es la BD admin real: AdminDbContext y
            // TenantDbContext se registran con ella (Persistence/DependencyInjection).
            builder.UseSetting("ConnectionStrings:TenantConnection", adminConnection);
            builder.UseSetting("ConnectionStrings:SqlServer", _sql.GetConnectionString());
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
            // tests → el archivo no resuelve y tanto el emisor como el
            // validador generan claves aleatorias DISTINTAS → 401 en todo
            // endpoint autenticado. Path absoluto a las claves reales del API.
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

        await EnsureSchemasAsync();
        await SeedMasterAdminAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    private async Task EnsureSchemasAsync()
    {
        using var scope = Factory.Services.CreateScope();

        // TenantDbContext crea la BD admin de test + ADM_Tenants (shape legacy
        // de Fase 0 vía ErpTenantInfo) — igual que un entorno real pre-feature-002.
        var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        await tenantDb.Database.EnsureCreatedAsync();

        // El resto del schema admin sale de los DDL OFICIALES del feature 002
        // (no de EF EnsureCreated, que chocaría con el ADM_Tenants legacy):
        // 15a-15c/15e crean ADM_CentralUsers + memberships + invitations +
        // políticas + reset tokens, y la migración 26b alinea ADM_Tenants y el
        // Id de los reset tokens. Así el test también valida los scripts reales.
        var adminConnection = ResolveAdminConnectionString();
        foreach (var script in new[]
                 {
                     Path.Combine("database", "schema", "15a_Admin_CentralIdentity.sql"),
                     Path.Combine("database", "schema", "15b_Admin_Memberships_Invitations.sql"),
                     Path.Combine("database", "schema", "15c_Admin_MfaPolicy_LoginAttempts.sql"),
                     Path.Combine("database", "schema", "15e_Admin_PasswordResetTokens.sql"),
                     Path.Combine("database", "migration", "26b_Backfill_Gaps_Admin.sql"),
                 })
        {
            await ExecuteSqlScriptAsync(adminConnection, Path.Combine(FindRepoRoot(), script));
        }

        // El EnsureCreated del shape legacy deja IX_ADM_Tenants_Identifier como
        // único NO filtrado: dos tenants con Identifier NULL (el registro del
        // feature 002 no lo llena) chocan. La BD real no tiene ese índice —
        // se re-crea filtrado para permitir NULLs múltiples.
        await ExecuteSqlAsync(adminConnection, @"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ADM_Tenants_Identifier'
           AND object_id = OBJECT_ID('dbo.ADM_Tenants'))
BEGIN
    DROP INDEX IX_ADM_Tenants_Identifier ON dbo.ADM_Tenants;
    CREATE UNIQUE INDEX IX_ADM_Tenants_Identifier
        ON dbo.ADM_Tenants(Identifier) WHERE Identifier IS NOT NULL;
END");

        var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await appDb.Database.EnsureCreatedAsync();
    }

    private string ResolveAdminConnectionString() =>
        new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sql.GetConnectionString())
        {
            InitialCatalog = "IngenIA365ERP_AdminTest",
        }.ConnectionString;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "database")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repo (carpeta database/).");
    }

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteSqlScriptAsync(string connectionString, string scriptPath)
    {
        var sql = await File.ReadAllTextAsync(scriptPath);
        await using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await conn.OpenAsync();

        // Split por batches GO (separador de sqlcmd, no es T-SQL).
        var batches = Regex.Split(sql, @"^\s*GO\s*;?\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = batch;
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync();
        }
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
            _sql.DisposeAsync().AsTask(),
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
