using Carter;
using IngenIA365ERP.Persistence.MultiTenancy;

namespace IngenIA365ERP.API.Endpoints.Admin;

public class TenantEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/tenants").WithTags("Tenants");

        group.MapGet("/", async (TenantSchemaService service) =>
        {
            var tenants = await service.ListTenantsAsync();
            return Results.Ok(tenants);
        });

        group.MapPost("/", async (CreateTenantRequest request, TenantSchemaService service) =>
        {
            var tenant = await service.CreateTenantAsync(request.Identifier, request.Name, request.PlanType);
            return Results.Created($"/api/admin/tenants/{tenant.Identifier}", tenant);
        });

        group.MapDelete("/{identifier}", async (string identifier, TenantSchemaService service) =>
        {
            var deleted = await service.DropTenantAsync(identifier);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }
}

public record CreateTenantRequest(string Identifier, string Name, string? PlanType = "Basic");
