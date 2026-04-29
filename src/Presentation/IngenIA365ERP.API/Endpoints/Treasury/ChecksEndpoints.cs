using Carter;
using IngenIA365ERP.Application.Treasury.Checks.Commands.ProcessCheck;
using IngenIA365ERP.Application.Treasury.Checks.Commands.RegisterCheck;
using IngenIA365ERP.Application.Treasury.Checks.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

public class ChecksEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/treasury/checks")
            .WithTags("Checks")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListChecksQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListChecks");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCheckByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCheckById");

        group.MapPost("/", async (RegisterCheckCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/treasury/checks/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("RegisterCheck");

        group.MapPost("/{id:guid}/process", async (Guid id, ProcessCheckRequest request, ISender sender) =>
        {
            var command = new ProcessCheckCommand
            {
                CheckPublicId = id,
                Action = request.Action
            };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("ProcessCheck");
    }
}

// Request DTO
public record ProcessCheckRequest(string Action);
