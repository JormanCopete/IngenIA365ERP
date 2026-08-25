using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Indexes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Services;

/// <summary>
/// Crea la base de auditoría de una cooperativa y le garantiza índices y TTL.
///
/// <para>
/// Se llama al aprovisionar, no sólo al arrancar. MongoDB crea la base al
/// escribir el primer documento, sin índices: sin esto, el rastro de una
/// cooperativa nueva se escribiría bien y no se purgaría nunca, y nada lo diría
/// hasta que alguien fuera a comprobar la retención cinco años después.
/// </para>
/// </summary>
internal sealed class MongoAuditStoreProvisioner(
    IMongoClient client,
    IOptions<MongoDbSettings> settings,
    ILogger<MongoAuditStoreProvisioner> logger) : IAuditStoreProvisioner
{
    public async Task<string> AprovisionarAsync(string? tenantId, CancellationToken ct)
    {
        var nombre = AuditDatabaseNames.Para(settings.Value.DatabaseName, tenantId);
        var db = client.GetDatabase(nombre);

        var existentes = await db
            .ListCollectionNames(cancellationToken: ct)
            .ToListAsync(ct);

        if (!existentes.Contains(AuditDatabaseNames.Coleccion))
        {
            // Crearla explicitamente y no esperar al primer documento: es lo que
            // permite dejar el TTL puesto desde el principio.
            await db.CreateCollectionAsync(AuditDatabaseNames.Coleccion, cancellationToken: ct);
        }

        await AuditIndexBootstrap.EnsureIndexesAsync(
            db.GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion), ct);

        logger.LogInformation("Auditoría lista para {Base}: colección e índices garantizados.", nombre);
        return nombre;
    }
}
