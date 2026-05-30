using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Application.Audit.ExportAuditLogCsv;
using IngenIA365ERP.Application.Audit.ExportAuditLogPdf;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Indexes;
using IngenIA365ERP.Audit.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection(MongoDbSettings.SectionName));
        services.Configure<AuditSignatureSettings>(configuration.GetSection(AuditSignatureSettings.SectionName));

        var mongoSettings = configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>()
            ?? new MongoDbSettings();

        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
        services.AddSingleton<MongoDbInitializer>();

        // Singleton: batching queue shared across requests
        services.AddSingleton<MongoAuditService>();
        services.AddSingleton<IAuditService>(sp => sp.GetRequiredService<MongoAuditService>());

        // T028 — Writer append-only sobre audit_events_{tenantId}.
        services.AddSingleton<IAuditAppendOnlyWriter, AppendOnlyAuditWriter>();

        // T088 — Exporter CSV (CsvHelper). Scoped: el writer interno usa
        // streams por request; no compartirlos entre requests concurrentes.
        services.AddScoped<IAuditCsvExporter, AuditCsvExporter>();

        // T089 — Firmador HMAC y exporter PDF (QuestPDF). Singleton: las
        // claves se cargan de config una sola vez; el exporter es stateless.
        services.AddSingleton<IAuditSignatureService, AuditSignatureService>();
        services.AddScoped<IAuditPdfExporter, AuditPdfExporter>();

        // T026 — Bootstrap idempotente de índices y TTL al arranque.
        services.AddHostedService<AuditIndexBootstrap>();

        return services;
    }
}
