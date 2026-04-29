using Carter;
using IngenIA365ERP.Application.Accounting.AccountBalances.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class AccountBalancesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/balances")
            .WithTags("AccountBalances")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] GetAccountBalancesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetAccountBalances");
    }
}
