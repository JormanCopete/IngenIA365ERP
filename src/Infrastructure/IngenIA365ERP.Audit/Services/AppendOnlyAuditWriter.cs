using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Audit.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Services;

/// <summary>
/// Implementación append-only sobre <c>audit_events</c>. El refuerzo a
/// nivel de base de datos vive en el rol <c>appendOnly</c> aprovisionado
/// por <c>database/migration/15_Audit_Mongodb_Bootstrap.json</c> — incluso
/// si alguien adquiere otro <see cref="IMongoCollection{TDocument}"/> y
/// llama <c>UpdateOne</c>/<c>DeleteOne</c>, MongoDB rechaza la operación.
///
/// El rastro de cada cooperativa vive en <b>su propia base</b>, y dentro en una
/// única colección. La base se resuelve por llamada y no en el constructor: este
/// servicio es singleton, así que cachearla la habría clavado en la primera
/// cooperativa que escribiera y el resto habría acabado escribiendo ahí.
/// </summary>
internal sealed class AppendOnlyAuditWriter(
    IMongoClient client,
    IOptions<MongoDbSettings> settings) : IAuditAppendOnlyWriter
{

    public Task AppendAsync(AuditEventDocument entry, CancellationToken ct)
    {
        var db = client.GetDatabase(
            AuditDatabaseNames.Para(settings.Value.DatabaseName, entry.TenantId));
        var collection = db.GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);
        var doc = new BsonDocument
        {
            { "tenantId", entry.TenantId },
            { "userId", entry.UserId },
            { "userName", entry.UserName ?? BsonNull.Value.AsString },
            { "action", entry.Action },
            { "entityType", entry.EntityType },
            { "entityPublicId", entry.EntityPublicId ?? string.Empty },
            { "module", entry.Module ?? string.Empty },
            { "oldValuesJson", entry.OldValuesJson ?? string.Empty },
            { "newValuesJson", entry.NewValuesJson ?? string.Empty },
            { "changedFields", new BsonArray(entry.ChangedFields ?? []) },
            { "ipAddress", entry.IpAddress ?? string.Empty },
            { "userAgent", entry.UserAgent ?? string.Empty },
            { "endpoint", entry.Endpoint ?? string.Empty },
            { "httpMethod", entry.HttpMethod ?? string.Empty },
            { "httpStatusCode", entry.HttpStatusCode ?? 0 },
            { "durationMs", entry.DurationMs ?? 0L },
            { "occurredAt", entry.OccurredAt }
        };
        return collection.InsertOneAsync(doc, cancellationToken: ct);
    }
}
