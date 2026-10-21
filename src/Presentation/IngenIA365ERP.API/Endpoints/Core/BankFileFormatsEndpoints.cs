using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Core.BankFiles;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// Feature 010 (N4, D-42): formatos de archivo plano de los bancos, en Core y ligados al banco,
/// para que nómina, tesorería y contabilidad generen sus archivos con el mismo motor. Sólo
/// reenvía al <c>ISender</c>.
/// </summary>
public sealed class BankFileFormatsEndpoints : ICarterModule
{
    /// <summary>El tamaño máximo del archivo de prueba de la vista previa de lectura.</summary>
    public const int TamanoMaximoDePrueba = 2 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/bank-file-formats")
            .WithTags("BankFileFormats")
            .RequireAuthorization();

        group.MapGet("/", async (string? scope, Guid? bankId, bool? onlyActive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListBankFileFormatsQuery(scope, bankId, onlyActive ?? false), ct))
            .WithName("Core_BankFileFormats_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.View");

        group.MapGet("/sources", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new ListBankFieldSourcesQuery(), ct))
            .WithName("Core_BankFileFormats_Sources")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetBankFileFormatQuery(id), ct))
            .WithName("Core_BankFileFormats_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.View");

        // Feature 013, K2 (T0832): lee un archivo de prueba con el formato (FlatFileReader), sin guardar nada: la vista previa del formato de vuelta.
        group.MapPost("/{id:guid}/read-preview", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                if (!http.Request.HasFormContentType)
                    return Results.Json(new { code = "Validation.Invalid", message = "Adjunte el archivo de prueba.", traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status400BadRequest);
                var formulario = await http.Request.ReadFormAsync(ct);
                var archivo = formulario.Files.FirstOrDefault();
                if (archivo is null || archivo.Length == 0)
                    return Results.Json(new { code = "Validation.Invalid", message = "Adjunte el archivo de prueba.", traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status400BadRequest);
                if (archivo.Length > TamanoMaximoDePrueba)
                    return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo de prueba pesa más de 2 MB.", traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms, ct);
                return (object)await sender.Send(new ReadBankFileSampleQuery(id, ms.ToArray()), ct);
            })
            .WithName("Core_BankFileFormats_ReadPreview")
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(TamanoMaximoDePrueba + 64 * 1024))
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.View");

        group.MapPost("/", async (BankFileFormatDefinition definition, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateBankFileFormatCommand(definition), ct);
                return result.IsSuccess
                    ? Results.Created($"/api/core/bank-file-formats/{result.Value}", new { formatPublicId = result.Value })
                    : (object)result;
            })
            .WithName("Core_BankFileFormats_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.Manage");

        group.MapPut("/{id:guid}", async (Guid id, BankFileFormatDefinition definition, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateBankFileFormatCommand(id, definition), ct))
            .WithName("Core_BankFileFormats_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.Manage");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteBankFileFormatCommand(id), ct))
            .WithName("Core_BankFileFormats_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Core.BankFileFormats.Manage");
    }
}
