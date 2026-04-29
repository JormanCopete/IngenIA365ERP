using Carter;
using IngenIA365ERP.Application.Debit.DebitAgreementParameters.Commands.CreateDebitAgreementParameter;
using IngenIA365ERP.Application.Debit.DebitAgreementParameters.Commands.UpdateDebitAgreementParameter;
using IngenIA365ERP.Application.Debit.DebitAgreementParameters.Commands.DeleteDebitAgreementParameter;
using IngenIA365ERP.Application.Debit.DebitAgreementParameters.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Debit;

public class DebitAgreementParametersEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/debit/agreement-parameters")
            .WithTags("DebitAgreementParameters")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListDebitAgreementParametersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListDebitAgreementParameters");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetDebitAgreementParameterByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetDebitAgreementParameterById");

        group.MapPost("/", async (CreateDebitAgreementParameterCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/debit/agreement-parameters/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateDebitAgreementParameter");

        group.MapPut("/{id:guid}", async (Guid id, UpdateDebitAgreementParameterCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateDebitAgreementParameter");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteDebitAgreementParameterCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteDebitAgreementParameter");
    }
}
