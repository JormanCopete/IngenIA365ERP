using Carter;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

public class AuditEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit")
            .WithTags("Audit")
            .RequireAuthorization();

        group.MapGet("/logs", GetAuditLogsAsync)
            .WithName("GetAuditLogs")
            .WithDescription("Consultar logs de auditoría con filtros");

        group.MapGet("/logs/{entityType}/{entityId}", GetEntityHistoryAsync)
            .WithName("GetEntityHistory")
            .WithDescription("Historial de auditoría de una entidad");

        group.MapGet("/users/{userId}/activity", GetUserActivityAsync)
            .WithName("GetUserActivity")
            .WithDescription("Actividad de un usuario en un rango de fechas");

        group.MapGet("/access-logs", GetAccessLogsAsync)
            .WithName("GetAccessLogs")
            .WithDescription("Logs de acceso (login/logout)");
    }

    private static async Task<IResult> GetAuditLogsAsync(
        IAuditService auditService,
        [FromQuery] string? userId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? module = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new AuditQueryParameters
        {
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            Module = module,
            Action = action,
            From = from,
            To = to,
            PageNumber = pageNumber,
            PageSize = Math.Min(pageSize, 200)
        };

        var result = await auditService.QueryAsync(query);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEntityHistoryAsync(
        IAuditService auditService,
        string entityType,
        string entityId)
    {
        var result = await auditService.GetByEntityAsync(entityType, entityId);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetUserActivityAsync(
        IAuditService auditService,
        string userId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var result = await auditService.GetByUserAsync(userId, fromDate, toDate);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAccessLogsAsync(
        IAuditService auditService,
        [FromQuery] string? userId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;

        var result = await auditService.GetAccessLogsAsync(userId, fromDate, toDate);
        return Results.Ok(result);
    }
}
