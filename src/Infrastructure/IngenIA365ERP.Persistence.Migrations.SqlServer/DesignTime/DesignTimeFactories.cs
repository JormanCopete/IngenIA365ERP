using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.DesignTime;

/// <summary>
/// Factories de diseño para dotnet-ef (feature 004, D-02). Este ensamblado es
/// exclusivo del proveedor SQL Server, por lo que las factories fijan el
/// proveedor sin mirar DB_PROVIDER: seleccionar el proveedor equivale a
/// seleccionar el proyecto (--project) al invocar dotnet-ef.
/// La cadena solo se usa para `database update`/`migrations script`; para
/// `migrations add` no se abre conexión. Override: env var DB_DESIGN_CONNSTR.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        new SqlServerProviderConfigurator().Configure(
            options,
            Environment.GetEnvironmentVariable("DB_DESIGN_CONNSTR")
                ?? "Server=localhost;Database=IngenIA365ERP;Trusted_Connection=true;TrustServerCertificate=true;",
            MigrationsTarget.Application);
        return new ApplicationDbContext(options.Options);
    }
}

public sealed class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>();
        new SqlServerProviderConfigurator().Configure(
            options,
            Environment.GetEnvironmentVariable("DB_DESIGN_CONNSTR")
                ?? "Server=localhost;Database=IngenIA365ERP_Admin;Trusted_Connection=true;TrustServerCertificate=true;",
            MigrationsTarget.Admin);
        return new AdminDbContext(options.Options);
    }
}
