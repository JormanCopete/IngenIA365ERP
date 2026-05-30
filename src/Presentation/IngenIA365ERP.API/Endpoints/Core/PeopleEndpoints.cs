using Carter;
using IngenIA365ERP.Application.Core.People.Commands.CreatePerson;
using IngenIA365ERP.Application.Core.People.Commands.DeletePerson;
using IngenIA365ERP.Application.Core.People.Commands.UpdatePerson;
using IngenIA365ERP.Application.Core.People.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// CRUD principal del modulo Maestros - Personas (/api/core/people).
/// La busqueda libre (autocomplete) y el detalle expandido (con cartera, asociado, etc.)
/// estan en <see cref="PeopleDetailEndpoints"/>.
/// </summary>
public class PeopleEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/people")
            .WithTags("People")
            .RequireAuthorization();

        // GET /api/core/people  -> listado paginado (con filtros por rol)
        group.MapGet("/", async ([AsParameters] ListPeopleQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPeople");

        // GET /api/core/people/{id}  -> datos basicos para edicion
        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPersonById");

        // POST /api/core/people  -> crear persona
        group.MapPost("/", async (CreatePersonCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/people/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePerson");

        // PUT /api/core/people/{id}  -> actualizar persona
        group.MapPut("/{id:guid}", async (Guid id, UpdatePersonCommand command, ISender sender) =>
        {
            // Toma el id de la ruta como fuente de verdad.
            if (command.PublicId != id) command = command with { PublicId = id };

            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("UpdatePerson");

        // DELETE /api/core/people/{id}  -> soft-delete
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePersonCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("DeletePerson");
    }
}
