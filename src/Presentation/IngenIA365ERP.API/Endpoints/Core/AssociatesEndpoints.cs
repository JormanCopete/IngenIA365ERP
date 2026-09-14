using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociate;
using IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociateWithPerson;
using IngenIA365ERP.Application.Core.Associates.Commands.UpdateAssociate;
using IngenIA365ERP.Application.Core.Associates.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class AssociatesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/associates")
            .WithTags("Associates")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListAssociatesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListAssociates").RequirePermission("Core.Associates.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAssociateByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetAssociateById").RequirePermission("Core.Associates.View");

        // GET asociado por PersonId — util cuando ya tienes la persona y quieres
        // saber si tiene rol asociado.
        group.MapGet("/by-person/{personId:guid}", async (Guid personId, ISender sender) =>
        {
            var result = await sender.Send(new GetAssociateByPersonIdQuery(personId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetAssociateByPersonId").RequirePermission("Core.Associates.View");

        group.MapPost("/", async (RegisterAssociateCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/associates/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("RegisterAssociate").RequirePermission("Core.Associates.Create");

        // Feature 008 (US2): persona nueva y asociado en un solo paso, atómico. Exige crear el
        // asociado Y la persona (dos RequirePermission encadenados = AND); con uno solo, 404.
        group.MapPost("/with-person", async (RegisterAssociateWithPersonCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            // Éxito: 201 con la ubicación del asociado. Fallo: el Result tal cual, para que
            // ErrorEnvelopeFilter lo traduzca al envelope canónico (422 con código).
            return result.IsSuccess
                ? (object)Results.Created($"/api/core/associates/{result.Value.AssociatePublicId}", result.Value)
                : result;
        }).WithName("RegisterAssociateWithPerson")
          .AddEndpointFilter<ErrorEnvelopeFilter>()
          .RequirePermission("Core.Associates.Create")
          .RequirePermission("Core.People.Create");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAssociateCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("UpdateAssociate").RequirePermission("Core.Associates.Update");
    }
}
