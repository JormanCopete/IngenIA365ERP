using System.Collections.Concurrent;
using System.Reflection;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Interceptors;

/// <summary>
/// Captura antes, después y campos cambiados de toda entidad que se guarda, y los audita.
///
/// <para>
/// Feature 012 (T36, T37; T059, T062): el módulo sale de <see cref="ModuloDeAuditoria.Inferir"/> (la misma
/// inferencia que <c>AuditBehavior</c>); las entidades <see cref="SinDiffDeAuditoriaAttribute"/> no dejan
/// diferencias y las propiedades <see cref="NoAuditarAttribute"/> quedan enmascaradas. Para los <b>módulos
/// encadenados</b> (<see cref="AuditoriaEncadenada.Modulos"/>) las diferencias no van a Mongo después de
/// guardar: se agregan a <c>COR_AuditOutbox</c> en <c>SavingChanges</c>, así entran en el mismo
/// <c>SaveChanges</c> —y la misma transacción— que el cambio. Si el guardado falla, se retiran.
/// </para>
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditableEntityInterceptor> _logger;
    private readonly IOrigenDeLaPeticion? _origen;

    private static readonly ConcurrentDictionary<Type, bool> SinDiff = new();
    private static readonly ConcurrentDictionary<PropertyInfo, bool> Enmascaradas = new();

    private List<AuditEntryCapture>? _pendingCaptures;
    private List<AuditOutboxEntry>? _pendingOutbox;

    public AuditableEntityInterceptor(
        ICurrentUserService currentUserService,
        ICurrentTenantService tenantService,
        IAuditService auditService,
        ILogger<AuditableEntityInterceptor> logger,
        IOrigenDeLaPeticion? origen = null)
    {
        _currentUserService = currentUserService;
        _tenantService = tenantService;
        _auditService = auditService;
        _logger = logger;
        _origen = origen;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditInfo(eventData.Context);
        _pendingCaptures = CaptureChanges(eventData.Context);
        AgregarAlOutbox(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo(eventData.Context);
        _pendingCaptures = CaptureChanges(eventData.Context);
        AgregarAlOutbox(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        RetirarDelOutbox(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        RetirarDelOutbox(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>
    /// Las diferencias de los módulos encadenados, como filas de <c>COR_AuditOutbox</c> en el mismo guardado
    /// (T37). Sin cooperativa resuelta no hay flujo: siguen por Mongo, como antes.
    /// </summary>
    private void AgregarAlOutbox(Microsoft.EntityFrameworkCore.DbContext? context)
    {
        _pendingOutbox = null;
        if (context is null || _pendingCaptures is null || _pendingCaptures.Count == 0) return;

        var flujo = AuditoriaEncadenada.Flujo(_tenantService.TenantId);
        if (flujo is null) return;

        var encadenadas = _pendingCaptures.Where(c => AuditoriaEncadenada.EsEncadenado(c.Module)).ToList();
        if (encadenadas.Count == 0) return;

        var contexto = Contexto();
        _pendingOutbox = [];
        foreach (var capture in encadenadas)
        {
            var entrada = AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, new AuditLogCommand
            {
                Action = capture.Action,
                EntityType = capture.EntityType,
                EntityId = capture.EntityId,
                Module = capture.Module,
                OldValues = capture.OldValues,
                NewValues = capture.NewValues,
                ChangedFields = capture.ChangedFields,
            }));
            context.Add(entrada);
            _pendingOutbox.Add(entrada);
            _pendingCaptures.Remove(capture);
        }
    }

    private void RetirarDelOutbox(Microsoft.EntityFrameworkCore.DbContext? context)
    {
        if (context is not null && _pendingOutbox is not null)
        {
            foreach (var entrada in _pendingOutbox)
            {
                var entry = context.Entry(entrada);
                if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
            }
        }
        _pendingOutbox = null;
        _pendingCaptures = null;
    }

    /// <summary>Dentro de SavingChanges no se consulta nada: sólo lo que ya se sabe de la petición o del trabajo.</summary>
    private ContextoDeAuditoria Contexto()
    {
        var canal = _origen?.Canal ?? (ContextoAmbiental.Activo ? ExecutionChannel.Process : ExecutionChannel.Web);
        var metadata = new Dictionary<string, string> { ["Channel"] = AuditoriaEncadenada.Canal(canal) };
        var origen = _origen?.Origen ?? ContextoAmbiental.Origen;
        if (!string.IsNullOrWhiteSpace(origen)) metadata["Origin"] = origen;
        var endpoint = _origen?.Endpoint;
        return new ContextoDeAuditoria(
            _tenantService.TenantId,
            null,
            _currentUserService.UserId?.ToString() ?? "system",
            _currentUserService.UserName ?? "system",
            _origen?.Ip,
            _origen?.UserAgent,
            endpoint,
            endpoint is not null && endpoint.IndexOf(' ') is > 0 and var i ? endpoint[..i] : null,
            metadata);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        SendAuditLogs();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        SendAuditLogs();
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditInfo(Microsoft.EntityFrameworkCore.DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;
        var userId = _currentUserService.UserName ?? "system";

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntityLong>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }

    private List<AuditEntryCapture> CaptureChanges(Microsoft.EntityFrameworkCore.DbContext? context)
    {
        var captures = new List<AuditEntryCapture>();
        if (context is null) return captures;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            // Skip entities without a meaningful type name
            var entityType = entry.Entity.GetType();
            if (entityType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true)
                continue;

            // Feature 012 (T36): filas técnicas cuyo cambio no es un hecho de negocio.
            if (EsSinDiff(entityType))
                continue;

            var capture = new AuditEntryCapture
            {
                EntityType = entityType.Name,
                Action = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => entry.State.ToString()
                }
            };

            // Get entity ID (PublicId preferred, then Id)
            var publicIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "PublicId");
            var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
            capture.EntityId = publicIdProp?.CurrentValue?.ToString() ?? idProp?.CurrentValue?.ToString();

            // Infer module from namespace (feature 012: la misma inferencia que AuditBehavior)
            capture.Module = ModuloDeAuditoria.Inferir(entityType.Namespace, "Unknown");

            // Capture old/new values and changed fields
            switch (entry.State)
            {
                case EntityState.Added:
                    capture.NewValues = GetPropertyValues(entry, e => e.CurrentValue);
                    break;

                case EntityState.Modified:
                    var changedProps = entry.Properties.Where(p => p.IsModified).ToList();
                    capture.ChangedFields = changedProps.Select(p => p.Metadata.Name).ToList();
                    capture.OldValues = GetPropertyValues(changedProps, e => e.OriginalValue);
                    capture.NewValues = GetPropertyValues(changedProps, e => e.CurrentValue);
                    break;

                case EntityState.Deleted:
                    capture.OldValues = GetPropertyValues(entry, e => e.OriginalValue);
                    break;
            }

            captures.Add(capture);
        }

        return captures;
    }

    private void SendAuditLogs()
    {
        if (_pendingCaptures is null || _pendingCaptures.Count == 0)
            return;

        try
        {
            foreach (var capture in _pendingCaptures)
            {
                _ = _auditService.LogAsync(new AuditLogCommand
                {
                    Action = capture.Action,
                    EntityType = capture.EntityType,
                    EntityId = capture.EntityId,
                    Module = capture.Module,
                    OldValues = capture.OldValues,
                    NewValues = capture.NewValues,
                    ChangedFields = capture.ChangedFields
                });
            }
        }
        catch (Exception ex)
        {
            // Never block the business operation
            _logger.LogError(ex, "Failed to send audit logs to MongoDB");
        }
        finally
        {
            _pendingCaptures = null;
        }
    }

    private static Dictionary<string, object?> GetPropertyValues(EntityEntry entry, Func<PropertyEntry, object?> valueSelector)
    {
        return GetPropertyValues(entry.Properties.ToList(), valueSelector);
    }

    private static Dictionary<string, object?> GetPropertyValues(List<PropertyEntry> properties, Func<PropertyEntry, object?> valueSelector)
    {
        return properties
            .Where(p => !IsAuditField(p.Metadata.Name))
            .ToDictionary(p => p.Metadata.Name, p => EsEnmascarada(p) ? NoAuditarAttribute.Mascara : valueSelector(p));
    }

    private static bool EsSinDiff(Type tipo) =>
        SinDiff.GetOrAdd(tipo, t => t.GetCustomAttribute<SinDiffDeAuditoriaAttribute>() is not null);

    /// <summary>[NoAuditar]: el cambio se registra (la propiedad sigue en changedFields), el valor no.</summary>
    private static bool EsEnmascarada(PropertyEntry propiedad) =>
        propiedad.Metadata.PropertyInfo is { } info
        && Enmascaradas.GetOrAdd(info, i => i.GetCustomAttribute<NoAuditarAttribute>(inherit: true) is not null);

    private static bool IsAuditField(string name) =>
        name is "CreatedAt" or "CreatedBy" or "UpdatedAt" or "UpdatedBy"
            or "DeletedAt" or "DeletedBy" or "IsDeleted";

    private class AuditEntryCapture
    {
        public string EntityType { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string Module { get; set; } = string.Empty;
        public Dictionary<string, object?>? OldValues { get; set; }
        public Dictionary<string, object?>? NewValues { get; set; }
        public List<string>? ChangedFields { get; set; }
    }
}
