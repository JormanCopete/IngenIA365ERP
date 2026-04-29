using Carter;
using IngenIA365ERP.Application.Payroll.WithholdingCauses.Commands.CreateWithholdingCause;
using IngenIA365ERP.Application.Payroll.WithholdingCauses.Commands.UpdateWithholdingCause;
using IngenIA365ERP.Application.Payroll.WithholdingCauses.Commands.DeleteWithholdingCause;
using IngenIA365ERP.Application.Payroll.WithholdingCauses.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class WithholdingCausesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/withholding-causes")
            .WithTags("WithholdingCauses")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListWithholdingCausesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListWithholdingCauses");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetWithholdingCauseByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetWithholdingCauseById");

        group.MapPost("/", async (CreateWithholdingCauseCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/withholding-causes/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateWithholdingCause");

        group.MapPut("/{id:guid}", async (Guid id, UpdateWithholdingCauseCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateWithholdingCause");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteWithholdingCauseCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteWithholdingCause");
    }
}
