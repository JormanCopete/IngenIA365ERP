using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Security.Permissions;
using MediatR;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// Catálogo de permisos — solo lectura, expuesto para que la UI pueda
/// poblar la matriz de roles. No requiere mutaciones desde la API
/// (los permisos se siembran y son inmutables).
/// </summary>
public sealed class PermissionsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/permissions")
            .WithTags("Admin / Permissions")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/", ListAsync).WithName("Permissions_List")
            .RequirePermission("Security.Permissions.View");
    }

    private static async Task<object?> ListAsync(
        string? module, ISender sender, CancellationToken ct) =>
        await sender.Send(new ListPermissionsQuery(module), ct);
}
