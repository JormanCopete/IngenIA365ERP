using Carter;
using IngenIA365ERP.Application.Core.Agreements.Commands.CreateAgreement;
using IngenIA365ERP.Application.Core.Agreements.Commands.UpdateAgreement;
using IngenIA365ERP.Application.Core.Agreements.Commands.DeleteAgreement;
using IngenIA365ERP.Application.Core.Agreements.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class AgreementsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/agreements")
            .WithTags("Agreements")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListAgreementsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListAgreements");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAgreementByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetAgreementById");

        group.MapPost("/", async (CreateAgreementCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/agreements/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateAgreement");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAgreementCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateAgreement");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteAgreementCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteAgreement");
    }
}
