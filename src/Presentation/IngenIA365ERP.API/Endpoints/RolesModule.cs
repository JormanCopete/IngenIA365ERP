using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Security.Roles.CreateRole;
using IngenIA365ERP.Application.Security.Roles.DeleteRole;
using IngenIA365ERP.Application.Security.Roles.GetRoleByPublicId;
using IngenIA365ERP.Application.Security.Roles.ListRoles;
using IngenIA365ERP.Application.Security.Roles.UpdateRole;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

public sealed class RolesModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/roles")
            .WithTags("Admin / Roles")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/", ListAsync).WithName("Roles_List")
            .RequirePermission("Security.Roles.View");

        group.MapGet("/{publicId:guid}", GetAsync).WithName("Roles_Get")
            .RequirePermission("Security.Roles.View");

        group.MapPost("/", CreateAsync).WithName("Roles_Create")
            .RequirePermission("Security.Roles.Create");

        group.MapPut("/{publicId:guid}", UpdateAsync).WithName("Roles_Update")
            .RequirePermission("Security.Roles.Update");

        group.MapDelete("/{publicId:guid}", DeleteAsync).WithName("Roles_Delete")
            .RequirePermission("Security.Roles.Delete");
    }

    private static async Task<object?> ListAsync(
        [AsParameters] RolesListQueryParams q, ISender sender, CancellationToken ct) =>
        await sender.Send(new ListRolesQuery(
            q.Search, q.IncludeBuiltIn,
            new PageRequest(q.Page ?? 1, q.PageSize ?? 20)), ct);

    private static async Task<object?> GetAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new GetRoleByPublicIdQuery(publicId), ct);

    private static async Task<object?> CreateAsync(
        [FromBody] CreateRoleBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new CreateRoleCommand(
            body.Code, body.Name, body.Description,
            body.PermissionPublicIds ?? []), ct);

    private static async Task<object?> UpdateAsync(
        Guid publicId, [FromBody] UpdateRoleBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new UpdateRoleCommand(
            publicId, body.Name, body.Description, body.IsAssignable,
            body.PermissionPublicIds ?? []), ct);

    private static async Task<object?> DeleteAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new DeleteRoleCommand(publicId), ct);
}

public sealed record RolesListQueryParams(
    string? Search, bool IncludeBuiltIn = true, int? Page = null, int? PageSize = null);
public sealed record CreateRoleBody(
    string Code, string Name, string? Description, IReadOnlyList<Guid>? PermissionPublicIds);
public sealed record UpdateRoleBody(
    string Name, string? Description, bool IsAssignable, IReadOnlyList<Guid>? PermissionPublicIds);
