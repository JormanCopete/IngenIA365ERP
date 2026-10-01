using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Security.Roles.CreateRole;
using IngenIA365ERP.Application.Security.Roles.DeleteRole;
using IngenIA365ERP.Application.Security.Roles.GetRoleByPublicId;
using IngenIA365ERP.Application.Security.Roles.ListRoles;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
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

        // Feature 012 (T128; contracts/api.md §1.5): perfiles sugeridos. Ver las plantillas y crear desde una son
        // parte de crear un rol, así que ambas exigen Security.Roles.Create; el POST lleva Idempotency-Key.
        group.MapGet("/templates", ListTemplatesAsync).WithName("Roles_Templates")
            .RequirePermission("Security.Roles.Create");

        group.MapPost("/from-template", CreateFromTemplateAsync).WithName("Roles_CreateFromTemplate")
            .ConClaveDeOperacion()
            .RequirePermission("Security.Roles.Create");

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

    private static async Task<object?> ListTemplatesAsync(
        string? module, ISender sender, CancellationToken ct) =>
        await sender.Send(new ListRoleTemplatesQuery(module), ct);

    private static async Task<object?> CreateFromTemplateAsync(
        [FromBody] CreateRoleFromTemplateBody body, HttpContext http, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new CreateRoleFromTemplateCommand(
            body.TemplateKey ?? string.Empty, body.Code ?? string.Empty, body.Name ?? string.Empty, body.Description)
        {
            OperationKey = http.ClaveDeOperacion(),
        }, ct);
        return result.IsSuccess
            ? Results.Created($"/api/admin/roles/{result.Value.RolePublicId}", result.Value)
            : result;
    }

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
/// <summary>Cuerpo de <c>POST /api/admin/roles/from-template</c> (contracts/api.md §1.5).</summary>
public sealed record CreateRoleFromTemplateBody(
    string? TemplateKey, string? Code, string? Name, string? Description);
