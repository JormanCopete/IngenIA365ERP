using Carter;
using IngenIA365ERP.Application.Lending.Statements.Commands.GenerateStatements;
using IngenIA365ERP.Application.Lending.Statements.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class StatementsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/statements")
            .WithTags("LendingStatements")
            .RequireAuthorization();

        group.MapPost("/generate/{year:int}/{month:int}", async (int year, int month, ISender sender) =>
        {
            var result = await sender.Send(new GenerateStatementsCommand(year, month));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GenerateStatements");

        group.MapGet("/{personId:guid}/{year:int}/{month:int}", async (Guid personId, int year, int month, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonStatementQuery(personId, year, month));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPersonStatement");
    }
}
