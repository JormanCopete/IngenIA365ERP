using Carter;
using IngenIA365ERP.Application.Core.Banks.Commands.CreateBank;
using IngenIA365ERP.Application.Core.Banks.Commands.UpdateBank;
using IngenIA365ERP.Application.Core.Banks.Commands.DeleteBank;
using IngenIA365ERP.Application.Core.Banks.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class BanksEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/banks")
            .WithTags("Banks")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListBanksQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListBanks");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetBankByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetBankById");

        group.MapPost("/", async (CreateBankCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/banks/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateBank");

        group.MapPut("/{id:guid}", async (Guid id, UpdateBankCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateBank");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteBankCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteBank");
    }
}
