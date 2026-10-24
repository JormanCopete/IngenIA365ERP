using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// La respuesta del banco, el resultado del giro y el rechazo técnico del archivo (feature 015, US11, T0161; contracts/api.md §10). Las escrituras llevan
/// <c>Idempotency-Key</c>; cada ruta sólo reenvía al <see cref="ISender"/>. La respuesta del banco llega por multipart (hasta 10 MB) porque se lee con su formato en el
/// servidor; no es una ruta de adjuntos. (nuevo)
/// </summary>
public class PaymentResultsEndpoints : ICarterModule
{
    public const string RegistrarResultados = "Treasury.Results.Register";
    public const string VerOrdenes = PaymentsEndpoints.VerOrdenes;
    public const string AnularArchivo = PaymentsEndpoints.AnularArchivo;
    public const long MaxBytes = 10 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var archivos = TreasuryGroup.Grupo(app, "/payment-files", "Tesorería Resultados");
        archivos.MapPost("/{id:guid}/responses", CargarAsync)
            .WithName("Treasury_PaymentFiles_LoadResponse").DisableAntiforgery().ConClaveDeOperacion().RequirePermission(RegistrarResultados);
        archivos.MapGet("/{id:guid}/responses", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new ListPaymentResponsesQuery(id), ct))
            .WithName("Treasury_PaymentFiles_Responses").RequirePermission(new AlgunPermiso(RegistrarResultados, VerOrdenes));
        archivos.MapGet("/{id:guid}/responses/{rid:guid}", async (Guid id, Guid rid, ISender s, CancellationToken ct) =>
                await s.Send(new GetPaymentResponseQuery(id, rid), ct))
            .WithName("Treasury_PaymentFiles_Response").RequirePermission(new AlgunPermiso(RegistrarResultados, VerOrdenes));
        archivos.MapPost("/{id:guid}/responses/{rid:guid}/lines/{lineId:guid}/review",
                async (Guid id, Guid rid, Guid lineId, RevisionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                    await s.Send(new ReviewPaymentResponseLineCommand(id, rid, lineId, body.Accept, body.Note ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentFiles_ReviewLine").ConClaveDeOperacion().RequirePermission(RegistrarResultados);
        archivos.MapPost("/{id:guid}/rejected-by-bank", async (Guid id, PaymentsEndpoints.MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkPaymentFileRejectedByBankCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentFiles_RejectedByBank").ConClaveDeOperacion().RequirePermission(AnularArchivo);

        var ordenes = TreasuryGroup.Grupo(app, "/payment-orders", "Tesorería Resultados");
        ordenes.MapGet("/{id:guid}/results", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new ListDisbursementResultsQuery(id), ct))
            .WithName("Treasury_PaymentOrders_Results").RequirePermission(new AlgunPermiso(RegistrarResultados, VerOrdenes));
        ordenes.MapPost("/{id:guid}/results", async (Guid id, ResultadoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterDisbursementResultCommand(id, body.Outcome ?? string.Empty, body.OccurredOn, body.TreasuryReference, body.Reason, body.CheckNumber,
                    body.SupportAttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentOrders_RegisterResult").ConClaveDeOperacion().RequirePermission(RegistrarResultados);
        ordenes.MapPost("/results/bulk", async (RegisterDisbursementResultsBulkCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_PaymentOrders_RegisterResultsBulk").ConClaveDeOperacion().RequirePermission(RegistrarResultados);
    }

    private static async Task<object?> CargarAsync(Guid id, [FromForm] IFormFile archivo, [FromForm] Guid? format, ISender sender, HttpContext http, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return Results.Json(new { code = "Archivo.Vacio", message = "No se recibió un archivo.", traceId = http.TraceIdentifier }, statusCode: StatusCodes.Status400BadRequest);
        if (archivo.Length > MaxBytes)
            return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 10 MB.", traceId = http.TraceIdentifier },
                statusCode: StatusCodes.Status413PayloadTooLarge);
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        return await sender.Send(new LoadPaymentResponseCommand(id, archivo.FileName, ms.ToArray(), format) { OperationKey = http.ClaveDeOperacion() }, ct);
    }

    public sealed record RevisionRequest(bool Accept, string? Note);

    public sealed record ResultadoRequest(string? Outcome, DateOnly OccurredOn, string? TreasuryReference, string? Reason, string? CheckNumber, Guid? SupportAttachmentPublicId);
}
