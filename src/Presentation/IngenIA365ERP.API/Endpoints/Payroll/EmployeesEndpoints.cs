using Carter;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployee;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.TerminateEmployee;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class EmployeesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/employees")
            .WithTags("PayrollEmployees")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListEmployeesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListEmployees");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetEmployeeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetEmployeeById");

        group.MapPost("/", async (RegisterEmployeeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/employees/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("RegisterEmployee");

        group.MapPost("/{id:guid}/terminate", async (Guid id, TerminateEmployeeRequest request, ISender sender) =>
        {
            var command = new TerminateEmployeeCommand(id, request.TerminationDate, request.TerminationCause);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("TerminateEmployee");
    }
}

public record TerminateEmployeeRequest(DateTime TerminationDate, string? TerminationCause);
