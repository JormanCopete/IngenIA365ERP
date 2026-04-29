using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection(MongoDbSettings.SectionName));

        var mongoSettings = configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>()
            ?? new MongoDbSettings();

        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
        services.AddSingleton<MongoDbInitializer>();

        // Singleton: batching queue shared across requests
        services.AddSingleton<MongoAuditService>();
        services.AddSingleton<IAuditService>(sp => sp.GetRequiredService<MongoAuditService>());

        return services;
    }
}
