using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Interceptors;

public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditableEntityInterceptor> _logger;

    private List<AuditEntryCapture>? _pendingCaptures;

    public AuditableEntityInterceptor(
        ICurrentUserService currentUserService,
        ICurrentTenantService tenantService,
        IAuditService auditService,
        ILogger<AuditableEntityInterceptor> logger)
    {
        _currentUserService = currentUserService;
        _tenantService = tenantService;
        _auditService = auditService;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditInfo(eventData.Context);
        _pendingCaptures = CaptureChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo(eventData.Context);
        _pendingCaptures = CaptureChanges(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
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

            // Infer module from namespace
            var ns = entityType.Namespace ?? string.Empty;
            capture.Module = InferModule(ns);

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
        return entry.Properties
            .Where(p => !IsAuditField(p.Metadata.Name))
            .ToDictionary(p => p.Metadata.Name, valueSelector);
    }

    private static Dictionary<string, object?> GetPropertyValues(List<PropertyEntry> properties, Func<PropertyEntry, object?> valueSelector)
    {
        return properties
            .Where(p => !IsAuditField(p.Metadata.Name))
            .ToDictionary(p => p.Metadata.Name, valueSelector);
    }

    private static bool IsAuditField(string name) =>
        name is "CreatedAt" or "CreatedBy" or "UpdatedAt" or "UpdatedBy"
            or "DeletedAt" or "DeletedBy" or "IsDeleted";

    private static string InferModule(string ns)
    {
        if (ns.Contains(".Accounting")) return "Accounting";
        if (ns.Contains(".Lending")) return "Lending";
        if (ns.Contains(".Payroll")) return "Payroll";
        if (ns.Contains(".Inventory")) return "Inventory";
        if (ns.Contains(".CDT")) return "CDT";
        if (ns.Contains(".Debit")) return "Debit";
        if (ns.Contains(".Treasury")) return "Treasury";
        if (ns.Contains(".Security")) return "Security";
        if (ns.Contains(".Audit")) return "Audit";
        if (ns.Contains(".Web")) return "Web";
        if (ns.Contains(".Admin")) return "Admin";
        if (ns.Contains(".Core")) return "Core";
        return "Unknown";
    }

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
