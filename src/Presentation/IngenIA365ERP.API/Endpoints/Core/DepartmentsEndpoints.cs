using Carter;
using IngenIA365ERP.Application.Core.Departments.Commands.CreateDepartment;
using IngenIA365ERP.Application.Core.Departments.Commands.DeleteDepartment;
using IngenIA365ERP.Application.Core.Departments.Commands.UpdateDepartment;
using IngenIA365ERP.Application.Core.Departments.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class DepartmentsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/departments")
            .WithTags("Departments")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListDepartmentsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListDepartments");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetDepartmentByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetDepartmentById");

        group.MapPost("/", async (CreateDepartmentCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/departments/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateDepartment");

        group.MapPut("/{id:guid}", async (Guid id, UpdateDepartmentCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("UpdateDepartment");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteDepartmentCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("DeleteDepartment");
    }
}
