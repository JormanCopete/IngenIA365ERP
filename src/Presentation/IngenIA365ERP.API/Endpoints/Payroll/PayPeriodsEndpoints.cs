using Carter;
using IngenIA365ERP.Application.Payroll.PayPeriods.Commands.CreatePayPeriod;
using IngenIA365ERP.Application.Payroll.PayPeriods.Commands.UpdatePayPeriod;
using IngenIA365ERP.Application.Payroll.PayPeriods.Commands.DeletePayPeriod;
using IngenIA365ERP.Application.Payroll.PayPeriods.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class PayPeriodsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/pay-periods")
            .WithTags("PayPeriods")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPayPeriodsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPayPeriods");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPayPeriodByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPayPeriodById");

        group.MapPost("/", async (CreatePayPeriodCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/pay-periods/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePayPeriod");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePayPeriodCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePayPeriod");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePayPeriodCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePayPeriod");
    }
}
