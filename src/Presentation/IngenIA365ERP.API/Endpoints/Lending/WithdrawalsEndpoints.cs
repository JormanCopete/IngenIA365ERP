using Carter;
using IngenIA365ERP.Application.Lending.Withdrawals.Commands.ProcessWithdrawal;
using IngenIA365ERP.Application.Lending.Withdrawals.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class WithdrawalsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/withdrawals")
            .WithTags("Withdrawals")
            .RequireAuthorization();

        group.MapGet("/preview/{personId:guid}", async (Guid personId, ISender sender) =>
        {
            var result = await sender.Send(new GetWithdrawalPreviewQuery(personId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetWithdrawalPreview");

        group.MapPost("/", async (ProcessWithdrawalCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/withdrawals/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("ProcessWithdrawal");

        group.MapGet("/", async ([AsParameters] ListWithdrawalsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListWithdrawals");
    }
}
