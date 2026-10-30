using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// Las pagadurías en Recaudo (feature 015, US7, T0210; contracts/api.md §6): los archivos de descuentos que llegan de Cartera, confirmar su envío, cargar la respuesta de
/// la pagaduría (multipart hasta 10 MB o captura), revisarla y devolverla a Cartera, leer su evaluación, y registrar y aplicar el giro. Cada escritura lleva
/// <c>Idempotency-Key</c>; cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class PaymastersEndpoints : ICarterModule
{
    public const string Ver = "Receipts.Paymasters.View";
    public const string ConfirmarEnvio = "Receipts.Paymasters.ConfirmSent";
    public const string CargarRespuesta = "Receipts.Paymasters.LoadResponse";
    public const string RegistrarGiro = "Receipts.Paymasters.RegisterRemittance";
    public const string AplicarGiro = "Receipts.Paymasters.ApplyRemittance";
    public const long MaxBytes = 10 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var todos = new AlgunPermiso(Ver, ConfirmarEnvio, CargarRespuesta, RegistrarGiro, AplicarGiro);
        var f = ReceiptsGroup.Grupo(app, "/payroll-files", "Recaudo Pagadurías");
        f.MapGet("/", async (Guid? paymaster, int? year, int? month, PayrollDeductionFileState? state, ISender s, CancellationToken ct) =>
                await s.Send(new ListPayrollDeductionFilesQuery(paymaster, year, month, state), ct))
            .WithName("Receipts_PayrollFiles_List").RequirePermission(todos);
        f.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetPayrollDeductionFileQuery(id), ct))
            .WithName("Receipts_PayrollFiles_Get").RequirePermission(todos);
        f.MapGet("/{id:guid}/file", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new DownloadPayrollDeductionFileCommand(id), ct))
            .WithName("Receipts_PayrollFiles_Download").RequirePermission(Ver);
        f.MapPost("/{id:guid}/sent", async (Guid id, EnvioRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ConfirmPayrollFileSentCommand(id, body.Channel, body.SentAt, body.Reference) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PayrollFiles_Sent").ConClaveDeOperacion().RequirePermission(ConfirmarEnvio);
        f.MapGet("/{id:guid}/responses", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new ListPaymasterResponsesQuery(id), ct))
            .WithName("Receipts_PayrollFiles_Responses").RequirePermission(todos);
        f.MapPost("/{id:guid}/responses", CargarAsync).WithName("Receipts_PayrollFiles_LoadResponse").DisableAntiforgery().ConClaveDeOperacion()
            .RequirePermission(CargarRespuesta);
        f.MapPost("/{id:guid}/responses/captured", async (Guid id, CapturaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new LoadPaymasterResponseCommand(id, body.FileName, Captured: body.Rows ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PayrollFiles_CaptureResponse").ConClaveDeOperacion().RequirePermission(CargarRespuesta);
        f.MapGet("/{id:guid}/responses/{rid:guid}/preview", async (Guid id, Guid rid, ISender s, CancellationToken ct) =>
                await s.Send(new PreviewPaymasterResponseQuery(id, rid), ct))
            .WithName("Receipts_PayrollFiles_PreviewResponse").RequirePermission(CargarRespuesta);
        f.MapPost("/{id:guid}/responses/{rid:guid}/emit", async (Guid id, Guid rid, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new EmitPaymasterResultCommand(id, rid) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PayrollFiles_EmitResponse").ConClaveDeOperacion().RequirePermission(CargarRespuesta);
        f.MapPost("/{id:guid}/responses/{rid:guid}/refresh", async (Guid id, Guid rid, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RefreshPaymasterResponseStatusCommand(id, rid) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PayrollFiles_RefreshResponse").ConClaveDeOperacion().RequirePermission(todos);

        var g = ReceiptsGroup.Grupo(app, "/paymaster-remittances", "Recaudo Giros de pagadurías");
        g.MapGet("/", async (Guid? paymaster, ISender s, CancellationToken ct) => await s.Send(new ListPaymasterRemittancesQuery(paymaster), ct))
            .WithName("Receipts_PaymasterRemittances_List").RequirePermission(todos);
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetPaymasterRemittanceQuery(id), ct))
            .WithName("Receipts_PaymasterRemittances_Get").RequirePermission(todos);
        g.MapPost("/", async (GiroRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterPaymasterRemittanceCommand(body.PaymasterPersonPublicId, body.BankStatementLinePublicId, body.FilePublicIds ?? [], body.Amount,
                    body.AllocationOrder ?? PaymasterAllocationOrder.AgreementPriority, body.LineOrder, body.SurplusAsCreditBalance, body.Note)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_PaymasterRemittances_Register").ConClaveDeOperacion().RequirePermission(RegistrarGiro);
        g.MapPost("/{id:guid}/apply", async (Guid id, AplicarRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApplyPaymasterRemittanceCommand(id, body?.MaxLines ?? 200, body?.RunPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PaymasterRemittances_Apply").ConClaveDeOperacion().RequirePermission(AplicarGiro);

        // Feature 015, R1 (T0384; FR-082): el calendario legal de giro y el informe de morosas (onlyLate).
        var cal = ReceiptsGroup.Grupo(app, "/paymaster-calendar", "Recaudo Calendario de pagadurías");
        cal.MapGet("/", async (Guid? paymaster, int? year, bool? onlyLate, ISender s, CancellationToken ct) =>
                await s.Send(new GetPaymasterCalendarQuery(paymaster, year, onlyLate ?? false), ct))
            .WithName("Receipts_PaymasterCalendar_Get").RequirePermission(todos);
        cal.MapPost("/", async (RegisterPaymasterPayDateCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PaymasterCalendar_Register").ConClaveDeOperacion().RequirePermission(RegistrarGiro);

        var b = ReceiptsGroup.Grupo(app, "/paymaster-balances", "Recaudo Saldos de pagadurías");
        b.MapPost("/{id:guid}/resolve", async (Guid id, ResolverRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ResolvePaymasterBalanceCommand(id, body.Resolution ?? string.Empty, body.Amount, body.SupportAttachmentPublicId, body.PersonBankAccountPublicId,
                    body.ReceiptPublicId, body.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PaymasterBalances_Resolve").ConClaveDeOperacion().RequirePermission(AplicarGiro);
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
        return await sender.Send(new LoadPaymasterResponseCommand(id, archivo.FileName, ms.ToArray(), format) { OperationKey = http.ClaveDeOperacion() }, ct);
    }

    public sealed record EnvioRequest(PayrollFileChannel Channel, DateTime? SentAt, string? Reference);

    public sealed record CapturaRequest(string? FileName, IReadOnlyList<FilaDeRespuestaCapturada>? Rows);

    public sealed record GiroRequest(Guid PaymasterPersonPublicId, Guid BankStatementLinePublicId, IReadOnlyList<Guid>? FilePublicIds, decimal? Amount,
        PaymasterAllocationOrder? AllocationOrder, IReadOnlyList<Guid>? LineOrder, bool SurplusAsCreditBalance, string? Note);

    public sealed record AplicarRequest(int? MaxLines, Guid? RunPublicId);

    public sealed record ResolverRequest(string? Resolution, decimal Amount, Guid SupportAttachmentPublicId, Guid? PersonBankAccountPublicId, Guid? ReceiptPublicId, string? Note);
}
