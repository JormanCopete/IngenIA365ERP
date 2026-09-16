using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Accounting.Documents;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>Comprobantes (feature 009, contracts/api.md §6): lista, detalle, borradores, validación, contabilizar, reversar, imprimir, por origen.</summary>
public class DocumentsEndpoints : ICarterModule
{
    /// <summary>Cuerpo de <c>POST/PUT /drafts</c> y <c>POST /validate</c>.</summary>
    public sealed record BorradorRequest(string VoucherTypeCode, DateOnly Date, string Description, IReadOnlyList<LineaDeBorradorInput> Lines);

    public sealed record ReversionRequest(string Reason, DateOnly? Date);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/documents")
            .WithTags("AccountingDocuments")
            .RequireAuthorization();

        group.MapGet("/", async ([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? voucherType, [FromQuery] string? status,
                [FromQuery] string? origin, [FromQuery] long? number, [FromQuery] int? page, [FromQuery] int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDocumentsQuery(from, to, voucherType, status, origin, number, page ?? 1, pageSize ?? 50), ct))
            .WithName("Accounting_Documents_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.View");

        group.MapGet("/by-source", async ([FromQuery] string module, [FromQuery] Guid sourcePublicId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDocumentBySourceQuery(module ?? string.Empty, sourcePublicId), ct))
            .WithName("Accounting_Documents_BySource")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.View");

        group.MapGet("/my-drafts", async (ISender sender, CancellationToken ct) => await sender.Send(new GetMyDraftsQuery(), ct))
            .WithName("Accounting_Documents_MyDrafts")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.Create");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetDocumentQuery(id), ct))
            .WithName("Accounting_Documents_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.View");

        group.MapGet("/{id:guid}/print", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new PrintDocumentQuery(id), ct);
                return result.IsSuccess ? Results.File(result.Value.Content, "application/pdf", result.Value.FileName) : (object)result;
            })
            .WithName("Accounting_Documents_Print")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.View");

        group.MapPost("/drafts", async (BorradorRequest body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveDraftDocumentCommand(null, body.VoucherTypeCode, body.Date, body.Description ?? string.Empty, body.Lines ?? []), ct);
                return result.IsSuccess ? (object)Results.Created($"/api/accounting/documents/{result.Value.PublicId}", result.Value) : result;
            })
            .WithName("Accounting_Documents_CreateDraft")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.Create");

        group.MapPut("/drafts/{id:guid}", async (Guid id, BorradorRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SaveDraftDocumentCommand(id, body.VoucherTypeCode, body.Date, body.Description ?? string.Empty, body.Lines ?? []), ct))
            .WithName("Accounting_Documents_UpdateDraft")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.Create");

        group.MapDelete("/drafts/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new DiscardDraftCommand(id), ct))
            .WithName("Accounting_Documents_DiscardDraft")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.Create");

        group.MapPost("/validate", async (BorradorRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ValidateDraftQuery(body.VoucherTypeCode, body.Date, body.Description ?? string.Empty, body.Lines ?? []), ct))
            .WithName("Accounting_Documents_Validate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.Create");

        group.MapPost("/{id:guid}/post", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new PostDocumentCommand(id), ct))
            .WithName("Accounting_Documents_Post")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.Post");

        group.MapPost("/{id:guid}/reverse", async (Guid id, ReversionRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReverseDocumentCommand(id, body.Reason ?? string.Empty, body.Date), ct))
            .WithName("Accounting_Documents_Reverse")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Vouchers.Void");
    }
}
