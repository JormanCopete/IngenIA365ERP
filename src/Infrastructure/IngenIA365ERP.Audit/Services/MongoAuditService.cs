using System.Collections.Concurrent;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Services;

public class MongoAuditService : IAuditService, IDisposable
{
    private readonly IMongoClient _client;
    private readonly MongoDbSettings _settings;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ILogger<MongoAuditService> _logger;

    private readonly ConcurrentQueue<AuditLog> _auditQueue = new();
    private readonly ConcurrentQueue<AccessLog> _accessQueue = new();
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private bool _disposed;

    public MongoAuditService(
        IMongoClient client,
        IOptions<MongoDbSettings> settings,
        ICurrentUserService currentUserService,
        ICurrentTenantService tenantService,
        ILogger<MongoAuditService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _currentUserService = currentUserService;
        _tenantService = tenantService;
        _logger = logger;

        _flushTimer = new Timer(
            _ => _ = FlushInternalAsync(),
            null,
            TimeSpan.FromSeconds(_settings.FlushIntervalSeconds),
            TimeSpan.FromSeconds(_settings.FlushIntervalSeconds));
    }

    // === WRITE: Legacy overload ===

    public Task LogAsync(string action, string entityType, string entityId,
        object? oldValues, object? newValues, CancellationToken cancellationToken = default)
    {
        return LogAsync(new AuditLogCommand
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
        }, cancellationToken);
    }

    // === WRITE: Full audit log ===

    public Task LogAsync(AuditLogCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.TenantId ?? "default";

        var entry = new AuditLog
        {
            TenantId = tenantId,
            UserId = _currentUserService.UserId?.ToString() ?? "system",
            UserName = _currentUserService.UserName ?? "system",
            Action = command.Action,
            EntityType = command.EntityType,
            EntityId = command.EntityId,
            Module = command.Module,
            OldValues = SerializeToBson(command.OldValues),
            NewValues = SerializeToBson(command.NewValues),
            ChangedFields = command.ChangedFields,
            IpAddress = command.IpAddress,
            UserAgent = command.UserAgent,
            Endpoint = command.Endpoint,
            HttpMethod = command.HttpMethod,
            HttpStatusCode = command.HttpStatusCode,
            DurationMs = command.DurationMs,
            Timestamp = DateTime.UtcNow,
            Metadata = command.Metadata != null ? new BsonDocument(command.Metadata) : null
        };

        _auditQueue.Enqueue(entry);

        // Flush immediately if queue exceeds batch size
        if (_auditQueue.Count >= _settings.BatchSize)
            _ = FlushInternalAsync();

        return Task.CompletedTask;
    }

    // === WRITE: Access log ===

    public Task LogAccessAsync(AccessLogCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.TenantId ?? "default";

        var entry = new AccessLog
        {
            TenantId = tenantId,
            UserId = _currentUserService.UserId?.ToString() ?? "anonymous",
            UserName = _currentUserService.UserName ?? "anonymous",
            Action = command.Action,
            IpAddress = command.IpAddress,
            UserAgent = command.UserAgent,
            Success = command.Success,
            FailureReason = command.FailureReason,
            Timestamp = DateTime.UtcNow
        };

        _accessQueue.Enqueue(entry);

        if (_accessQueue.Count >= _settings.BatchSize)
            _ = FlushInternalAsync();

        return Task.CompletedTask;
    }

    // === FLUSH ===

    public async Task FlushAsync()
    {
        await FlushInternalAsync();
    }

    private async Task FlushInternalAsync()
    {
        if (!await _flushLock.WaitAsync(0))
            return; // Another flush in progress

        try
        {
            await FlushAuditQueueAsync();
            await FlushAccessQueueAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing audit logs to MongoDB, writing fallback");
            await WriteFallbackAsync();
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task FlushAuditQueueAsync()
    {
        var batch = DequeueBatch(_auditQueue, _settings.BatchSize);
        if (batch.Count == 0) return;

        // Group by tenant for per-tenant collections
        foreach (var group in batch.GroupBy(e => e.TenantId))
        {
            var collection = GetAuditCollection(group.Key);
            await collection.InsertManyAsync(group.ToList());
        }

        _logger.LogDebug("Flushed {Count} audit log entries", batch.Count);
    }

    private async Task FlushAccessQueueAsync()
    {
        var batch = DequeueBatch(_accessQueue, _settings.BatchSize);
        if (batch.Count == 0) return;

        foreach (var group in batch.GroupBy(e => e.TenantId))
        {
            var collection = GetAccessCollection(group.Key);
            await collection.InsertManyAsync(group.ToList());
        }

        _logger.LogDebug("Flushed {Count} access log entries", batch.Count);
    }

    private async Task WriteFallbackAsync()
    {
        try
        {
            var fallbackDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(fallbackDir);

            var auditBatch = DequeueBatch(_auditQueue, _settings.BatchSize * 10);
            if (auditBatch.Count > 0)
            {
                var filePath = Path.Combine(fallbackDir, $"audit_fallback_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                var json = JsonSerializer.Serialize(auditBatch, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                _logger.LogWarning("Wrote {Count} audit entries to fallback file: {Path}", auditBatch.Count, filePath);
            }

            var accessBatch = DequeueBatch(_accessQueue, _settings.BatchSize * 10);
            if (accessBatch.Count > 0)
            {
                var filePath = Path.Combine(fallbackDir, $"access_fallback_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                var json = JsonSerializer.Serialize(accessBatch, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                _logger.LogWarning("Wrote {Count} access entries to fallback file: {Path}", accessBatch.Count, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to write audit fallback file");
        }
    }

    // === QUERY: Paged ===

    public async Task<PagedList<AuditLogEntry>> QueryAsync(AuditQueryParameters query, CancellationToken cancellationToken = default)
    {
        var tenantId = query.TenantId ?? _tenantService.TenantId ?? "default";
        var collection = GetAuditCollection(tenantId);

        var filterBuilder = Builders<AuditLog>.Filter;
        var filters = new List<FilterDefinition<AuditLog>>();

        if (!string.IsNullOrEmpty(query.UserId))
            filters.Add(filterBuilder.Eq(a => a.UserId, query.UserId));
        if (!string.IsNullOrEmpty(query.EntityType))
            filters.Add(filterBuilder.Eq(a => a.EntityType, query.EntityType));
        if (!string.IsNullOrEmpty(query.EntityId))
            filters.Add(filterBuilder.Eq(a => a.EntityId, query.EntityId));
        if (!string.IsNullOrEmpty(query.Module))
            filters.Add(filterBuilder.Eq(a => a.Module, query.Module));
        if (!string.IsNullOrEmpty(query.Action))
            filters.Add(filterBuilder.Eq(a => a.Action, query.Action));
        if (query.From.HasValue)
            filters.Add(filterBuilder.Gte(a => a.Timestamp, query.From.Value));
        if (query.To.HasValue)
            filters.Add(filterBuilder.Lte(a => a.Timestamp, query.To.Value));

        var filter = filters.Count > 0
            ? filterBuilder.And(filters)
            : filterBuilder.Empty;

        var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var entries = await collection
            .Find(filter)
            .SortByDescending(a => a.Timestamp)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = entries.Select(MapToEntry).ToList().AsReadOnly();
        return new PagedList<AuditLogEntry>(items, (int)totalCount, query.PageNumber, query.PageSize);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.TenantId ?? "default";
        var collection = GetAuditCollection(tenantId);

        var filter = Builders<AuditLog>.Filter.And(
            Builders<AuditLog>.Filter.Eq(a => a.EntityType, entityType),
            Builders<AuditLog>.Filter.Eq(a => a.EntityId, entityId));

        var entries = await collection
            .Find(filter)
            .SortByDescending(a => a.Timestamp)
            .Limit(500)
            .ToListAsync(cancellationToken);

        return entries.Select(MapToEntry).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetByUserAsync(string userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.TenantId ?? "default";
        var collection = GetAuditCollection(tenantId);

        var filter = Builders<AuditLog>.Filter.And(
            Builders<AuditLog>.Filter.Eq(a => a.UserId, userId),
            Builders<AuditLog>.Filter.Gte(a => a.Timestamp, from),
            Builders<AuditLog>.Filter.Lte(a => a.Timestamp, to));

        var entries = await collection
            .Find(filter)
            .SortByDescending(a => a.Timestamp)
            .Limit(1000)
            .ToListAsync(cancellationToken);

        return entries.Select(MapToEntry).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<AccessLogEntry>> GetAccessLogsAsync(string? userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.TenantId ?? "default";
        var collection = GetAccessCollection(tenantId);

        var filterBuilder = Builders<AccessLog>.Filter;
        var filters = new List<FilterDefinition<AccessLog>>
        {
            filterBuilder.Gte(a => a.Timestamp, from),
            filterBuilder.Lte(a => a.Timestamp, to)
        };

        if (!string.IsNullOrEmpty(userId))
            filters.Add(filterBuilder.Eq(a => a.UserId, userId));

        var filter = filterBuilder.And(filters);

        var entries = await collection
            .Find(filter)
            .SortByDescending(a => a.Timestamp)
            .Limit(1000)
            .ToListAsync(cancellationToken);

        return entries.Select(a => new AccessLogEntry(
            a.Id, a.TenantId, a.UserId, a.UserName,
            a.Action, a.IpAddress, a.UserAgent,
            a.Success, a.FailureReason, a.Timestamp
        )).ToList().AsReadOnly();
    }

    // === Legacy query ===

    public async Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(string entityType, string entityId,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync(new AuditQueryParameters
        {
            EntityType = entityType,
            EntityId = entityId,
            PageNumber = page,
            PageSize = pageSize
        }, cancellationToken);

        return result.Items;
    }

    // === Helpers ===

    private IMongoCollection<AuditLog> GetAuditCollection(string tenantId)
    {
        var db = _client.GetDatabase(_settings.DatabaseName);
        return db.GetCollection<AuditLog>($"audit_{tenantId}");
    }

    private IMongoCollection<AccessLog> GetAccessCollection(string tenantId)
    {
        var db = _client.GetDatabase(_settings.DatabaseName);
        return db.GetCollection<AccessLog>($"access_{tenantId}");
    }

    private static AuditLogEntry MapToEntry(AuditLog a) => new(
        a.Id, a.TenantId, a.UserId, a.UserName,
        a.Action, a.EntityType, a.EntityId, a.Module,
        a.OldValues?.ToJson(), a.NewValues?.ToJson(),
        a.ChangedFields, a.IpAddress, a.Endpoint,
        a.DurationMs, a.Timestamp);

    private static List<T> DequeueBatch<T>(ConcurrentQueue<T> queue, int maxItems)
    {
        var batch = new List<T>(Math.Min(maxItems, queue.Count));
        while (batch.Count < maxItems && queue.TryDequeue(out var item))
            batch.Add(item);
        return batch;
    }

    private static BsonDocument? SerializeToBson(object? value)
    {
        if (value is null) return null;
        try
        {
            var json = JsonSerializer.Serialize(value);
            return BsonDocument.Parse(json);
        }
        catch
        {
            return new BsonDocument("_raw", value.ToString());
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _flushTimer.Dispose();

        // Final flush
        FlushInternalAsync().GetAwaiter().GetResult();

        _flushLock.Dispose();
    }
}
