using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.DeleteAttachment;
using IngenIA365ERP.Application.Attachments.DownloadAttachment;
using IngenIA365ERP.Application.Attachments.ListAttachments;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T109 — Endpoints REST de adjuntos cifrados (US5).
///
/// <para>
/// El upload va por <c>multipart/form-data</c> con dos partes: el archivo
/// y los metadatos de owner como form-fields. El download devuelve el
/// payload descifrado con <c>Results.File</c> + filename original. El
/// listado por owner es JSON paginado pequeño (≤ 50 items en práctica).
/// </para>
/// </summary>
public sealed class AttachmentsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attachments")
            .WithTags("Attachments")
            .RequireAuthorization();

        group.MapPost("/", UploadAsync)
            .WithName("Attachments_Upload")
            .DisableAntiforgery()
            .RequirePermission("Attachments.Upload");

        group.MapGet("/{publicId:guid}", DownloadAsync)
            .WithName("Attachments_Download")
            .RequirePermission("Attachments.Download");

        group.MapDelete("/{publicId:guid}", DeleteAsync)
            .WithName("Attachments_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Attachments.Delete");

        group.MapGet("/by-owner", ListByOwnerAsync)
            .WithName("Attachments_ListByOwner")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Attachments.Download");
    }

    private static async Task<IResult> UploadAsync(
        [FromForm] IFormFile file,
        [FromForm] string ownerEntityType,
        [FromForm] Guid ownerEntityPublicId,
        ISender sender,
        HttpContext http,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Results.Json(
                new { code = AttachmentErrorCodes.Validation_FileEmpty,
                      message = "No se recibió un archivo.",
                      traceId = http.TraceIdentifier },
                statusCode: StatusCodes.Status400BadRequest);
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var result = await sender.Send(new UploadAttachmentCommand(
            OwnerEntityType: ownerEntityType,
            OwnerEntityPublicId: ownerEntityPublicId,
            FileName: file.FileName,
            ContentType: file.ContentType,
            Content: bytes), ct);

        return (IResult)ErrorEnvelopeFilter.Translate(http, result)!;
    }

    private static async Task<IResult> DownloadAsync(
        Guid publicId,
        ISender sender,
        HttpContext http,
        CancellationToken ct)
    {
        var result = await sender.Send(new DownloadAttachmentQuery(publicId), ct);
        if (result.IsFailure)
        {
            return (IResult)ErrorEnvelopeFilter.Translate(http, result)!;
        }

        var download = result.Value;
        http.Response.Headers["X-Attachment-Sha256"] = download.Sha256Hex;
        return Results.File(download.Content, download.ContentType, download.FileName);
    }

    private static async Task<object?> DeleteAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new DeleteAttachmentCommand(publicId), ct);

    private static async Task<object?> ListByOwnerAsync(
        [AsParameters] AttachmentOwnerQueryParams q,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ListAttachmentsByOwnerQuery(
            q.OwnerEntityType, q.OwnerEntityPublicId), ct);
}

public sealed record AttachmentOwnerQueryParams(
    string OwnerEntityType,
    Guid OwnerEntityPublicId);
