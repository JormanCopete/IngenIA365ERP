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

        // Directorio de credenciales de segundo factor. Es el unico sitio que
        // escribe ADM_MfaCredentials y, con el, TwoFactorEnabled.
        services.AddScoped<
            Application.Common.Interfaces.Identity.IMfaDirectory,
            Identity.MfaDirectory>();

        // Main application DbContext (tenant-scoped)
        // La cadena sale de la cooperativa del ambito, no de la configuracion.
        //
        // Esta lambda se evalua UNA VEZ POR AMBITO —medido, no supuesto— asi que
        // cada peticion construye sus opciones con la conexion de SU cooperativa.
        // Si se evaluara una sola vez por proceso, la cadena quedaria congelada en
        // la primera cooperativa que entrase y todas las demas leerian sus datos.
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            var cooperativa = sp.GetRequiredService<ErpTenantInfo>();
            providerConfigurator.Configure(
                options, cooperativa.ConnectionString!, MigrationsTarget.Application);
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
        services.AddScoped<TenantConnectionResolver>();

        // La fabrica decide contra que base habla la operativa: la de la peticion, la del
        // trabajo de fondo (ContextoAmbiental, feature 012) o, sin ninguna de las dos, la
        // plantilla. Vive en MultiTenancy/CooperativaDelAmbito para poder probarla.
        services.AddScoped(CooperativaDelAmbito.Crear);

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        // Feature 012 (T15, T138): el cerrojo pesimista de la confirmacion, con el SQL de cada motor sobre el propio contexto.
        services.AddScoped<Application.Inventory.Common.ICerrojoDeInventario, Inventory.CerrojoDeInventario>();
        // Feature 009 (FR-011): donde esta parametrizada una cuenta, recorriendo las tablas de siete modulos.
        services.AddScoped<Application.Accounting.Accounts.IAccountReferenceFinder, Services.AccountReferenceFinder>();
        services.AddScoped<TenantSchemaService>();

        // Aprovisionador por BASE. Convive con el de esquema y todavia no lo llama
        // nadie: se registra para poder probarlo contra PostgreSQL real antes de que
        // nada dependa de el.
        services.AddScoped<ITenantDatabaseProvisioner, TenantDatabaseProvisioner>();

        // Para los handlers que escriben en una cooperativa que no es la de la
        // peticion: aceptar una invitacion, aprovisionar un esquema.
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

        // Ranura de Redis por cooperativa: se lee de ADM_Tenants, no se deriva.
        services.AddScoped<ITenantCacheSlots, TenantCacheSlots>();
        services.AddScoped<ITenantCacheSlotAllocator, TenantCacheSlotAllocator>();

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
        // Nomina (feature 005): plan por defecto, conceptos estandar, parametros legales
        // del ano, las cinco clases de riesgo ARL y tipo de comprobante NM. Idempotentes
        // por clave natural.
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.PayrollPlansSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.PayrollConceptDefinitionsSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.PayrollLegalParametersSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.WorkRiskClassesSeeder>();
        // Feature 010: motivos de retiro, festivos de la Ley 51 y políticas por empresa (T017).
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.TerminationReasonsSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.HolidaysSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.CompanyPoliciesSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.BankFileFormatsSeeder>();
        // Contabilidad (feature 009): tipos de comprobante (reemplaza al NM de nomina) y tipos de documento cruce, desde JSON incrustado.
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.AccountCatalogsSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.FinancialStatementItemsSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.VoucherTypesSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.CrossDocumentTypesSeeder>();
        // Feature 012 (T39, T094): los tipos de alerta del catalogo cerrado con sus destinatarios por defecto (Order 83).
        // La tabla COR_AlertTypes llega con PlataformaParaInventario (T186).
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.AlertTypesSeeder>();
        // Feature 012 (T152): un tipo de documento por clase operable con su consecutivo, y la política del saldo inicial
        // (Order 80). No hace nada hasta que la base tenga InventarioComercialNucleo (T440), que crea sus tablas.
        services.AddScoped<Seeding.IDataSeeder, Seeding.Parametric.InventoryDocumentTypesSeeder>();
        services.AddScoped<Seeding.IDataSeeder, Seeding.Demo.DemoDataSeeder>();
        services.AddScoped<Application.Common.Interfaces.Database.IDataSeedRunner, Seeding.DataSeedRunner>();
        // Feature 005: reaplicar la semilla de nomina sobre la cooperativa activa desde la pantalla de conceptos.
        services.AddScoped<Application.Payroll.Concepts.IPayrollSeedApplier, Seeding.PayrollSeedApplier>();
        services.AddScoped<Application.Common.Interfaces.Database.IDatabaseStatusReader, Initialization.DatabaseStatusReader>();
        services.AddHostedService<Initialization.DatabaseInitializerHostedService>();

        return services;
    }
}
