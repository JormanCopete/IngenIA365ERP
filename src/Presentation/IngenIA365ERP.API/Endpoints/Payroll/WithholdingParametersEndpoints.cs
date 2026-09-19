using Carter;
using IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.CreateWithholdingParameter;
using IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.UpdateWithholdingParameter;
using IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.DeleteWithholdingParameter;
using IngenIA365ERP.Application.Payroll.WithholdingParameters.Queries;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class WithholdingParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/withholding-parameters")
            .WithTags("WithholdingParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListWithholdingParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListWithholdingParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetWithholdingParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetWithholdingParameterById");

        group.MapPost("/", async (CreateWithholdingParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/withholding-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateWithholdingParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateWithholdingParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            // Un tramo que se cruza o un plan inexistente no es un 404: es un 400 con el motivo.
            return result.IsSuccess ? Results.NoContent()
                : result.Error.Code == Error.NotFound.Code ? Results.NotFound(result.Error) : Results.BadRequest(result.Error);
        }).WithName("UpdateWithholdingParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteWithholdingParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteWithholdingParameter");
    }
}
