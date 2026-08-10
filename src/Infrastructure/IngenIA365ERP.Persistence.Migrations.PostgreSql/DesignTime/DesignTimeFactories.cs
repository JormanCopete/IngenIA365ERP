using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.DesignTime;

/// <summary>
/// Factories de diseño para dotnet-ef (feature 004, D-02). Este ensamblado es
/// exclusivo del proveedor PostgreSQL — ver la nota en el homólogo SqlServer.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        new PostgreSqlProviderConfigurator().Configure(
            options,
            Environment.GetEnvironmentVariable("DB_DESIGN_CONNSTR")
                ?? "Host=localhost;Port=5433;Database=ingenia365erp;Username=ingenia;Password=IngenIA365_Dev2026!",
            MigrationsTarget.Application);
        return new ApplicationDbContext(options.Options);
    }
}

public sealed class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>();
        new PostgreSqlProviderConfigurator().Configure(
            options,
            Environment.GetEnvironmentVariable("DB_DESIGN_CONNSTR")
                ?? "Host=localhost;Port=5433;Database=ingenia365erp_admin;Username=ingenia;Password=IngenIA365_Dev2026!",
            MigrationsTarget.Admin);
        return new AdminDbContext(options.Options);
    }
}
