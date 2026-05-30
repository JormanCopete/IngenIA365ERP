using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Interceptors;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        var tenantConnectionString = configuration.GetConnectionString("TenantConnection")
            ?? connectionString;

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, SoftDeleteInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, RowVersionInterceptor>();

        // Admin/Tenant management DbContext (lives in IngenIA365ERP_Admin)
        services.AddDbContext<TenantDbContext>(options =>
            options.UseSqlServer(tenantConnectionString));

        // T072 — AdminDbContext sobre la misma BD (IngenIA365ERP_Admin), pero
        // expuesto a Application como IAdminDbContext para que los handlers
        // de Tenants/Branches no toquen el ApplicationDbContext operacional.
        services.AddDbContext<AdminDbContext>(options =>
            options.UseSqlServer(tenantConnectionString));
        services.AddScoped<IAdminDbContext>(sp => sp.GetRequiredService<AdminDbContext>());

        // Main application DbContext (tenant-scoped)
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<TenantSchemaService>();

        // Directorio de tenants (BD IngenIA365ERP_Admin) accesible desde Application
        // sin acoplar a EF/Persistence.
        services.AddScoped<ITenantDirectory, TenantDirectory>();

        return services;
    }
}
