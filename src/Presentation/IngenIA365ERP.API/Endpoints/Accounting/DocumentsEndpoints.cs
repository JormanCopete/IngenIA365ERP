using Carter;
using IngenIA365ERP.Application.Accounting.Documents.Commands.CreateDocument;
using IngenIA365ERP.Application.Accounting.Documents.Commands.PostDocument;
using IngenIA365ERP.Application.Accounting.Documents.Commands.VoidDocument;
using IngenIA365ERP.Application.Accounting.Documents.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class DocumentsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/documents")
            .WithTags("AccountingDocuments")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListDocumentsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListDocuments");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetDocumentByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetDocumentById");

        group.MapPost("/", async (CreateDocumentCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/documents/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateDocument");

        group.MapPost("/{id:guid}/void", async (Guid id, VoidDocumentRequest? request, ISender sender) =>
        {
            var command = new VoidDocumentCommand(id, request?.Reason);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("VoidDocument");

        group.MapPost("/{id:guid}/post", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new PostDocumentCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("PostDocument");
    }
}

public record VoidDocumentRequest(string? Reason);
