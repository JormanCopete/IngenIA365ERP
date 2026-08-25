using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Initialization;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Persistence.Seeding;

public sealed record SeederRunResult(string Name, SeedScope Scope, int TenantsTouched, int Inserted);

/// <summary>
/// Orquestador de seeds (feature 004 — T036, data-model §2). Filtra por
/// categoria/flags/ambiente, ordena por Order y ejecuta con transaccion por
/// seeder×alcance. Alcance Tenant: itera el esquema default ("dbo") + cada
/// tenant registrado, abriendo cada esquema por separado (principio IV).
/// Se rehusa a correr sobre esquemas con migraciones pendientes (FR-021).
/// </summary>
public sealed class SeedOrchestrator(
    IServiceProvider serviceProvider,
    IOptions<DatabaseOptions> options,
    IHostEnvironment environment,
    ILogger<SeedOrchestrator> logger)
{
    /// <summary>Seeding de arranque segun flags (lo invoca el inicializador tras migrar).</summary>
    public async Task RunStartupAsync(CancellationToken ct)
    {
        var seed = options.Value.Seed;

        if (seed.RunParametricSeed)
            await RunAsync(SeedCategory.Parametric, scope: null, tenantIdentifier: null, ct);
        else
            logger.LogInformation("Seed paramétrico omitido (Database:Seed:RunParametricSeed=false).");

        if (EffectiveRunTestSeed())
        {
            if (environment.IsProduction())
            {
                // FR-017: la activacion explicita de datos demo en Production
                // queda en el log Y en la auditoria Mongo (TTL 5 anos).
                logger.LogWarning(
                    "Seed de PRUEBAS habilitado explícitamente en Production (Database:Seed:RunTestSeed=true).");
                using var auditScope = serviceProvider.CreateScope();
                var audit = auditScope.ServiceProvider
                    .GetService<IngenIA365ERP.Application.Common.Interfaces.IAuditService>();
                if (audit is not null)
                    await audit.LogAsync(
                        IngenIA365ERP.Application.Common.Audit.AuditEventTypes.DatabaseSeedTestSeedEnabledInProduction,
                        entityType: "Database", entityId: "Seed",
                        oldValues: null,
                        newValues: new { RunTestSeed = true, Environment = environment.EnvironmentName },
                        ct);
            }
            await RunAsync(SeedCategory.Test, scope: null, tenantIdentifier: null, ct);
        }
        else
        {
            logger.LogInformation("Seed de pruebas omitido por política de ambiente ({Env}).", environment.EnvironmentName);
        }
    }

    /// <summary>
    /// Siembra UNA cooperativa recién aprovisionada, aplicando la misma política de
    /// categorías que el arranque.
    ///
    /// <para>
    /// Existe para que esa política viva en un solo sitio. Estaba en dos: el
    /// arranque sembraba paramétricos y de demostración, y el aprovisionador sólo
    /// paramétricos. La consecuencia medida es que el contenido de una cooperativa
    /// dependía de si el servicio se había reiniciado después de crearla — dos
    /// cooperativas creadas el mismo día con el mismo código acababan distintas.
    /// Esa clase de asimetría hace que un fallo aparezca en un entorno y no en
    /// otro.
    /// </para>
    /// </summary>
    public async Task<int> SembrarCooperativaAsync(string identificador, CancellationToken ct)
    {
        var total = 0;

        if (options.Value.Seed.RunParametricSeed)
        {
            var r = await RunAsync(SeedCategory.Parametric, SeedScope.Tenant, identificador, ct);
            total += r.Sum(x => x.Inserted);
        }
        else
        {
            logger.LogInformation(
                "Seed paramétrico omitido para {Identificador} (Database:Seed:RunParametricSeed=false).",
                identificador);
        }

        if (EffectiveRunTestSeed())
        {
            var r = await RunAsync(SeedCategory.Test, SeedScope.Tenant, identificador, ct);
            total += r.Sum(x => x.Inserted);
        }

        return total;
    }

    /// <summary>Default por ambiente: on en Development/QA, off en Production salvo opt-in (FR-017).</summary>
    public bool EffectiveRunTestSeed() =>
        options.Value.Seed.RunTestSeed
        ?? (environment.IsDevelopment() || environment.IsEnvironment("QA"));

    /// <summary>
    /// Ejecuta los seeders de una categoria. <paramref name="scope"/> null = ambos
    /// alcances; <paramref name="tenantIdentifier"/> limita a un tenant concreto.
    /// </summary>
    public async Task<IReadOnlyList<SeederRunResult>> RunAsync(
        SeedCategory category, SeedScope? scope, string? tenantIdentifier, CancellationToken ct)
    {
        using var outerScope = serviceProvider.CreateScope();
        var sp = outerScope.ServiceProvider;

        // FR-021 — nunca sembrar sobre esquema desactualizado.
        var guard = sp.GetRequiredService<PendingMigrationsGuard>();
        var pending = await guard.ComputeAsync(ct);
        if (pending.HasAny)
            throw new InvalidOperationException(
                $"[Database.Seed.SchemaOutdated] Hay migraciones pendientes — aplique las migraciones antes de sembrar: {pending.Describe()}");

        var seeders = sp.GetServices<IDataSeeder>()
            .Where(s => s.Category == category)
            .Where(s => scope is null || s.Scope == scope)
            .OrderBy(s => s.Order)
            .ToList();

        var results = new List<SeederRunResult>();
        foreach (var seeder in seeders)
        {
            var name = seeder.GetType().Name;
            var inserted = 0;
            var tenantsTouched = 0;

            if (seeder.Scope == SeedScope.Admin)
            {
                inserted = await RunAdminSeederAsync(sp, seeder, ct);
                tenantsTouched = 0;
            }
            else
            {
                foreach (var tenant in await ResolveTenantTargetsAsync(sp, tenantIdentifier))
                {
                    inserted += await RunTenantSeederAsync(sp, seeder, tenant, ct);
                    tenantsTouched++;
                }
            }

            logger.LogInformation("Seeder {Seeder} ({Category}/{Scope}): {Inserted} fila(s) insertadas en {Tenants} objetivo(s).",
                name, seeder.Category, seeder.Scope, inserted, Math.Max(tenantsTouched, 1));
            results.Add(new SeederRunResult(name, seeder.Scope, tenantsTouched, inserted));
        }

        return results;
    }

    private async Task<int> RunAdminSeederAsync(IServiceProvider sp, IDataSeeder seeder, CancellationToken ct)
    {
        var admin = sp.GetRequiredService<AdminDbContext>();
        var context = NewContext(admin: admin, tenantDb: null, tenant: null);

        var strategy = admin.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await admin.Database.BeginTransactionAsync(ct);
            var inserted = await seeder.SeedAsync(context, ct);
            await tx.CommitAsync(ct);
            return inserted;
        });
    }

    private async Task<int> RunTenantSeederAsync(
        IServiceProvider sp, IDataSeeder seeder, ErpTenantInfo? tenant, CancellationToken ct)
    {
        // Un ApplicationDbContext NUEVO por cooperativa, con SU conexion — jamas se
        // comparte entre cooperativas dentro de una misma operacion (Principio IV).
        //
        // Las opciones NO se toman del contenedor. Ahi la cadena sale de la
        // cooperativa del ambito, y este orquestador corre sin peticion detras: le
        // llegaria siempre la plantilla, asi que sembraria N veces la misma base
        // creyendo que siembra N cooperativas.
        var configurador = sp.GetRequiredService<Providers.IDbProviderConfigurator>();
        var resolutor = sp.GetRequiredService<MultiTenancy.TenantConnectionResolver>();

        var cadena = tenant is null
            ? resolutor.Plantilla
            : resolutor.Resolver(tenant.SchemaName, tenant.ConnectionString, tenant.Name);

        var constructor = new DbContextOptionsBuilder<ApplicationDbContext>();
        // Los interceptores se registran como ISaveChangesInterceptor. Pedir
        // IInterceptor compila —por covarianza— y devuelve CERO: contextos
        // sembrando sin auditoria ni borrado logico, sin una sola senal.
        constructor.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
        configurador.Configure(constructor, cadena, Providers.MigrationsTarget.Application);

        await using var tenantDb = new ApplicationDbContext(
            constructor.Options,
            tenant is null ? null : new MultiTenancy.ErpTenantInfo
            {
                Identifier = tenant.Identifier,
                Name = tenant.Name,
                SchemaName = "dbo",
                ConnectionString = cadena,
            },
            currentUserService: null);
        var context = NewContext(admin: null, tenantDb: tenantDb, tenant: tenant);

        var strategy = tenantDb.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await tenantDb.Database.BeginTransactionAsync(ct);
            var inserted = await seeder.SeedAsync(context, ct);
            await tx.CommitAsync(ct);
            return inserted;
        });
    }

    /// <summary>Esquema default ("dbo", tenant=null) + tenants registrados, o uno concreto.</summary>
    private static async Task<List<ErpTenantInfo?>> ResolveTenantTargetsAsync(
        IServiceProvider sp, string? tenantIdentifier)
    {
        var schemaService = sp.GetRequiredService<TenantSchemaService>();
        var registered = await schemaService.ListTenantsAsync();

        if (tenantIdentifier is not null)
        {
            var match = registered.FirstOrDefault(t =>
                string.Equals(t.Identifier, tenantIdentifier, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"[Database.Seed.TenantNotFound] No existe un tenant activo con identificador '{tenantIdentifier}'.");
            return [match];
        }

        var targets = new List<ErpTenantInfo?> { null }; // esquema default dbo
        targets.AddRange(registered.Where(t =>
            !string.IsNullOrWhiteSpace(t.Schema) &&
            !t.Schema!.Equals("dbo", StringComparison.OrdinalIgnoreCase)));
        return targets;
    }

    private SeedContext NewContext(AdminDbContext? admin, ApplicationDbContext? tenantDb, ErpTenantInfo? tenant) => new()
    {
        Admin = admin,
        TenantDb = tenantDb,
        Tenant = tenant,
        EnvironmentName = environment.EnvironmentName,
        Logger = logger
    };
}
