using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Admin.Branches.CreateBranch;
using IngenIA365ERP.Application.Admin.Branches.DeactivateBranch;
using IngenIA365ERP.Application.Admin.Branches.ListBranches;
using IngenIA365ERP.Application.Admin.Branches.UpdateBranch;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

public sealed class BranchesModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/branches")
            .WithTags("Admin / Branches")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/", ListAsync).WithName("Branches_List")
            .RequirePermission("Admin.Branches.View");

        group.MapPost("/", CreateAsync).WithName("Branches_Create")
            .RequirePermission("Admin.Branches.Create");

        group.MapPut("/{publicId:guid}", UpdateAsync).WithName("Branches_Update")
            .RequirePermission("Admin.Branches.Update");

        group.MapPost("/{publicId:guid}/deactivate", DeactivateAsync).WithName("Branches_Deactivate")
            .RequirePermission("Admin.Branches.Deactivate");
    }

    private static async Task<object?> ListAsync(
        [AsParameters] BranchesListQueryParams q,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ListBranchesQuery(
            q.TenantPublicId, q.Search, q.IncludeInactive,
            new PageRequest(q.Page ?? 1, q.PageSize ?? 20)), ct);

    private static async Task<object?> CreateAsync(
        [FromBody] CreateBranchBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new CreateBranchCommand(
            body.TenantPublicId, body.Code, body.Name, body.Address,
            body.Phone, body.Email, body.IsHeadquarters), ct);

    private static async Task<object?> UpdateAsync(
        Guid publicId,
        [FromBody] UpdateBranchBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new UpdateBranchCommand(
            publicId, body.Name, body.Address, body.Phone, body.Email, body.IsHeadquarters), ct);

    private static async Task<object?> DeactivateAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new DeactivateBranchCommand(publicId), ct);
}

public sealed record BranchesListQueryParams(
    Guid? TenantPublicId, string? Search, bool IncludeInactive = false, int? Page = null, int? PageSize = null);
public sealed record CreateBranchBody(
    Guid TenantPublicId, string Code, string Name,
    string? Address, string? Phone, string? Email, bool IsHeadquarters);
public sealed record UpdateBranchBody(
    string Name, string? Address, string? Phone, string? Email, bool IsHeadquarters);
