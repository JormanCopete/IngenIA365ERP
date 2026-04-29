using Carter;
using IngenIA365ERP.Application.Lending.Accruals.Commands.AccrueInterest;
using IngenIA365ERP.Application.Lending.Accruals.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class AccrualsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/accruals")
            .WithTags("LendingAccruals")
            .RequireAuthorization();

        group.MapPost("/", async (AccrueInterestCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("AccrueInterest");

        group.MapGet("/preview", async (DateOnly accrualDate, Guid? creditLinePublicId, ISender sender) =>
        {
            var result = await sender.Send(new GetAccrualPreviewQuery(accrualDate, creditLinePublicId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetAccrualPreview");
    }
}
