using Carter;
using IngenIA365ERP.Application.Payroll.PayrollConcepts.Commands.CreatePayrollConcept;
using IngenIA365ERP.Application.Payroll.PayrollConcepts.Commands.UpdatePayrollConcept;
using IngenIA365ERP.Application.Payroll.PayrollConcepts.Commands.DeletePayrollConcept;
using IngenIA365ERP.Application.Payroll.PayrollConcepts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class PayrollConceptsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/concepts")
            .WithTags("PayrollConcepts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPayrollConceptsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPayrollConcepts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPayrollConceptByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPayrollConceptById");

        group.MapPost("/", async (CreatePayrollConceptCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/concepts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePayrollConcept");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePayrollConceptCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePayrollConcept");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePayrollConceptCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePayrollConcept");
    }
}
