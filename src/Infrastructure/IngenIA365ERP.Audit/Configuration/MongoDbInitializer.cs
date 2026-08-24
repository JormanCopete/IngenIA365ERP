using IngenIA365ERP.Audit.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Configuration;

public class MongoDbInitializer
{
    private readonly IMongoClient _client;
    private readonly MongoDbSettings _settings;
    private readonly ILogger<MongoDbInitializer> _logger;

    public MongoDbInitializer(
        IMongoClient client,
        IOptions<MongoDbSettings> settings,
        ILogger<MongoDbInitializer> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(string tenantId)
    {
        var db = _client.GetDatabase(_settings.DatabaseName);

        await InitializeAuditCollectionAsync(db, tenantId);
        await InitializeAccessCollectionAsync(db, tenantId);

        _logger.LogInformation("MongoDB collections initialized for tenant {TenantId}", tenantId);
    }

    public async Task InitializeDefaultAsync()
    {
        await InitializeAsync("default");
    }

    private async Task InitializeAuditCollectionAsync(IMongoDatabase db, string tenantId)
    {
        var collectionName = Services.MongoAuditService.NombreDeColeccion(tenantId);
        var collection = db.GetCollection<AuditLog>(collectionName);

        var indexes = new List<CreateIndexModel<AuditLog>>
        {
            // Query by date (most common)
            new(Builders<AuditLog>.IndexKeys.Descending(a => a.Timestamp),
                new CreateIndexOptions { Name = "idx_timestamp" }),

            // Query by entity
            new(Builders<AuditLog>.IndexKeys
                    .Ascending(a => a.EntityType)
                    .Ascending(a => a.EntityId),
                new CreateIndexOptions { Name = "idx_entity" }),

            // Query by user
            new(Builders<AuditLog>.IndexKeys
                    .Ascending(a => a.UserId)
                    .Descending(a => a.Timestamp),
                new CreateIndexOptions { Name = "idx_user_timestamp" }),

            // Query by module
            new(Builders<AuditLog>.IndexKeys
                    .Ascending(a => a.Module)
                    .Descending(a => a.Timestamp),
                new CreateIndexOptions { Name = "idx_module_timestamp" }),

            // TTL for automatic expiration
            new(Builders<AuditLog>.IndexKeys.Ascending(a => a.Timestamp),
                new CreateIndexOptions
                {
                    Name = "ttl_timestamp",
                    ExpireAfter = TimeSpan.FromDays(_settings.RetentionDays)
                })
        };

        await collection.Indexes.CreateManyAsync(indexes);
    }

    private async Task InitializeAccessCollectionAsync(IMongoDatabase db, string tenantId)
    {
        var collectionName = $"access_{tenantId}";
        var collection = db.GetCollection<AccessLog>(collectionName);

        var indexes = new List<CreateIndexModel<AccessLog>>
        {
            // Query by date
            new(Builders<AccessLog>.IndexKeys.Descending(a => a.Timestamp),
                new CreateIndexOptions { Name = "idx_timestamp" }),

            // Query by user
            new(Builders<AccessLog>.IndexKeys
                    .Ascending(a => a.UserId)
                    .Descending(a => a.Timestamp),
                new CreateIndexOptions { Name = "idx_user_timestamp" }),

            // TTL (90 days default for access logs)
            new(Builders<AccessLog>.IndexKeys.Ascending(a => a.Timestamp),
                new CreateIndexOptions
                {
                    Name = "ttl_timestamp",
                    ExpireAfter = TimeSpan.FromDays(_settings.AccessLogRetentionDays)
                })
        };

        await collection.Indexes.CreateManyAsync(indexes);
    }
}
