using Carter;
using IngenIA365ERP.Application.Payroll.WorkRiskRates.Commands.CreateWorkRiskRate;
using IngenIA365ERP.Application.Payroll.WorkRiskRates.Commands.UpdateWorkRiskRate;
using IngenIA365ERP.Application.Payroll.WorkRiskRates.Commands.DeleteWorkRiskRate;
using IngenIA365ERP.Application.Payroll.WorkRiskRates.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class WorkRiskRatesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/work-risk-rates")
            .WithTags("WorkRiskRates")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListWorkRiskRatesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListWorkRiskRates");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetWorkRiskRateByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetWorkRiskRateById");

        group.MapPost("/", async (CreateWorkRiskRateCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/work-risk-rates/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateWorkRiskRate");

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkRiskRateCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateWorkRiskRate");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteWorkRiskRateCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteWorkRiskRate");
    }
}
