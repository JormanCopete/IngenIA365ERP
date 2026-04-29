using Carter;
using IngenIA365ERP.Application.Payroll.AutoContributionParams.Commands.CreateAutoContributionParam;
using IngenIA365ERP.Application.Payroll.AutoContributionParams.Commands.UpdateAutoContributionParam;
using IngenIA365ERP.Application.Payroll.AutoContributionParams.Commands.DeleteAutoContributionParam;
using IngenIA365ERP.Application.Payroll.AutoContributionParams.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class AutoContributionParamsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/auto-contribution-params")
            .WithTags("AutoContributionParams")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListAutoContributionParamsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListAutoContributionParams");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAutoContributionParamByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetAutoContributionParamById");

        group.MapPost("/", async (CreateAutoContributionParamCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/auto-contribution-params/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateAutoContributionParam");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAutoContributionParamCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateAutoContributionParam");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteAutoContributionParamCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteAutoContributionParam");
    }
}
