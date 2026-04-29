using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IngenIA365ERP.DbMigrator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        if (args.Length < 2)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLower();
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
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- create --connection <conn> --tenant <id> --name <name> [--plan Basic]");
        Console.WriteLine("  dotnet run -- list --connection <conn>");
        Console.WriteLine("  dotnet run -- drop --connection <conn> --tenant <id> --confirm");
    }
}
