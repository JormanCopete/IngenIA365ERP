using IngenIA365ERP.Audit.Bootstrap;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Serilog;
using Serilog.Extensions.Logging;

namespace IngenIA365ERP.DbMigrator;

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

        var command = args[0].ToLower();

        // audit-bootstrap usa Mongo y NO SQL Server — atajo antes de validar --connection.
        if (command == "audit-bootstrap")
        {
            return await RunAuditBootstrapAsync(args);
        }

        var connString = GetArg(args, "--connection");
        if (string.IsNullOrEmpty(connString))
        {
            Log.Error("--connection is required");
            return 1;
        }

        var dbOptions = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(connString).Options;

        try
        {
            switch (command)
            {
                case "create":
                {
                    var tenant = GetArg(args, "--tenant");
                    var name = GetArg(args, "--name");
                    var plan = GetArg(args, "--plan") ?? "Basic";

                    if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(name))
                    {
                        Log.Error("--tenant and --name are required for 'create'");
                        return 1;
                    }

                    Log.Information("Creating tenant: {Tenant} ({Name})", tenant, name);
                    using var db = new TenantDbContext(dbOptions);
                    await db.Database.EnsureCreatedAsync();
                    var service = new TenantSchemaService(db, connString);
                    var info = await service.CreateTenantAsync(tenant, name, plan);
                    Log.Information("Tenant created: {Id} -> schema [{Schema}]", info.Id, info.Schema);
                    break;
                }

                case "list":
                {
                    using var db = new TenantDbContext(dbOptions);
                    var tenants = await db.Tenants.OrderBy(t => t.Identifier).ToListAsync();
                    Log.Information("Found {Count} tenants:", tenants.Count);
                    foreach (var t in tenants)
                        Log.Information("  {Identifier} -> [{Schema}] ({Plan}, Active={Active})",
                            t.Identifier, t.Schema, t.PlanType, t.IsActive);
                    break;
                }

                case "drop":
                {
                    var tenant = GetArg(args, "--tenant");
                    var confirm = args.Contains("--confirm");

                    if (string.IsNullOrEmpty(tenant))
                    {
                        Log.Error("--tenant is required for 'drop'");
                        return 1;
                    }
                    if (!confirm)
                    {
                        Log.Warning("Use --confirm to actually drop the tenant. This is DESTRUCTIVE.");
                        return 1;
                    }

                    using var db = new TenantDbContext(dbOptions);
                    var service = new TenantSchemaService(db, connString);
                    var dropped = await service.DropTenantAsync(tenant);
                    Log.Information(dropped ? "Tenant dropped." : "Tenant not found.");
                    break;
                }

                default:
                    Log.Error("Unknown command: {Command}", command);
                    PrintUsage();
                    return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Operation failed");
            return 1;
        }
    }

    static string? GetArg(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    static void PrintUsage()
    {
        Console.WriteLine("IngenIA365ERP Database Schema Manager");
        Console.WriteLine();
        Console.WriteLine("Tenant operations (SQL Server):");
        Console.WriteLine("  dotnet run -- create --connection <conn> --tenant <id> --name <name> [--plan Basic]");
        Console.WriteLine("  dotnet run -- list --connection <conn>");
        Console.WriteLine("  dotnet run -- drop --connection <conn> --tenant <id> --confirm");
        Console.WriteLine();
        Console.WriteLine("MongoDB audit bootstrap (idempotente):");
        Console.WriteLine("  dotnet run -- audit-bootstrap --mongo-connection <conn> [--file path]");
        Console.WriteLine("    --mongo-connection: ej. mongodb://admin:****@localhost:27017/?authSource=admin");
        Console.WriteLine("    --file:             default 'database/migration/15_Audit_Mongodb_Bootstrap.json'");
        Console.WriteLine("    Requiere env vars: AUDIT_WRITER_PASSWORD, AUDIT_READER_PASSWORD");
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
