using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Saas.DatabaseAdmin;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// Feature 004 (T043) — administracion de base de datos para el master admin
/// (contracts/database-admin.md): seeding bajo demanda y estado de
/// migraciones. Los endpoints solo reenvian a ISender (principio III); la
/// ejecucion del seed queda auditada via AuditBehavior (principio X).
/// </summary>
public sealed class DatabaseAdminModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/saas/database")
            .WithTags("SaaS Admin / Database")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapPost("/seed", SeedAsync).WithName("Saas_DatabaseSeed");
        group.MapGet("/status", StatusAsync).WithName("Saas_DatabaseStatus");
    }

    private static async Task<object?> SeedAsync(
        [FromBody] SeedBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct)
    {
        var guard = RequireMasterAdminAttribute.Check(http);
        if (!guard.IsAllowed)
            return Result.Failure<RunDatabaseSeedResult>(guard.ErrorCode!, guard.Message!);

        return await sender.Send(new RunDatabaseSeedCommand(
            body.Category, body.Scope, body.TenantPublicId, body.ConfirmTestSeed), ct);
    }

    public sealed record SeedBody(
        string Category,
        string Scope,
        Guid? TenantPublicId = null,
        bool ConfirmTestSeed = false);

    private static async Task<object?> StatusAsync(HttpContext http, ISender sender, CancellationToken ct)
    {
        var guard = RequireMasterAdminAttribute.Check(http);
        if (!guard.IsAllowed)
            return Result.Failure<object>(guard.ErrorCode!, guard.Message!);

        return await sender.Send(new GetDatabaseStatusQuery(), ct);
    }
}
