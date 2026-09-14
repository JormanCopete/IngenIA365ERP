using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.People.Commands.CreatePerson;
using IngenIA365ERP.Application.Core.People.Commands.DeletePerson;
using IngenIA365ERP.Application.Core.People.Commands.RestorePerson;
using IngenIA365ERP.Application.Core.People.Commands.UpdatePerson;
using IngenIA365ERP.Application.Core.People.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// CRUD principal del modulo Maestros - Personas (/api/core/people).
/// La busqueda libre (autocomplete) y el detalle expandido (con cartera, asociado, etc.)
/// estan en <see cref="PeopleDetailEndpoints"/>.
///
/// <para>
/// Feature 008: cada ruta exige su permiso <c>Core.People.*</c>; sin él la respuesta es el
/// 404 indistinguible de <c>PermissionAuthorizationFilter</c>. Hasta el 2026-09-13 bastaba
/// con estar autenticado. La prueba de arquitectura <c>LosMaestrosDePersonaExigenPermiso</c>
/// lo vigila.
/// </para>
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
        }).WithName("ListPeople").RequirePermission("Core.People.View");

        // GET /api/core/people/{id}  -> datos basicos para edicion
        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPersonById").RequirePermission("Core.People.View");

        // POST /api/core/people  -> crear persona
        group.MapPost("/", async (CreatePersonCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/people/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePerson").RequirePermission("Core.People.Create");

        // PUT /api/core/people/{id}  -> actualizar persona
        group.MapPut("/{id:guid}", async (Guid id, UpdatePersonCommand command, ISender sender) =>
        {
            // Toma el id de la ruta como fuente de verdad.
            if (command.PublicId != id) command = command with { PublicId = id };

            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("UpdatePerson").RequirePermission("Core.People.Update");

        // DELETE /api/core/people/{id}  -> soft-delete
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePersonCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("DeletePerson").RequirePermission("Core.People.Delete");

        // POST /api/core/people/{id}/restore -> reactiva una eliminada (misma fila). Feature 008:
        // misma potestad que eliminar; el envelope canónico para que la pantalla lea el código.
        group.MapPost("/{id:guid}/restore", async (Guid id, ISender sender) =>
            await sender.Send(new RestorePersonCommand(id)))
                        .WithName("RestorePerson").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Core.People.Delete");
    }
}
