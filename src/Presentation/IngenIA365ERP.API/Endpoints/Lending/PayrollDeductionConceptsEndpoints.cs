using Carter;
using IngenIA365ERP.Application.Lending.PayrollDeductionConcepts.Commands.CreatePayrollDeductionConcept;
using IngenIA365ERP.Application.Lending.PayrollDeductionConcepts.Commands.UpdatePayrollDeductionConcept;
using IngenIA365ERP.Application.Lending.PayrollDeductionConcepts.Commands.DeletePayrollDeductionConcept;
using IngenIA365ERP.Application.Lending.PayrollDeductionConcepts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class PayrollDeductionConceptsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/deduction-concepts")
            .WithTags("PayrollDeductionConcepts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPayrollDeductionConceptsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPayrollDeductionConcepts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPayrollDeductionConceptByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPayrollDeductionConceptById");

        group.MapPost("/", async (CreatePayrollDeductionConceptCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/deduction-concepts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePayrollDeductionConcept");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePayrollDeductionConceptCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePayrollDeductionConcept");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePayrollDeductionConceptCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePayrollDeductionConcept");
    }
}
