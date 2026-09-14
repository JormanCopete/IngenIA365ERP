using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.EmployeeTax;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployee;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployeeWithPerson;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.TerminateEmployee;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.UpdateEmployee;
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
        }).WithName("ListEmployees").RequirePermission("Payroll.Employees.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetEmployeeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetEmployeeById").RequirePermission("Payroll.Employees.View");

        // Feature 005 (contracts/api.md §6): retención y plan del empleado. Los códigos
        // heredados Payroll.Employees.* no estan en el catalogo sembrado; se usan los de
        // novedades, que es quien mantiene los datos de nomina del empleado.
        group.MapGet("/{employeeId:guid}/withholding", async (Guid employeeId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetEmployeeWithholdingQuery(employeeId), ct))
            .WithName("Payroll_Employees_Withholding_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.View");

        group.MapPut("/{employeeId:guid}/withholding", async (Guid employeeId, SetEmployeeWithholdingCommand command, ISender sender, CancellationToken ct) =>
                await sender.Send(command with { EmployeePublicId = employeeId }, ct))
            .WithName("Payroll_Employees_Withholding_Set")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Novelties.Create");

        group.MapGet("/by-person/{personId:guid}", async (Guid personId, ISender sender) =>
        {
            var result = await sender.Send(new GetEmployeeByPersonIdQuery(personId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetEmployeeByPersonId").RequirePermission("Payroll.Employees.View");

        group.MapPost("/", async (RegisterEmployeeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/employees/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("RegisterEmployee").RequirePermission("Payroll.Employees.Create");

        // Feature 008 (US1): persona nueva y empleado en un solo paso, atómico. Exige crear el
        // empleado Y la persona (dos RequirePermission encadenados = AND); con uno solo, 404.
        group.MapPost("/with-person", async (RegisterEmployeeWithPersonCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            // Éxito: 201 con la ubicación de la ficha. Fallo: el Result tal cual, para que
            // ErrorEnvelopeFilter lo traduzca al envelope canónico (422 con código).
            return result.IsSuccess
                ? (object)Results.Created($"/api/payroll/employees/{result.Value.EmployeePublicId}", result.Value)
                : result;
        }).WithName("RegisterEmployeeWithPerson")
          .AddEndpointFilter<ErrorEnvelopeFilter>()
          .RequirePermission("Payroll.Employees.Create")
          .RequirePermission("Core.People.Create");

        group.MapPut("/{id:guid}", async (Guid id, UpdateEmployeeCommand command, ISender sender) =>
        {
            // Toma el id de la ruta como fuente de verdad (ignora cualquier valor en el body).
            if (command.EmployeePublicId != id) command = command with { EmployeePublicId = id };

            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("UpdateEmployee").RequirePermission("Payroll.Employees.Update");

        group.MapPost("/{id:guid}/terminate", async (Guid id, TerminateEmployeeRequest request, ISender sender) =>
        {
            var command = new TerminateEmployeeCommand(id, request.TerminationDate, request.TerminationCause);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("TerminateEmployee").RequirePermission("Payroll.Employees.Terminate");
    }
}

public record TerminateEmployeeRequest(DateTime TerminationDate, string? TerminationCause);
