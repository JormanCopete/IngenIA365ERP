using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Memberships.ActivateMembership;
using IngenIA365ERP.Application.Memberships.DemoteFromTenantAdmin;
using IngenIA365ERP.Application.Memberships.ListTenantMembers;
using IngenIA365ERP.Application.Memberships.PromoteToTenantAdmin;
using IngenIA365ERP.Application.Memberships.RevokeMembership;
using IngenIA365ERP.Application.Memberships.SuspendMembership;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T101 — Endpoints de gestión de membresías de empresa (US4).
/// Autorización delegada al handler (tenant admin o master).
/// </summary>
public sealed class MembershipsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants/{tenantPublicId:guid}/members")
            .WithTags("Identity / Memberships")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/", ListAsync).WithName("Memberships_List");
        group.MapPost("/{publicId:guid}/suspend", SuspendAsync).WithName("Memberships_Suspend");
        group.MapPost("/{publicId:guid}/activate", ActivateAsync).WithName("Memberships_Activate");
        group.MapPost("/{publicId:guid}/revoke", RevokeAsync).WithName("Memberships_Revoke");
        group.MapPost("/{publicId:guid}/promote-admin", PromoteAsync).WithName("Memberships_Promote");
        group.MapPost("/{publicId:guid}/demote-admin", DemoteAsync).WithName("Memberships_Demote");
    }

    private static async Task<object?> ListAsync(
        Guid tenantPublicId,
        [AsParameters] ListParams q,
        ISender sender, CancellationToken ct) =>
        await sender.Send(new ListTenantMembersQuery(
            tenantPublicId, q.Status, q.Page ?? 1, q.PageSize ?? 20), ct);

    public sealed record ListParams(MembershipStatus? Status, int? Page, int? PageSize);

    private static async Task<object?> SuspendAsync(
        Guid tenantPublicId, Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new SuspendMembershipCommand(tenantPublicId, publicId), ct);

    private static async Task<object?> ActivateAsync(
        Guid tenantPublicId, Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new ActivateMembershipCommand(tenantPublicId, publicId), ct);

    private static async Task<object?> RevokeAsync(
        Guid tenantPublicId, Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new RevokeMembershipCommand(tenantPublicId, publicId), ct);

    private static async Task<object?> PromoteAsync(
        Guid tenantPublicId, Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new PromoteToTenantAdminCommand(tenantPublicId, publicId), ct);

    private static async Task<object?> DemoteAsync(
        Guid tenantPublicId, Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new DemoteFromTenantAdminCommand(tenantPublicId, publicId), ct);
}
