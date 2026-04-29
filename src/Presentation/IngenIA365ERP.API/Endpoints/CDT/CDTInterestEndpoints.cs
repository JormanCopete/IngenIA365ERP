using Carter;
using IngenIA365ERP.Application.CDT.InterestLiquidation.Commands.LiquidateCDTInterest;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.CDT;

public class CDTInterestEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cdt/interest-liquidation")
            .WithTags("CDTInterestLiquidation")
            .RequireAuthorization();

        group.MapPost("/", async (LiquidateCDTInterestCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("LiquidateCDTInterest");

        group.MapGet("/preview", async (DateOnly liquidationDate, ISender sender) =>
        {
            var result = await sender.Send(new GetCDTLiquidationPreviewQuery(liquidationDate));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetCDTLiquidationPreview");
    }
}
