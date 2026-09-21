using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Payroll.Dispersion;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>
/// Feature 010 (US8, contracts/api.md §9): archivos de dispersión bancaria desde la relación de
/// pago de cualquier corrida aprobada. Los formatos viven en <c>/api/core/bank-file-formats</c>
/// (D-42). Permiso por tramo; sólo reenvía al <c>ISender</c>.
/// </summary>
public sealed class DisbursementsEndpoints : ICarterModule
{
    public sealed record GenerateBody(Guid RunPublicId, Guid? FormatPublicId, DateOnly PaymentDate, Guid? SourceAccountPublicId, string? Reference, IReadOnlyList<Guid>? EmployeePublicIds);
    public sealed record PreviewBody(Guid RunPublicId, Guid? FormatPublicId, DateOnly PaymentDate, Guid? SourceAccountPublicId, string? Reference, BankFileFormatDefinition? Definition);
    public sealed record MarkSentBody(DateTime SentAt, string? BankReference, DateTime? PaidAt, string? Notes);
    public sealed record CancelBody(string Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/disbursements")
            .WithTags("PayrollDisbursements")
            .RequireAuthorization();

        group.MapGet("/", async (Guid? runId, int? year, string? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDisbursementFilesQuery(runId, year, status), ct))
            .WithName("Payroll_Disbursements_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Disbursement.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDisbursementFileQuery(id), ct))
            .WithName("Payroll_Disbursements_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Disbursement.View");

        group.MapGet("/{id:guid}/file", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DownloadDisbursementFileQuery(id), ct);
                if (result.IsFailure) return (object)result;
                var tipo = result.Value.ContentType.Contains("charset", StringComparison.OrdinalIgnoreCase)
                    ? result.Value.ContentType
                    : $"{result.Value.ContentType}; charset={result.Value.Encoding}";
                return Results.File(result.Value.Content, tipo, result.Value.FileName);
            })
            .WithName("Payroll_Disbursements_Download")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Disbursement.View");

        group.MapPost("/preview", async (PreviewBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewDisbursementFileCommand(body.RunPublicId, body.FormatPublicId, body.PaymentDate, body.SourceAccountPublicId, body.Reference, body.Definition), ct))
            .WithName("Payroll_Disbursements_Preview")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Disbursement.Generate");

        group.MapPost("/", async (GenerateBody body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GenerateDisbursementFileCommand(body.RunPublicId, body.FormatPublicId, body.PaymentDate, body.SourceAccountPublicId, body.Reference, body.EmployeePublicIds), ct);
                return result.IsSuccess
                    ? Results.Created($"/api/payroll/disbursements/{result.Value.FilePublicId}", result.Value)
                    : (object)result;
            })
            .WithName("Payroll_Disbursements_Generate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Disbursement.Generate");

        group.MapPost("/{id:guid}/mark-sent", async (Guid id, MarkSentBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new MarkDisbursementSentCommand(id, body.SentAt, body.BankReference, body.PaidAt, body.Notes), ct))
            .WithName("Payroll_Disbursements_MarkSent")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Disbursement.MarkSent");

        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new CancelDisbursementFileCommand(id, body.Reason), ct))
            .WithName("Payroll_Disbursements_Cancel")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Disbursement.Generate");
    }
}
