using Carter;

namespace IngenIA365ERP.API.Endpoints.Core;

public class PeopleEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/people").WithTags("People");

        group.MapGet("/", async () =>
        {
            // TODO: Wire up MediatR query: new GetPeopleQuery()
            return Results.Ok(new[]
            {
                new { PublicId = Guid.NewGuid(), FirstName = "Juan", LastName = "Perez", DocumentNumber = "1234567890" },
                new { PublicId = Guid.NewGuid(), FirstName = "Maria", LastName = "Garcia", DocumentNumber = "0987654321" }
            });
        })
        .WithName("GetPeople")
        .Produces(200);

        group.MapGet("/{publicId:guid}", async (Guid publicId) =>
        {
            // TODO: Wire up MediatR query: new GetPersonByIdQuery(publicId)
            return Results.Ok(new
            {
                PublicId = publicId,
                FirstName = "Juan",
                LastName = "Perez",
                DocumentNumber = "1234567890",
                IsAssociate = true,
                IsEmployee = false
            });
        })
        .WithName("GetPersonById")
        .Produces(200)
        .Produces(404);

        group.MapPost("/", async (CreatePersonRequest request) =>
        {
            // TODO: Wire up MediatR command: new CreatePersonCommand(request)
            var publicId = Guid.NewGuid();
            return Results.Created($"/api/people/{publicId}", new { PublicId = publicId });
        })
        .WithName("CreatePerson")
        .Produces(201)
        .Produces(400);

        group.MapPut("/{publicId:guid}", async (Guid publicId, UpdatePersonRequest request) =>
        {
            // TODO: Wire up MediatR command: new UpdatePersonCommand(publicId, request)
            return Results.NoContent();
        })
        .WithName("UpdatePerson")
        .Produces(204)
        .Produces(400)
        .Produces(404);

        group.MapDelete("/{publicId:guid}", async (Guid publicId) =>
        {
            // TODO: Wire up MediatR command: new DeletePersonCommand(publicId)
            return Results.NoContent();
        })
        .WithName("DeletePerson")
        .Produces(204)
        .Produces(404);
    }
}

// Placeholder DTOs - will be moved to Application layer
public record CreatePersonRequest(
    string FirstName,
    string LastName,
    string DocumentType,
    string DocumentNumber,
    bool IsAssociate = false,
    bool IsEmployee = false);

public record UpdatePersonRequest(
    string FirstName,
    string LastName,
    string DocumentType,
    string DocumentNumber,
    bool IsAssociate = false,
    bool IsEmployee = false);
