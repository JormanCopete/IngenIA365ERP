using Carter;
using IngenIA365ERP.Application.Payroll.SeveranceProviders.Commands.CreateSeveranceProvider;
using IngenIA365ERP.Application.Payroll.SeveranceProviders.Commands.UpdateSeveranceProvider;
using IngenIA365ERP.Application.Payroll.SeveranceProviders.Commands.DeleteSeveranceProvider;
using IngenIA365ERP.Application.Payroll.SeveranceProviders.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class SeveranceProvidersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/severance-providers")
            .WithTags("SeveranceProviders")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSeveranceProvidersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSeveranceProviders");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSeveranceProviderByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSeveranceProviderById");

        group.MapPost("/", async (CreateSeveranceProviderCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/severance-providers/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSeveranceProvider");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSeveranceProviderCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSeveranceProvider");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSeveranceProviderCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSeveranceProvider");
    }
}
