using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Admin.Tenants.ActivateTenant;
using IngenIA365ERP.Application.Admin.Tenants.ListTenants;
using IngenIA365ERP.Application.Admin.Tenants.ProvisionSchema;
using IngenIA365ERP.Application.Admin.Tenants.RegisterTenant;
using IngenIA365ERP.Application.Admin.Tenants.SuspendTenant;
using IngenIA365ERP.Application.Admin.Tenants.UpdateTenant;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T076 — Endpoints SaaS-global de cooperativas. Cada operación exige el
/// permiso correspondiente (<c>Admin.Tenants.*</c>) — el operador del
/// producto debe tener el rol equivalente. Sin permiso → 404 indistinguible.
/// </summary>
public sealed class TenantsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/saas/tenants")
            .WithTags("Admin / Tenants")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/", ListAsync).WithName("Tenants_List")
            .RequirePermission("Admin.Tenants.View");

        group.MapPost("/", RegisterAsync).WithName("Tenants_Register")
            .RequirePermission("Admin.Tenants.Create");

        group.MapPut("/{publicId:guid}", UpdateAsync).WithName("Tenants_Update")
            .RequirePermission("Admin.Tenants.Update");

        group.MapPost("/{publicId:guid}/suspend", SuspendAsync).WithName("Tenants_Suspend")
            .RequirePermission("Admin.Tenants.Suspend");

        group.MapPost("/{publicId:guid}/activate", ActivateAsync).WithName("Tenants_Activate")
            .RequirePermission("Admin.Tenants.Activate");

        group.MapPost("/{publicId:guid}/provision", ProvisionAsync).WithName("Tenants_Provision")
            .RequirePermission("Admin.Tenants.Create");
    }

    private static async Task<object?> ProvisionAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new ProvisionTenantSchemaCommand(publicId), ct);

    private static async Task<object?> ListAsync(
        [AsParameters] TenantsListQueryParams q,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ListTenantsQuery(
            q.Search,
            q.IncludeSuspended,
            new PageRequest(q.Page ?? 1, q.PageSize ?? 20)), ct);

    private static async Task<object?> RegisterAsync(
        [FromBody] RegisterTenantBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new RegisterTenantCommand(
            body.Name, body.SchemaName, body.Subdomain, body.Nit, body.LegalName,
            body.LegalAddress, body.TaxRegime, body.ContactEmail, body.ContactPhone,
            body.PlanType, body.MaxUsers, body.StorageLimitMb), ct);

    private static async Task<object?> UpdateAsync(
        Guid publicId,
        [FromBody] UpdateTenantBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new UpdateTenantCommand(
            publicId, body.Name, body.LegalName, body.LegalAddress, body.TaxRegime,
            body.ContactEmail, body.ContactPhone, body.PlanType, body.MaxUsers,
            body.StorageLimitMb), ct);

    private static async Task<object?> SuspendAsync(
        Guid publicId,
        [FromBody] SuspendTenantBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new SuspendTenantCommand(publicId, body.Reason), ct);

    private static async Task<object?> ActivateAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new ActivateTenantCommand(publicId), ct);
}

public sealed record TenantsListQueryParams(string? Search, bool IncludeSuspended = false, int? Page = null, int? PageSize = null);
public sealed record RegisterTenantBody(
    string Name, string SchemaName, string? Subdomain, string Nit, string LegalName,
    string? LegalAddress, string? TaxRegime, string ContactEmail, string? ContactPhone,
    string PlanType, int MaxUsers, long StorageLimitMb);
public sealed record UpdateTenantBody(
    string Name, string LegalName, string? LegalAddress, string? TaxRegime,
    string ContactEmail, string? ContactPhone, string PlanType, int MaxUsers, long StorageLimitMb);
public sealed record SuspendTenantBody(string Reason);
