using Carter;
using IngenIA365ERP.Application.Lending.SavingsInterest.Commands.LiquidateSavingsInterest;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class SavingsInterestEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/savings-interest-liquidation")
            .WithTags("SavingsInterestLiquidation")
            .RequireAuthorization();

        group.MapPost("/", async (LiquidateSavingsInterestCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("LiquidateSavingsInterest");

        group.MapGet("/preview", async (DateOnly liquidationDate, ISender sender) =>
        {
            var result = await sender.Send(new GetSavingsLiquidationPreviewQuery(liquidationDate));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetSavingsLiquidationPreview");
    }
}
