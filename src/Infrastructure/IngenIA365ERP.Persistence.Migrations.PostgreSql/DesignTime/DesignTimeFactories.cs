using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.DesignTime;

/// <summary>
/// Factories de diseño para dotnet-ef (feature 004, D-02). Este ensamblado es
/// exclusivo del proveedor PostgreSQL — ver la nota en el homólogo SqlServer.
///
/// <para>
/// La cadena por defecto <b>no lleva contraseña</b> (2026-09-23): alcanza para
/// <c>migrations add</c>, que no abre conexión. Para <c>database update</c> o
/// <c>migrations script</c> contra una base, la cadena entera va en la variable
/// de entorno <c>DB_DESIGN_CONNSTR</c>. Hasta esa fecha el valor por defecto traía
/// la contraseña del contenedor de desarrollo escrita en el código; lo impide
/// <c>NoHayContrasenasEnElCodigo</c>.
/// </para>
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        new PostgreSqlProviderConfigurator().Configure(
            options,
            Environment.GetEnvironmentVariable("DB_DESIGN_CONNSTR")
                ?? "Host=localhost;Port=5433;Database=ingenia365erp;Username=ingenia",
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
                ?? "Host=localhost;Port=5433;Database=ingenia365erp_admin;Username=ingenia",
            MigrationsTarget.Admin);
        return new AdminDbContext(options.Options);
    }
}
