using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Infrastructure;

/// <summary>
/// Fixture base para los integration tests de la Fase 0 y posteriores.
/// Levanta SQL Server, MongoDB y Redis en contenedores efímeros con
/// Testcontainers y configura el <see cref="WebApplicationFactory{TEntryPoint}"/>
/// con esas connection strings. Tras el arranque, siembra una cooperativa
/// demo (<c>demo</c>) con su sucursal matriz <c>MAT</c>.
///
/// Uso desde un test:
/// <code>
/// public class MyTests(ApiTestFixture fx) : IClassFixture&lt;ApiTestFixture&gt;
/// {
///     [Fact] public async Task X() {
///         using var http = fx.CreateAuthenticatedClient();
///         …
///     }
/// }
/// </code>
///
/// Notas:
///  * Los contenedores requieren Docker en el host del runner. Si no está
///    disponible, los tests deben marcarse con <c>Skip</c> o gated por
///    una variable de entorno; el fixture mismo lanza al arranque.
///  * <see cref="EnsureCreatedAsync"/> usa <c>EnsureCreated</c> de EF —
///    suficiente para Phase 2. Cuando aterricen las migraciones EF reales,
///    se cambia a <c>MigrateAsync</c>.
/// </summary>
public sealed class ApiTestFixture : IAsyncLifetime
{
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

    public const string DemoTenantIdentifier = "demo";
    public const string DemoBranchCode = "MAT";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _sql.StartAsync(),
            _mongo.StartAsync(),
            _redis.StartAsync());

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _sql.GetConnectionString());
            builder.UseSetting("ConnectionStrings:TenantConnection", _sql.GetConnectionString());
            builder.UseSetting("MongoDb:ConnectionString", _mongo.GetConnectionString());
            builder.UseSetting("MongoDb:DatabaseName", "IngenIA365ERP_Audit_Test");
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Redis:ConnectionString", _redis.GetConnectionString());
            builder.UseSetting("Smtp:Host", "localhost");
            builder.UseSetting("Smtp:Port", "25");
        });

        await EnsureSchemasAsync();
        await SeedDemoTenantAndBranchAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    private async Task EnsureSchemasAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        await tenantDb.Database.EnsureCreatedAsync();

        var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await appDb.Database.EnsureCreatedAsync();
    }

    private async Task SeedDemoTenantAndBranchAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var existing = await tenantDb.Tenants.FirstOrDefaultAsync(t => t.Identifier == DemoTenantIdentifier);
        ErpTenantInfo demo;
        if (existing is null)
        {
            demo = new ErpTenantInfo
            {
                PublicId = Guid.NewGuid(),
                Identifier = DemoTenantIdentifier,
                Name = "Cooperativa Demo",
                ConnectionString = _sql.GetConnectionString(),
                SchemaName = "dbo",
                LicenseType = "Test",
                IsActive = true,
                MaxUsers = 100,
                CreatedAt = DateTime.UtcNow
            };
            tenantDb.Tenants.Add(demo);
            await tenantDb.SaveChangesAsync();
        }
        else
        {
            demo = existing;
        }

        var appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await appDb.Tenants.FirstOrDefaultAsync(t => t.SchemaName == "dbo");
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "Cooperativa Demo",
                SchemaName = "dbo",
                Subdomain = DemoTenantIdentifier,
                PlanType = "Test",
                IsActive = true,
                ContactEmail = "demo@ingenia365.test",
                Nit = "900000001-1",
                LegalName = "Cooperativa Demo S.A.S.",
                LegalAddress = "Calle 1 #1-1, Bogotá, Colombia",
                TaxRegime = "Común"
            };
            appDb.Tenants.Add(tenant);
            await appDb.SaveChangesAsync();
        }

        var branch = await appDb.TenantBranches.FirstOrDefaultAsync(b => b.TenantId == tenant.Id && b.Code == DemoBranchCode);
        if (branch is null)
        {
            appDb.TenantBranches.Add(new TenantBranch
            {
                TenantId = tenant.Id,
                Code = DemoBranchCode,
                Name = "Sede Principal",
                IsHeadquarters = true,
                IsActive = true,
                Address = "Calle 1 #1-1, Bogotá"
            });
            await appDb.SaveChangesAsync();
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
