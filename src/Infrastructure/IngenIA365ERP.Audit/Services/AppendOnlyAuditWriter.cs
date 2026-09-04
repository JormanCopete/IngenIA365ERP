using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Models;
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
        // La forma del documento vive en AuditDocumentSchema y la comparte con
        // MongoAuditService: los dos escritores producen el mismo documento, y la
        // consola lee una sola forma (mas la heredada, que no se puede reescribir).
        var doc = AuditDocumentSchema.ToDocument(entry);
        return collection.InsertOneAsync(doc, cancellationToken: ct);
    }
}
