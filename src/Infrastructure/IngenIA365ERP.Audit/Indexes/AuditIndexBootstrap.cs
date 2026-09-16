using IngenIA365ERP.Audit.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Indexes;

/// <summary>
/// <see cref="IHostedService"/> que al arranque garantiza los índices
/// y el TTL de la colección <c>audit_events</c> (FR-023, SC-007; feature 009: por módulo, ver <see cref="AuditRetention"/>).
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
    /// <summary>Índice TTL anterior a la feature 009 (cinco años sobre <c>occurredAt</c>, igual para todo).</summary>
    internal const string IndiceTtlHeredado = "ttl_occurredAt_5y";

    /// <summary>Índice TTL vigente: cada documento trae su vencimiento (<see cref="AuditRetention"/>).</summary>
    internal const string IndiceTtl = "ttl_expiresAt";

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
                await EnsureIndexesAsync(coll, cancellationToken, logger);
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

    internal static async Task EnsureIndexesAsync(IMongoCollection<BsonDocument> coll, CancellationToken ct, ILogger? logger = null)
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
        };

        await coll.Indexes.CreateManyAsync(models, ct);
        await AsegurarRetencionAsync(coll, ct, logger);
    }

    /// <summary>
    /// Feature 009 (FR-052): retención por módulo con un solo índice TTL sobre
    /// <c>expiresAt</c> (<c>expireAfterSeconds = 0</c>: MongoDB purga cuando la fecha del
    /// documento pasa). Tres pasos idempotentes: (1) a los documentos anteriores, que no traen
    /// <c>expiresAt</c>, se les calcula desde <c>occurredAt</c> (o el <c>Timestamp</c>
    /// heredado) con la retención de su módulo; (2) se retira el índice heredado de cinco años
    /// sobre <c>occurredAt</c>, que de quedar purgaría el rastro contable a los cinco;
    /// (3) se crea el índice nuevo. Verificado contra MongoDB 7.0 (el del clúster).
    /// </summary>
    internal static async Task AsegurarRetencionAsync(IMongoCollection<BsonDocument> coll, CancellationToken ct, ILogger? logger = null)
    {
        var sinVencimiento = Builders<BsonDocument>.Filter.Exists("expiresAt", false);
        var diezAnios = new BsonArray(AuditRetention.ModulosDeDiezAnios);
        var ocurridoEl = new BsonDocument("$ifNull", new BsonArray { "$occurredAt", "$Timestamp" });
        var retencion = new BsonDocument("$cond", new BsonArray
        {
            new BsonDocument("$in", new BsonArray { new BsonDocument("$ifNull", new BsonArray { "$module", "$Module" }), diezAnios }),
            (long)AuditRetention.Contable.TotalMilliseconds,
            (long)AuditRetention.Regulatoria.TotalMilliseconds,
        });
        var vencimiento = new BsonDocument("$add", new BsonArray { ocurridoEl, retencion });
        var etapas = new[] { new BsonDocument("$set", new BsonDocument("expiresAt", vencimiento)) };
        try
        {
            await coll.UpdateManyAsync(sinVencimiento, Builders<BsonDocument>.Update.Pipeline(etapas), cancellationToken: ct);
        }
        catch (MongoCommandException ex)
        {
            // La colección es append-only por rol (el descriptor 15_Audit_Mongodb_Bootstrap.json
            // sólo concede insert): si la API entra con ese usuario, el estampado de los
            // documentos anteriores lo hace un administrador con el mismo updateMany. Sin él,
            // esos documentos no vencen nunca (nunca menos retención de la exigida) y el rastro
            // nuevo sí trae su fecha. Se dice, no se calla.
            logger?.LogWarning(ex,
                "AuditIndexBootstrap: no se pudo estampar expiresAt en los documentos anteriores de {Coleccion}; " +
                "ejecutarlo con un usuario con permiso de update (docs/operaciones/contabilidad-primer-ejercicio.md).",
                coll.CollectionNamespace.FullName);
        }

        var existentes = await (await coll.Indexes.ListAsync(ct)).ToListAsync(ct);
        if (existentes.Any(i => i["name"] == IndiceTtlHeredado))
            await coll.Indexes.DropOneAsync(IndiceTtlHeredado, ct);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("expiresAt"),
            new CreateIndexOptions { Name = IndiceTtl, ExpireAfter = TimeSpan.Zero }), cancellationToken: ct);
    }
}
