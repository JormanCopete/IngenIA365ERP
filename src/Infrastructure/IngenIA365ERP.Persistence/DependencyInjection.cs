using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Interceptors;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- Feature 004: seccion "Database" con fail-fast (FR-003) ----
        var section = configuration.GetSection(DatabaseOptions.SectionName);

        services.AddOptions<DatabaseOptions>()
            .Bind(section)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();

        if (!section.Exists())
        {
            // Compatibilidad con hosts previos al feature 004 (fixtures/tools
            // que solo definen ConnectionStrings clasicas): se asume SqlServer.
            services.PostConfigure<DatabaseOptions>(o =>
            {
                o.Provider = "SqlServer";
                o.ConnectionStrings["SqlServer"] =
                    configuration.GetConnectionString("DefaultConnection")
                    ?? configuration.GetConnectionString("SqlServer") ?? string.Empty;
                o.AdminConnectionStrings["SqlServer"] =
                    configuration.GetConnectionString("TenantConnection")
                    ?? configuration.GetConnectionString("SqlServerAdmin") ?? string.Empty;
            });
        }

        // Resolucion en tiempo de registro (las AddDbContext necesitan decidir
        // proveedor ya). La validacion formal ocurre igual via ValidateOnStart.
        var dbOptions = section.Exists()
            ? section.Get<DatabaseOptions>() ?? new DatabaseOptions()
            : new DatabaseOptions
            {
                Provider = "SqlServer",
                ConnectionStrings = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["SqlServer"] = configuration.GetConnectionString("DefaultConnection")
                        ?? configuration.GetConnectionString("SqlServer") ?? string.Empty
                },
                AdminConnectionStrings = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["SqlServer"] = configuration.GetConnectionString("TenantConnection")
                        ?? configuration.GetConnectionString("SqlServerAdmin") ?? string.Empty
                }
            };

        IDbProviderConfigurator providerConfigurator =
            DatabaseProviderParser.TryParse(dbOptions.Provider, out var provider) && provider == DatabaseProvider.PostgreSql
                ? new PostgreSqlProviderConfigurator()
                : new SqlServerProviderConfigurator();
        services.AddSingleton(providerConfigurator);

        var operationalConnectionString = dbOptions.ConnectionStrings.GetValueOrDefault(
            providerConfigurator.Provider == DatabaseProvider.PostgreSql ? "PostgreSQL" : "SqlServer") ?? string.Empty;
        string adminConnectionString;
        try
        {
            adminConnectionString = dbOptions.GetActiveAdminConnectionString();
        }
        catch (InvalidOperationException)
        {
            // La validacion ValidateOnStart reportara el problema real con el
            // codigo Database.* correspondiente; aqui evitamos enmascararla.
            adminConnectionString = string.Empty;
        }

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, SoftDeleteInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, RowVersionInterceptor>();

        // Admin/Tenant management DbContext (BD IngenIA365ERP_Admin). Comparte
        // base con AdminDbContext; su esquema lo gobiernan las migraciones Admin,
        // por eso NUNCA migra (MigrationsTarget.None).
        services.AddDbContext<TenantDbContext>(options =>
            providerConfigurator.Configure(options, adminConnectionString, MigrationsTarget.None));

        // AdminDbContext — expuesto a Application como IAdminDbContext.
        services.AddDbContext<AdminDbContext>(options =>
            providerConfigurator.Configure(options, adminConnectionString, MigrationsTarget.Admin));
        services.AddScoped<IAdminDbContext>(sp => sp.GetRequiredService<AdminDbContext>());

        // Main application DbContext (tenant-scoped)
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            providerConfigurator.Configure(options, operationalConnectionString, MigrationsTarget.Application);
        });

        // ---- El cable del aislamiento entre cooperativas ----
        //
        // ApplicationDbContext ya sabia elegir esquema: OnModelCreating hace
        // `_tenantInfo?.Schema ?? "dbo"`. Lo que faltaba es que alguien pusiera ese
        // ErpTenantInfo en el contenedor. Nadie lo hacia, en todo el repositorio, asi
        // que el operador `??` degradaba a dbo en cada peticion, en silencio: todas
        // las cooperativas compartiendo un unico esquema, sin excepcion ni log.
        //
        // No hace falta tocar la firma del constructor: ApplicationDbContext ya
        // declara (DbContextOptions, ErpTenantInfo? = null, ICurrentUserService? = null)
        // y el contenedor elige el constructor de mas parametros que pueda satisfacer.
        // Se registra el TIPO CONCRETO porque la anotacion de nulabilidad no la honra.
        services.AddScoped(sp =>
        {
            var peticion = sp.GetService<IHttpContextAccessor>()?.HttpContext;

            // Sin peticion HTTP —arranque, trabajos de fondo, la CLI de migraciones—
            // no hay cooperativa de la que tirar, y dbo es la respuesta correcta:
            // ahi viven el arbol de migraciones y las plantillas. Se comprueba el
            // HttpContext y no el esquema porque por esa via son indistinguibles.
            if (peticion is null)
            {
                return new ErpTenantInfo { SchemaName = "dbo" };
            }

            var actual = sp.GetRequiredService<ICurrentTenantService>();
            var esquema = actual.Schema;

            // Dentro de una peticion, un esquema sin resolver NO puede caer a dbo.
            // No hay segunda barrera que lo recoja: ninguna entidad implementa
            // ITenantEntity y los 285 filtros globales son todos de borrado logico.
            // Si el esquema sale mal, nada lo detiene y los datos se mezclan. Fallar
            // ruidosamente es la unica opcion segura.
            if (string.IsNullOrWhiteSpace(esquema))
            {
                throw new InvalidOperationException(
                    "Se pidio la base operativa dentro de una peticion sin cooperativa resuelta " +
                    $"({peticion.Request.Method} {peticion.Request.Path}). Caer a 'dbo' mezclaria " +
                    "los datos de todas las cooperativas. Si esta ruta debe funcionar sin " +
                    "cooperativa, no tiene que usar IApplicationDbContext.");
            }

            return new ErpTenantInfo
            {
                SchemaName = esquema,
                Name = actual.TenantName,
                Id = actual.TenantId,
            };
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<TenantSchemaService>();

        // Para los handlers que escriben en una cooperativa que no es la de la
        // peticion: aceptar una invitacion, aprovisionar un esquema.
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

        // Directorio de tenants (BD IngenIA365ERP_Admin) accesible desde Application
        // sin acoplar a EF/Persistence.
        services.AddScoped<ITenantDirectory, TenantDirectory>();

        // ---- Feature 004: inicializador de BD + seeding (US2/US3) ----
        services.AddSingleton<Initialization.DatabaseReadiness>();
        services.AddScoped<Initialization.PendingMigrationsGuard>();
        services.AddSingleton<Seeding.SeedOrchestrator>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.SystemParametersSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.CurrenciesSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.DocumentTypesSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.ChartOfAccountsSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Demo.DemoDataSeeder>();
        services.AddScoped<Application.Common.Interfaces.Database.IDataSeedRunner, Seeding.DataSeedRunner>();
        services.AddScoped<Application.Common.Interfaces.Database.IDatabaseStatusReader, Initialization.DatabaseStatusReader>();
        services.AddHostedService<Initialization.DatabaseInitializerHostedService>();

        return services;
    }
}
