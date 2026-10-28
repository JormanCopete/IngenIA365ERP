using IngenIA365ERP.Application.Common.Personas;
using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.People.Queries;
using IngenIA365ERP.Application.Core.People.Services;
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

        // Feature 014 (decisión del dueño del 2026-10-18, D20): la ficha muestra sólo las cuentas donde la persona es titular; las de cotitular se
        // abren aparte con este botón.
        group.MapGet("/{id:guid}/co-held-accounts", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonCoHeldAccountsQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPersonCoHeldAccounts").RequirePermission("Core.People.View");

        // Feature 008: `rol` filtra por bandera (associate, employee, salesperson, customer,
        // supplier, advisor, thirdparty) y la fila trae las ocho banderas.
        group.MapGet("/search", async (string? q, string? rol, ISender sender) =>
        {
            var result = await sender.Send(new SearchPeopleQuery(q ?? "", rol));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("SearchPeople").RequirePermission("Core.People.View");

        // Feature 008: la persona dueña de un documento, eliminadas incluidas. Por query string
        // y no por ruta: el log de peticiones registra la ruta, no la query. Feature 017: `type`
        // (opcional) distingue CC y CE del mismo número; sin él y con varias, 409 pidiendo el tipo.
        group.MapGet("/by-document", async (string? taxId, string? type, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonByDocumentQuery(taxId ?? "", type));
            if (result.IsSuccess) return Results.Ok(result.Value);
            return result.Error.Code == ResolutorDePersonaPorDocumento.CodigoAmbiguo
                ? Results.Conflict(result.Error)
                : Results.NotFound(result.Error);
        }).WithName("GetPersonByDocument").RequirePermission("Core.People.View");
    }
}
