using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.CreateSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.UpdateSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.DeleteSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Vendedores heredados (<c>INV_Salespeople</c>), lo único del inventario viejo que sobrevive a
/// <c>RetiroDelInventarioHeredado</c> (feature 012, T036). Hasta entonces bastaba con estar autenticado;
/// desde aquí las lecturas piden <c>Inventory.Salespeople.View</c> y las escrituras
/// <c>Inventory.Salespeople.Manage</c>. Los dos códigos los siembra el catálogo de permisos del
/// inventario (fase 3, T48); hasta entonces nadie los tiene y sólo entra el administrador maestro.
/// La reescritura de la operación (restaurar o crear, retiro con motivo) es de US12.
/// </summary>
public class SalespeopleEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/salespeople")
            .WithTags("Salespeople")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSalespeopleQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSalespeople")
          .AddEndpointFilter<ErrorEnvelopeFilter>()
          .RequirePermission("Inventory.Salespeople.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSalespersonByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSalespersonById")
          .AddEndpointFilter<ErrorEnvelopeFilter>()
          .RequirePermission("Inventory.Salespeople.View");

        group.MapPost("/", async (CreateSalespersonCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/salespeople/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSalesperson")
          .AddEndpointFilter<ErrorEnvelopeFilter>()
          .RequirePermission("Inventory.Salespeople.Manage");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSalespersonCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSalesperson")
          .AddEndpointFilter<ErrorEnvelopeFilter>()
          .RequirePermission("Inventory.Salespeople.Manage");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSalespersonCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSalesperson")
          .AddEndpointFilter<ErrorEnvelopeFilter>()
          .RequirePermission("Inventory.Salespeople.Manage");
    }
}
