using Carter;
using IngenIA365ERP.Application.Payroll.WorkRiskProviders.Commands.CreateWorkRiskProvider;
using IngenIA365ERP.Application.Payroll.WorkRiskProviders.Commands.UpdateWorkRiskProvider;
using IngenIA365ERP.Application.Payroll.WorkRiskProviders.Commands.DeleteWorkRiskProvider;
using IngenIA365ERP.Application.Payroll.WorkRiskProviders.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class WorkRiskProvidersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/work-risk")
            .WithTags("WorkRiskProviders")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListWorkRiskProvidersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListWorkRiskProviders");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetWorkRiskProviderByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetWorkRiskProviderById");

        group.MapPost("/", async (CreateWorkRiskProviderCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/work-risk/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateWorkRiskProvider");

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkRiskProviderCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateWorkRiskProvider");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteWorkRiskProviderCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteWorkRiskProvider");
    }
}
