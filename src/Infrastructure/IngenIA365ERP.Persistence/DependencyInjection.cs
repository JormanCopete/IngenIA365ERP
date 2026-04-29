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

        // Admin/Tenant management DbContext (lives in IngenIA365ERP_Admin)
        services.AddDbContext<TenantDbContext>(options =>
            options.UseSqlServer(tenantConnectionString));

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

        return services;
    }
}
