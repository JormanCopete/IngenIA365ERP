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

        // Crea la base de auditoria de una cooperativa con sus indices y TTL.
        services.AddSingleton<IAuditStoreProvisioner, MongoAuditStoreProvisioner>();

        // T088 — Exporter CSV (CsvHelper). Scoped: el writer interno usa
        // streams por request; no compartirlos entre requests concurrentes.
        services.AddScoped<IAuditCsvExporter, AuditCsvExporter>();

        // T089 — Firmador HMAC y exporter PDF (QuestPDF). Singleton: las
        // claves se cargan de config una sola vez; el exporter es stateless.
        services.AddSingleton<IAuditSignatureService, AuditSignatureService>();
        services.AddScoped<IAuditPdfExporter, AuditPdfExporter>();

        // Feature 012 (T38; T064–T066): el sello de integridad de la cadena de auditoría, el sellado y
        // reenvío por cooperativa, y el lector de la verificación. El servicio de fondo que los conduce
        // (AuditOutboxForwarder) lo registra SOLO la API, condicionado a Integration:AuditForwarder:Enabled.
        services.AddSingleton<IngenIA365ERP.Audit.Integrity.SelloDeIntegridad>();
        services.AddSingleton<SelladoDeAuditoria>();
        services.AddSingleton<IngenIA365ERP.Application.Audit.VerifyIntegrity.ILectorDeCadenaDeAuditoria,
            IngenIA365ERP.Audit.Integrity.LectorDeCadenaDeAuditoria>();

        // T026 — Bootstrap idempotente de índices y TTL al arranque.
        services.AddHostedService<AuditIndexBootstrap>();

        return services;
    }
}
