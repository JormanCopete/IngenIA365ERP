using Carter;
using IngenIA365ERP.API.Endpoints.Attachments;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.DeleteAttachment;
using IngenIA365ERP.Application.Attachments.DownloadAttachment;
using IngenIA365ERP.Application.Attachments.ListAttachments;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T109 — Endpoints REST de adjuntos cifrados (US5).
///
/// <para>
/// El download devuelve el payload descifrado con <c>Results.File</c> + filename original. El
/// listado por owner es JSON paginado pequeño (≤ 50 items en práctica).
/// </para>
///
/// <para>
/// Feature 011: la subida multipart a través de la API (<c>POST /api/attachments</c>) se retiró sin
/// alias el 2026-09-23. Ninguna pantalla la usaba —su único cliente era el componente huérfano
/// <c>AttachmentUploader</c>— y dejaba colgar un archivo de cualquier dueño, incluida la PILA de otro.
/// Las personas suben directo al almacén con una autorización firmada (contracts/api.md §1–§3); los
/// módulos siguen guardando sus archivos por <c>UploadAttachmentCommand</c>, que no tiene ruta. Todo
/// el grupo lleva el limitador de concurrencia <see cref="LimiteDeAdjuntos"/>.
/// </para>
/// </summary>
public sealed class AttachmentsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attachments")
            .WithTags("Attachments")
            .RequireAuthorization()
            .RequireRateLimiting(LimiteDeAdjuntos.Politica);

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
