using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Security.Users.AdminResetPassword;
using IngenIA365ERP.Application.Security.Users.AssignBranch;
using IngenIA365ERP.Application.Security.Users.AssignRole;
using IngenIA365ERP.Application.Security.Users.DisableUser;
using IngenIA365ERP.Application.Security.Users.GetUserByPublicId;
using IngenIA365ERP.Application.Security.Users.ListUsers;
using IngenIA365ERP.Application.Security.Users.RemoveRole;
using IngenIA365ERP.Application.Security.Users.RestoreUser;
using IngenIA365ERP.Application.Security.Users.UnlockUser;
using IngenIA365ERP.Application.Security.Users.UpdateUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T076 — 11 endpoints REST sobre <c>/api/admin/users</c>. Cada uno exige
/// el permiso correspondiente; sin él, 404 indistinguible (FR-017).
/// </summary>
public sealed class UsersModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users")
            .WithTags("Admin / Users")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/", ListAsync).WithName("Users_List")
            .RequirePermission("Security.Users.View");

        group.MapGet("/{publicId:guid}", GetAsync).WithName("Users_Get")
            .RequirePermission("Security.Users.View");

        // El alta directa se retiro. Crear una fila SEC_Users sin identidad central
        // produce alguien que existe en la pantalla, al que se le pueden asignar
        // roles, y que NO puede entrar: el acceso valida contra ADM_CentralUsers,
        // que ese camino no tocaba. Las personas entran por invitacion
        // —POST /api/tenants/{id}/invitations— que crea las dos mitades.

        group.MapPut("/{publicId:guid}", UpdateAsync).WithName("Users_Update")
            .RequirePermission("Security.Users.Update");

        group.MapPost("/{publicId:guid}/disable", DisableAsync).WithName("Users_Disable")
            .RequirePermission("Security.Users.Disable");

        group.MapPost("/{publicId:guid}/restore", RestoreAsync).WithName("Users_Restore")
            .RequirePermission("Security.Users.Restore");

        group.MapPost("/{publicId:guid}/roles", AssignRoleAsync).WithName("Users_AssignRole")
            .RequirePermission("Security.Users.AssignRole");

        group.MapDelete("/{publicId:guid}/roles/{rolePublicId:guid}", RemoveRoleAsync).WithName("Users_RemoveRole")
            .RequirePermission("Security.Users.RemoveRole");

        group.MapPost("/{publicId:guid}/branches", AssignBranchAsync).WithName("Users_AssignBranch")
            .RequirePermission("Security.Users.AssignBranch");

        group.MapPost("/{publicId:guid}/reset-password", ResetPasswordAsync).WithName("Users_ResetPassword")
            .RequirePermission("Security.Users.ResetPassword");

        group.MapPost("/{publicId:guid}/unlock", UnlockAsync).WithName("Users_Unlock")
            .RequirePermission("Security.Users.Unlock");
    }

    private static async Task<object?> ListAsync(
        [AsParameters] UsersListQueryParams q,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ListUsersQuery(
            q.Search, q.IncludeDisabled, q.RoleCode,
            new PageRequest(q.Page ?? 1, q.PageSize ?? 20)), ct);

    private static async Task<object?> GetAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new GetUserByPublicIdQuery(publicId), ct);

    private static async Task<object?> UpdateAsync(
        Guid publicId, [FromBody] UpdateUserBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new UpdateUserCommand(
            publicId, body.Email, body.IdentificationNumber, body.PersonId), ct);

    private static async Task<object?> DisableAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new DisableUserCommand(publicId), ct);

    private static async Task<object?> RestoreAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new RestoreUserCommand(publicId), ct);

    private static async Task<object?> AssignRoleAsync(
        Guid publicId, [FromBody] AssignRoleBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new AssignRoleCommand(publicId, body.RolePublicId), ct);

    private static async Task<object?> RemoveRoleAsync(
        Guid publicId, Guid rolePublicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new RemoveRoleCommand(publicId, rolePublicId), ct);

    private static async Task<object?> AssignBranchAsync(
        Guid publicId, [FromBody] AssignBranchBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new AssignBranchCommand(publicId, body.BranchPublicId, body.IsDefault), ct);

    private static async Task<object?> ResetPasswordAsync(
        Guid publicId, [FromBody] AdminResetPasswordBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new AdminResetPasswordCommand(publicId, body.NewPassword), ct);

    private static async Task<object?> UnlockAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new UnlockUserCommand(publicId), ct);
}

public sealed record UsersListQueryParams(
    string? Search, bool IncludeDisabled = false, string? RoleCode = null,
    int? Page = null, int? PageSize = null);

public sealed record UpdateUserBody(string Email, string? IdentificationNumber, int? PersonId);
public sealed record AssignRoleBody(Guid RolePublicId);
public sealed record AssignBranchBody(Guid BranchPublicId, bool IsDefault);
public sealed record AdminResetPasswordBody(string NewPassword);
