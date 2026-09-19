using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.People.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class PeopleDetailEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/people")
            .WithTags("People")
            .RequireAuthorization();

        group.MapGet("/{id:guid}/detail", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonDetailQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPersonDetail").RequirePermission("Core.People.View");

        // Feature 008: `rol` filtra por bandera (associate, employee, salesperson, customer,
        // supplier, advisor, thirdparty) y la fila trae las ocho banderas.
        group.MapGet("/search", async (string? q, string? rol, ISender sender) =>
        {
            var result = await sender.Send(new SearchPeopleQuery(q ?? "", rol));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("SearchPeople").RequirePermission("Core.People.View");

        // Feature 008: la persona dueña de un documento, eliminadas incluidas. Por query string
        // y no por ruta: el log de peticiones registra la ruta, no la query.
        group.MapGet("/by-document", async (string? taxId, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonByDocumentQuery(taxId ?? ""));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPersonByDocument").RequirePermission("Core.People.View");
    }
}
