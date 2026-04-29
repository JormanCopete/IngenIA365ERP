using Carter;
using IngenIA365ERP.Application.Payroll.PensionProviders.Commands.CreatePensionProvider;
using IngenIA365ERP.Application.Payroll.PensionProviders.Commands.UpdatePensionProvider;
using IngenIA365ERP.Application.Payroll.PensionProviders.Commands.DeletePensionProvider;
using IngenIA365ERP.Application.Payroll.PensionProviders.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class PensionProvidersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/pension-providers")
            .WithTags("PensionProviders")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPensionProvidersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPensionProviders");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPensionProviderByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPensionProviderById");

        group.MapPost("/", async (CreatePensionProviderCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/pension-providers/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePensionProvider");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePensionProviderCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePensionProvider");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePensionProviderCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePensionProvider");
    }
}
