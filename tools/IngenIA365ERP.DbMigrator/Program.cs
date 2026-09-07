using IngenIA365ERP.Audit.Bootstrap;
using IngenIA365ERP.Persistence;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Initialization;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;
using IngenIA365ERP.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;
using Serilog.Extensions.Logging;

namespace IngenIA365ERP.DbMigrator;

/// <summary>
/// CLI de operacion de base de datos (feature 004 — T030, contracts/cli.md).
/// Lee la seccion "Database" (appsettings de la API + variables de entorno,
/// con overrides --provider/--connection) y opera sobre el motor activo.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        if (args.Length < 1)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        // audit-bootstrap usa Mongo — atajo antes de armar el stack relacional.
        if (command == "audit-bootstrap")
            return await RunAuditBootstrapAsync(args);

        try
        {
            await using var services = BuildServices(args);

            switch (command)
            {
                case "migrate":
                    return await RunMigrateAsync(services, args);

                case "seed":
                    return await RunSeedAsync(services, args);

                case "script":
                    return await RunScriptAsync(services, args);

                case "create":
                {
                    var tenant = GetArg(args, "--tenant");
                    var name = GetArg(args, "--name");
                    if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(name))
                    {
                        Log.Error("--tenant y --name son obligatorios para 'create'");
                        return 1;
                    }
                    using var scope = services.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<TenantSchemaService>();
                    var info = await service.CreateTenantAsync(tenant, name, GetArg(args, "--plan") ?? "Basic");
                    Log.Information("Tenant creado: {Id} → esquema [{Schema}]", info.Id, info.Schema);
                    return 0;
                }

                case "list":
                {
                    using var scope = services.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<TenantSchemaService>();
                    var tenants = await service.ListTenantsAsync();
                    Log.Information("{Count} tenant(s):", tenants.Count);
                    foreach (var t in tenants)
                        Log.Information("  {Identifier} → [{Schema}] ({Plan}, Active={Active})",
                            t.Identifier, t.Schema, t.PlanType, t.IsActive);
                    return 0;
                }

                case "drop":
                {
                    var tenant = GetArg(args, "--tenant");
                    if (string.IsNullOrEmpty(tenant)) { Log.Error("--tenant es obligatorio para 'drop'"); return 1; }
                    if (!args.Contains("--confirm"))
                    {
                        Log.Warning("Use --confirm para eliminar de verdad. Operación DESTRUCTIVA.");
                        return 1;
                    }
                    using var scope = services.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<TenantSchemaService>();
                    var dropped = await service.DropTenantAsync(tenant);
                    Log.Information(dropped ? "Tenant eliminado." : "Tenant no encontrado.");
                    return 0;
                }

                default:
                    Log.Error("Comando desconocido: {Command}", command);
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Log.Fatal("La operación falló: {Message}", ex.Message);
            return 1;
        }
    }

    // ------------------------------------------------------------------
    // Comandos feature 004
    // ------------------------------------------------------------------

    static async Task<int> RunMigrateAsync(ServiceProvider services, string[] args)
    {
        var scopeArg = (GetArg(args, "--scope") ?? "all").ToLowerInvariant();
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        if (scopeArg is "admin" or "all")
        {
            var adminDb = sp.GetRequiredService<AdminDbContext>();
            await MigrateContextAsync("admin", adminDb);
        }

        if (scopeArg is "tenants" or "all")
        {
            var appDb = sp.GetRequiredService<ApplicationDbContext>();
            await MigrateContextAsync("operativa (dbo)", appDb);

            // Este bucle quedó en el modelo ANTERIOR —un esquema por cooperativa
            // dentro de una base compartida— y el vivo es una base por
            // cooperativa (constitución v2.0.0, Principio IV: el aislamiento es
            // físico). Lo que hace hoy no es «migrar la cooperativa»: es crear
            // sus ~289 tablas como un esquema DENTRO de la base a la que apunte
            // Database__ConnectionStrings, que en un despliegue es la plantilla.
            //
            // El agravante es el valor por defecto: --scope vale "all" si no se
            // pasa, así que un Job que invoque el migrador a secas entra aquí.
            //
            // Se para en vez de hacerlo. Un fallo ruidoso se arregla; una base
            // llena de esquemas basura se descubre semanas después.
            var schemaService = sp.GetRequiredService<TenantSchemaService>();
            var only = GetArg(args, "--tenant");
            var porEsquema = (await schemaService.ListTenantsAsync())
                .Where(t => only is null ||
                            string.Equals(t.Identifier, only, StringComparison.OrdinalIgnoreCase))
                .Where(t => !string.IsNullOrWhiteSpace(t.Schema) &&
                            !t.Schema!.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                .Select(t => $"{t.Identifier} → esquema '{t.Schema}'")
                .ToList();

            if (porEsquema.Count > 0)
            {
                Log.Error(
                    "[Migrator.ModeloDeAislamiento] Hay {Cuenta} cooperativa(s) declaradas con esquema " +
                    "propio, y este comando las migraría como esquemas dentro de la base operativa " +
                    "actual — que es el modelo anterior al Principio IV.\n  {Lista}\n\n" +
                    "Migre cada cooperativa contra SU base, apuntando " +
                    "Database__ConnectionStrings__<Proveedor> a ella y usando --tenant __ninguna__ " +
                    "para neutralizar este bucle. El alcance 'admin' sí es seguro y ya se aplicó.",
                    porEsquema.Count, string.Join("\n  ", porEsquema));

                return 2;
            }
        }

        Log.Information("migrate completado.");
        return 0;
    }

    static async Task MigrateContextAsync(string label, Microsoft.EntityFrameworkCore.DbContext db)
    {
        var pending = (await db.Database.CanConnectAsync()
            ? await db.Database.GetPendingMigrationsAsync()
            : db.Database.GetMigrations()).ToList();
        if (pending.Count == 0)
        {
            Log.Information("BD {Label}: sin migraciones pendientes.", label);
            return;
        }
        Log.Information("BD {Label}: aplicando {Count} migración(es): {List}", label, pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();
    }

    static async Task<int> RunSeedAsync(ServiceProvider services, string[] args)
    {
        var categoryArg = GetArg(args, "--category");
        if (!Enum.TryParse<SeedCategory>(categoryArg, ignoreCase: true, out var category))
        {
            Log.Error("--category es obligatorio: parametric | test");
            return 1;
        }

        var orchestrator = services.GetRequiredService<SeedOrchestrator>();
        var env = services.GetRequiredService<IHostEnvironment>();

        if (category == SeedCategory.Test && env.IsProduction() && !args.Contains("--confirm-test-seed"))
        {
            Log.Error("Seed de pruebas en Production requiere --confirm-test-seed (FR-017).");
            return 1;
        }

        SeedScope? scope = (GetArg(args, "--scope")?.ToLowerInvariant()) switch
        {
            "admin" => SeedScope.Admin,
            "tenant" => SeedScope.Tenant,
            _ => null
        };

        var results = await orchestrator.RunAsync(category, scope, GetArg(args, "--tenant"), CancellationToken.None);
        foreach (var r in results)
            Log.Information("  {Name} [{Scope}]: {Inserted} insertadas ({Tenants} objetivo/s)",
                r.Name, r.Scope, r.Inserted, r.TenantsTouched);
        Log.Information("seed completado ({Count} seeder/s).", results.Count);
        return 0;
    }

    static async Task<int> RunScriptAsync(ServiceProvider services, string[] args)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var providerKey = options.ProviderKey;

        var outputDir = GetArg(args, "--output")
            ?? Path.Combine("database", "schema", "generated", providerKey);
        Directory.CreateDirectory(outputDir);

        var from = GetArg(args, "--from");

        foreach (var (label, db) in new (string, Microsoft.EntityFrameworkCore.DbContext)[]
                 {
                     ("Admin", sp.GetRequiredService<AdminDbContext>()),
                     ("Application", sp.GetRequiredService<ApplicationDbContext>())
                 })
        {
            var migrator = db.Database.GetService<IMigrator>();
            var script = migrator.GenerateScript(
                fromMigration: from, toMigration: null,
                options: MigrationsSqlGenerationOptions.Idempotent);

            var header = $"""
                -- ============================================================
                -- IngenIA365ERP — script idempotente para DBA (feature 004)
                -- Contexto : {label}DbContext · Proveedor: {providerKey}
                -- Generado : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                -- Desde    : {(from ?? "(inicio)")} → (última migración)
                -- Ejecución: re-ejecutable sin efectos (IF NOT EXISTS / historial
                --            __EFMigrationsHistory). Reversión: Down() de cada
                --            migración (DbMigrator/dotnet-ef) o restore de backup.
                -- REQUIERE BACKUP PREVIO en producción (principio XII).
                -- ============================================================

                """;

            var file = Path.Combine(outputDir, $"{label}_idempotent.sql");
            await File.WriteAllTextAsync(file, header + script);
            Log.Information("Script {Label} → {File} ({Size:N0} bytes)", label, file, new FileInfo(file).Length);
        }

        return 0;
    }

    // ------------------------------------------------------------------
    // Infraestructura CLI
    // ------------------------------------------------------------------

    static ServiceProvider BuildServices(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var overrides = new Dictionary<string, string?>();
        if (GetArg(args, "--provider") is { } provider)
            overrides["Database:Provider"] = provider;
        if (GetArg(args, "--connection") is { } conn)
        {
            // El override aplica al proveedor efectivo.
            var key = (GetArg(args, "--provider") ?? "SqlServer").ToLowerInvariant().Contains("post")
                ? "PostgreSQL" : "SqlServer";
            overrides[$"Database:ConnectionStrings:{key}"] = conn;
        }

        var settingsDir = GetArg(args, "--settings-dir") ?? Path.Combine("src", "Presentation", "IngenIA365ERP.API");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(Path.Combine(settingsDir, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(settingsDir, $"appsettings.{environmentName}.json"), optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(overrides)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSerilog());
        services.AddSingleton<IHostEnvironment>(new CliHostEnvironment(environmentName));
        // Dependencias de app que Persistence espera del host (interceptores de auditoria).
        services.AddSingleton<IngenIA365ERP.Application.Common.Interfaces.ICurrentUserService, CliCurrentUserService>();
        services.AddSingleton<IngenIA365ERP.Application.Common.Interfaces.ICurrentTenantService, CliCurrentTenantService>();
        services.AddSingleton<IngenIA365ERP.Application.Common.Interfaces.IAuditService, CliNoOpAuditService>();
        services.AddPersistenceServices(configuration);
        return services.BuildServiceProvider();
    }

    sealed class CliCurrentUserService : IngenIA365ERP.Application.Common.Interfaces.ICurrentUserService
    {
        public int? UserId => null;
        public string? UserName => "system:cli";
        public string? TenantId => null;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => false;
    }

    sealed class CliCurrentTenantService : IngenIA365ERP.Application.Common.Interfaces.ICurrentTenantService
    {
        public string? TenantId => null;
        public string? TenantName => null;
        public string? Schema => null;
        public string? ConnectionString => null;
    }

    /// <summary>
    /// La CLI opera esquema y seeds (que auditan por log/Serilog); la auditoria
    /// Mongo de negocio pertenece al pipeline MediatR de la API — aqui no aplica.
    /// </summary>
    sealed class CliNoOpAuditService : IngenIA365ERP.Application.Common.Interfaces.IAuditService
    {
        public Task LogAsync(string action, string entityType, string entityId, object? oldValues, object? newValues, CancellationToken ct = default) => Task.CompletedTask;
        public Task LogAsync(IngenIA365ERP.Application.Common.Interfaces.AuditLogCommand command, CancellationToken ct = default) => Task.CompletedTask;
        public Task LogAccessAsync(IngenIA365ERP.Application.Common.Interfaces.AccessLogCommand command, CancellationToken ct = default) => Task.CompletedTask;
        public Task FlushAsync() => Task.CompletedTask;
        public Task<IngenIA365ERP.Application.Common.Models.PagedList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>> QueryAsync(IngenIA365ERP.Application.Common.Interfaces.AuditQueryParameters query, CancellationToken ct = default)
            => throw new NotSupportedException("Consultas de auditoría no disponibles desde la CLI.");
        public Task<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>> GetByEntityAsync(string entityType, string entityId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>>([]);
        public Task<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>> GetByUserAsync(string userId, DateTime from, DateTime to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>>([]);
        public Task<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AccessLogEntry>> GetAccessLogsAsync(string? userId, DateTime from, DateTime to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AccessLogEntry>>([]);
        public Task<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>> GetLogsAsync(string entityType, string entityId, int page = 1, int pageSize = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry>>([]);
    }

    sealed class CliHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "IngenIA365ERP.DbMigrator";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    static string? GetArg(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    static void PrintUsage()
    {
        Console.WriteLine("IngenIA365ERP — operación de base de datos (multi-motor, feature 004)");
        Console.WriteLine();
        Console.WriteLine("La configuración sale de la sección Database (appsettings de la API +");
        Console.WriteLine("variables de entorno). Overrides: --provider PostgreSQL|SqlServer,");
        Console.WriteLine("--connection <conn> y --settings-dir <ruta>.");
        Console.WriteLine();
        Console.WriteLine("Migraciones y seeds:");
        Console.WriteLine("  dotnet run -- migrate [--scope admin|tenants|all] [--tenant <id>]");
        Console.WriteLine("  dotnet run -- seed --category parametric|test [--scope admin|tenant|all] [--tenant <id>] [--confirm-test-seed]");
        Console.WriteLine("  dotnet run -- script [--from <migración>] [--output <dir>]");
        Console.WriteLine();
        Console.WriteLine("Tenants:");
        Console.WriteLine("  dotnet run -- create --tenant <id> --name <nombre> [--plan Basic]");
        Console.WriteLine("  dotnet run -- list");
        Console.WriteLine("  dotnet run -- drop --tenant <id> --confirm");
        Console.WriteLine();
        Console.WriteLine("MongoDB audit bootstrap (idempotente):");
        Console.WriteLine("  dotnet run -- audit-bootstrap --mongo-connection <conn> [--file path]");
    }

    static async Task<int> RunAuditBootstrapAsync(string[] args)
    {
        var mongoConn = GetArg(args, "--mongo-connection");
        if (string.IsNullOrEmpty(mongoConn))
        {
            Log.Error("--mongo-connection is required for 'audit-bootstrap'");
            PrintUsage();
            return 1;
        }

        var file = GetArg(args, "--file") ?? "database/migration/15_Audit_Mongodb_Bootstrap.json";
        if (!File.Exists(file))
        {
            Log.Error("Descriptor file not found: {File} (cwd: {Cwd})", file, Directory.GetCurrentDirectory());
            return 1;
        }

        try
        {
            var descriptor = AuditBootstrapRunner.LoadDescriptor(file);
            Log.Information(
                "Descriptor cargado: db='{Db}' version={Version} collections={Cols} roles={Roles} users={Users}",
                descriptor.Database, descriptor.Version,
                descriptor.Collections.Count, descriptor.Roles.Count, descriptor.Users.Count);

            var client = new MongoClient(mongoConn);
            using var loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);

            var runner = new AuditBootstrapRunner(
                client,
                new EnvPasswordResolver(),
                loggerFactory.CreateLogger<AuditBootstrapRunner>());

            var result = await runner.ApplyAsync(descriptor);

            Log.Information("Audit bootstrap completado. Cambios netos: {Total}", result.TotalChanges);
            if (result.CollectionsCreated.Count > 0)
                Log.Information("  + Collections: {List}", string.Join(", ", result.CollectionsCreated));
            if (result.IndexesCreated.Count > 0)
                Log.Information("  + Indexes:     {List}", string.Join(", ", result.IndexesCreated));
            if (result.RolesCreated.Count > 0)
                Log.Information("  + Roles:       {List}", string.Join(", ", result.RolesCreated));
            if (result.UsersCreated.Count > 0)
                Log.Information("  + Users (new): {List}", string.Join(", ", result.UsersCreated));
            if (result.UsersUpdated.Count > 0)
                Log.Information("  ~ Users (upd): {List}", string.Join(", ", result.UsersUpdated));

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "audit-bootstrap failed");
            return 1;
        }
    }
}
