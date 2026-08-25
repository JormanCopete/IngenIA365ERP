using IngenIA365ERP.Audit.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Indexes;

/// <summary>
/// <see cref="IHostedService"/> que al arranque garantiza los índices
/// y el TTL de 5 años de la colección <c>audit_events</c> (FR-023, SC-007).
/// Es idempotente: <c>CreateMany</c> en MongoDB no falla si el índice ya
/// existe con la misma especificación.
///
/// Nota: los índices se crean sobre la "colección plantilla"
/// <c>audit_events_template</c> y se replican por tenant cuando aparece la
/// primera escritura. Para el bootstrap inicial creamos también los índices
/// sobre cualquier colección <c>audit_events_*</c> ya existente.
/// </summary>
internal sealed class AuditIndexBootstrap(
    IMongoClient client,
    IOptions<MongoDbSettings> settings,
    ILogger<AuditIndexBootstrap> logger) : IHostedService
{
    private static readonly TimeSpan TtlFiveYears = TimeSpan.FromDays(365 * 5);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Recorre las BASES de auditoria, no las colecciones de una base.
            //
            // Antes miraba las colecciones audit_events_* dentro de una sola base.
            // Con una base por cooperativa eso deja de encontrar nada, y el efecto
            // seria mudo: los indices y el TTL de cinco anios no se crearian, la
            // escritura seguiria funcionando, y a los cinco anios el rastro no se
            // habria purgado. FR-023 incumplido sin un solo error por el camino.
            var prefijo = settings.Value.DatabaseName;
            var bases = (await client.ListDatabaseNames(cancellationToken).ToListAsync(cancellationToken))
                .Where(n => n.StartsWith(prefijo + "_", StringComparison.Ordinal))
                .ToList();

            // La global siempre existe conceptualmente aunque no tenga documentos:
            // se garantiza para que su TTL este puesto desde el primer evento.
            var global = AuditDatabaseNames.Para(prefijo, null);
            if (!bases.Contains(global)) bases.Add(global);

            foreach (var nombre in bases)
            {
                var db = client.GetDatabase(nombre);
                var coll = db.GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);
                await EnsureIndexesAsync(coll, cancellationToken);
                logger.LogInformation(
                    "AuditIndexBootstrap: índices y TTL garantizados en {Base}", nombre);
            }
        }
        catch (Exception ex)
        {
            // Que falle el bootstrap no debe tumbar la API; el writer reintentará.
            // El runbook detallará pasos manuales si esto se repite.
            logger.LogError(ex, "AuditIndexBootstrap: falló la inicialización de índices");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static async Task EnsureIndexesAsync(IMongoCollection<BsonDocument> coll, CancellationToken ct)
    {
        var keys = Builders<BsonDocument>.IndexKeys;
        var models = new[]
        {
            new CreateIndexModel<BsonDocument>(
                keys.Ascending("tenantId").Descending("occurredAt"),
                new CreateIndexOptions { Name = "ix_tenant_occurredAt" }),

            new CreateIndexModel<BsonDocument>(
                keys.Ascending("tenantId").Ascending("userId").Descending("occurredAt"),
                new CreateIndexOptions { Name = "ix_tenant_user_occurredAt" }),

            new CreateIndexModel<BsonDocument>(
                keys.Ascending("tenantId").Ascending("entityType").Ascending("entityPublicId").Descending("occurredAt"),
                new CreateIndexOptions { Name = "ix_tenant_entity_occurredAt" }),

            // TTL 5 años (SARLAFT — FR-023, SC-007). MongoDB lo aplica con
            // resolución de minutos sobre el campo `occurredAt`.
            new CreateIndexModel<BsonDocument>(
                keys.Ascending("occurredAt"),
                new CreateIndexOptions
                {
                    Name = "ttl_occurredAt_5y",
                    ExpireAfter = TtlFiveYears
                })
        };

        await coll.Indexes.CreateManyAsync(models, ct);
    }
}
