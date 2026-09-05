using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.PayPeriods.Commands.CreatePayPeriod;
using IngenIA365ERP.Application.Payroll.PayPeriods.Commands.UpdatePayPeriod;
using IngenIA365ERP.Application.Payroll.PayPeriods.Commands.DeletePayPeriod;
using IngenIA365ERP.Application.Payroll.PayPeriods.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>
/// Períodos de pago (feature 005, contracts/api.md §2). Permisos: quien ve la liquidación
/// ve los períodos (<c>Payroll.Runs.View</c>); quien calcula los crea y edita
/// (<c>Payroll.Runs.Calculate</c>). Los códigos heredados <c>Payroll.PayrollPeriods.*</c>
/// no están en el catálogo sembrado y por eso no se exigen.
/// </summary>
public class PayPeriodsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/pay-periods")
            .WithTags("PayPeriods")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPayPeriodsQuery query, ISender sender) =>
        {
            return await sender.Send(query);
        }).WithName("ListPayPeriods").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Runs.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            return await sender.Send(new GetPayPeriodByIdQuery(id));
        }).WithName("GetPayPeriodById").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Runs.View");

        group.MapPost("/", async (CreatePayPeriodCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/pay-periods/{result.Value}", result.Value)
                : (object)result;
        }).WithName("CreatePayPeriod").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Runs.Calculate");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePayPeriodCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            return await sender.Send(command);
        }).WithName("UpdatePayPeriod").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Runs.Calculate");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            return await sender.Send(new DeletePayPeriodCommand(id));
        }).WithName("DeletePayPeriod").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Runs.Calculate");
    }
}
