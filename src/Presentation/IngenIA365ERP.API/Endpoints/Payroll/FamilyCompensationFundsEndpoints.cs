using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.FamilyCompensationFunds.Commands.CreateFamilyCompensationFund;
using IngenIA365ERP.Application.Payroll.FamilyCompensationFunds.Commands.UpdateFamilyCompensationFund;
using IngenIA365ERP.Application.Payroll.FamilyCompensationFunds.Commands.DeleteFamilyCompensationFund;
using IngenIA365ERP.Application.Payroll.FamilyCompensationFunds.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class FamilyCompensationFundsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/family-compensation-funds")
            .WithTags("FamilyCompensationFunds")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListFamilyCompensationFundsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListFamilyCompensationFunds")
            .RequirePermission(new AlgunPermiso("Payroll.Entities.View", "Payroll.Employees.View", "Payroll.Concepts.View"));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetFamilyCompensationFundByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetFamilyCompensationFundById")
            .RequirePermission(new AlgunPermiso("Payroll.Entities.View", "Payroll.Employees.View", "Payroll.Concepts.View"));

        group.MapPost("/", async (CreateFamilyCompensationFundCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/family-compensation-funds/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateFamilyCompensationFund")
            .RequirePermission("Payroll.Entities.Manage");

        group.MapPut("/{id:guid}", async (Guid id, UpdateFamilyCompensationFundCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateFamilyCompensationFund")
            .RequirePermission("Payroll.Entities.Manage");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteFamilyCompensationFundCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteFamilyCompensationFund")
            .RequirePermission("Payroll.Entities.Manage");
    }
}
