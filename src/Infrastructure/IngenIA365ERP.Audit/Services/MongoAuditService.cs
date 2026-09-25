using System.Collections.Concurrent;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Execution;
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

    /// <summary>
    /// La base de auditoría a la que va (o de la que se lee) este evento: la de la cooperativa, o la
    /// global cuando la operación no tiene cooperativa (identidad, plataforma).
    ///
    /// <para>
    /// <b>Guarda de segundo plano</b> (feature 012, T5, FR-083, T045): un trabajo de fondo corre por
    /// <c>IEjecutorEnCooperativa</c>, que fija la cooperativa en <see cref="ContextoAmbiental"/>. Si
    /// hay contexto y aun así no se resolvió cooperativa, algo está mal cableado, y caer a la global
    /// escondería el evento donde nadie de esa cooperativa lo ve. Se niega: <c>Critical</c> y
    /// excepción, nunca <see cref="AuditDatabaseNames.SufijoGlobal"/> desde un trabajo de fondo.
    /// </para>
    /// </summary>
    private string CooperativaOGlobal()
    {
        var cooperativa = _tenantService.TenantId;
        if (cooperativa is not null) return cooperativa;

        if (ContextoAmbiental.Activo)
        {
            _logger.LogCritical(
                "[Auditoria.SegundoPlanSinCooperativa] Un trabajo de fondo ({Origen}, actor {Actor}) intentó " +
                "auditar sin cooperativa resuelta. No se escribe en la base global.",
                ContextoAmbiental.Origen, ContextoAmbiental.Actor?.Name);
            throw new InvalidOperationException(
                $"Auditoría en segundo plano sin cooperativa resuelta ({ContextoAmbiental.Origen}). " +
                "Nunca se cae a la base Global desde un trabajo de fondo: revisá que corra por IEjecutorEnCooperativa.");
        }

        return AuditDatabaseNames.SufijoGlobal;
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
        // Sin cooperativa el rastro va a la base GLOBAL, no a un cubo llamado
        // "default".
        //
        // Aqui se caia de vuelta al literal «default», y eso es lo que
        // AuditDatabaseNames documenta como el origen de la basura: el nombre
        // llegaba ya resuelto a "default", asi que la constante Global no se
        // aplicaba nunca. Resultado medido en desarrollo: 148 documentos en
        // IngenIA365ERP_Audit_default y CERO en IngenIA365ERP_Audit_Global, que
        // es la que lee la consola. Los eventos de identidad —inicios de sesion,
        // segundo factor, invitaciones— se escribian y quedaban invisibles.
        var tenantId = CooperativaOGlobal();

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
        var tenantId = CooperativaOGlobal();

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
            // El mismo documento que escribe AppendOnlyAuditWriter, no la clase
            // serializada: una sola forma en la coleccion de aqui en adelante.
            await collection.InsertManyAsync(group.Select(AuditDocumentSchema.ToDocument).ToList());
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
        var tenantId = query.TenantId ?? CooperativaOGlobal();
        var collection = GetAuditCollection(tenantId);

        // Filtro, orden y mapeo entienden las dos formas del documento; ver
        // AuditDocumentSchema. Aqui no se nombra ningun campo a proposito.
        var filter = AuditDocumentSchema.Filtro(query);

        var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var documentos = await collection
            .Find(filter)
            .Sort(AuditDocumentSchema.MasRecientePrimero)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = documentos.Select(AuditDocumentSchema.ToEntry).ToList().AsReadOnly();
        return new PagedList<AuditLogEntry>(items, (int)totalCount, query.PageNumber, query.PageSize);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        var tenantId = CooperativaOGlobal();
        var collection = GetAuditCollection(tenantId);

        var filter = AuditDocumentSchema.FiltroPorEntidad(entityType, entityId);

        var documentos = await collection
            .Find(filter)
            .Sort(AuditDocumentSchema.MasRecientePrimero)
            .Limit(500)
            .ToListAsync(cancellationToken);

        return documentos.Select(AuditDocumentSchema.ToEntry).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetByUserAsync(string userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = CooperativaOGlobal();
        var collection = GetAuditCollection(tenantId);

        var filter = AuditDocumentSchema.FiltroPorUsuario(userId, from, to);

        var documentos = await collection
            .Find(filter)
            .Sort(AuditDocumentSchema.MasRecientePrimero)
            .Limit(1000)
            .ToListAsync(cancellationToken);

        return documentos.Select(AuditDocumentSchema.ToEntry).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<AccessLogEntry>> GetAccessLogsAsync(string? userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var tenantId = CooperativaOGlobal();
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

    /// <summary>
    /// Nombre de la colección de auditoría de una cooperativa.
    ///
    /// <para>
    /// <b>Una sola convención, y esto cerraba una brecha.</b> Había dos:
    /// <c>AppendOnlyAuditWriter</c> escribía en <c>audit_events_{tenantId}</c> y
    /// esta consola leía de <c>audit_{tenantId}</c>. Colecciones distintas, así que
    /// todo lo que registran los handlers de identidad —inicios de sesión, segundo
    /// factor, invitaciones, membresías— quedaba escrito y era <b>invisible</b>
    /// desde la consola de auditoría. El rastro regulatorio existía y nadie podía
    /// consultarlo.
    /// </para>
    ///
    /// <para>
    /// Se conserva <c>audit_events_</c> y no la otra porque es la que ya tiene los
    /// cuatro índices y el TTL de cinco años que exige FR-023.
    /// </para>
    /// </summary>
    /// <summary>
    /// Se abre como <see cref="BsonDocument"/>, no como <see cref="AuditLog"/>. El
    /// class map exigía que cada documento tuviera exactamente sus propiedades, y
    /// la colección guarda dos formas (ver <see cref="AuditDocumentSchema"/>): con
    /// el primer documento de la otra forma la consola entera devolvía 500.
    /// </summary>
    private IMongoCollection<BsonDocument> GetAuditCollection(string tenantId)
    {
        // Base por cooperativa, coleccion constante dentro. Antes era al reves:
        // una base compartida con una coleccion por cooperativa. El aislamiento
        // por coleccion depende de que nadie componga mal el nombre; el de base
        // lo sostiene el motor.
        var db = _client.GetDatabase(
            AuditDatabaseNames.Para(_settings.DatabaseName, tenantId));
        return db.GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);
    }

    private IMongoCollection<AccessLog> GetAccessCollection(string tenantId)
    {
        var db = _client.GetDatabase(
            AuditDatabaseNames.Para(_settings.DatabaseName, tenantId));
        return db.GetCollection<AccessLog>("access_log");
    }

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
