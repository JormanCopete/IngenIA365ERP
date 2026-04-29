using Carter;
using IngenIA365ERP.Application.Debit.DebitDailyParameters.Commands.CreateDebitDailyParameter;
using IngenIA365ERP.Application.Debit.DebitDailyParameters.Commands.UpdateDebitDailyParameter;
using IngenIA365ERP.Application.Debit.DebitDailyParameters.Commands.DeleteDebitDailyParameter;
using IngenIA365ERP.Application.Debit.DebitDailyParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Debit;

public class DebitDailyParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/debit/daily-parameters")
            .WithTags("DebitDailyParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListDebitDailyParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListDebitDailyParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetDebitDailyParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetDebitDailyParameterById");

        group.MapPost("/", async (CreateDebitDailyParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/debit/daily-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateDebitDailyParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateDebitDailyParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateDebitDailyParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteDebitDailyParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteDebitDailyParameter");
    }
}
