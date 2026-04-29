using Carter;
using IngenIA365ERP.Application.Accounting.JournalEntries.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class JournalEntriesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/journal-entries")
            .WithTags("JournalEntries")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListJournalEntriesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListJournalEntries");
    }
}
