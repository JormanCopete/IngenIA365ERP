using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Pila;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>
/// Feature 010 (US5, contracts/api.md §7): la planilla PILA. Datos del aportante, layouts,
/// fecha límite, validación previa, generación versionada, detalle con explicación por campo,
/// descarga del <c>.txt</c> (con la diferencia del cuadre mostrada antes) y marca de cargada.
/// Permiso por tramo; sólo reenvía al <c>ISender</c>.
/// </summary>
public sealed class PilaEndpoints : ICarterModule
{
    public sealed record GenerateBody(bool AcknowledgeWarnings);
    public sealed record MarkUploadedBody(DateTime UploadedAt, string OperatorReference, DateOnly? OperatorFilingDate, DateOnly? PaidAt);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/pila")
            .WithTags("PayrollPila")
            .RequireAuthorization();

        group.MapGet("/settings", async (ISender sender, CancellationToken ct) => await sender.Send(new GetPilaSettingsQuery(), ct))
            .WithName("Payroll_Pila_GetSettings").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.View");

        group.MapPut("/settings", async (PilaSettingsInput body, ISender sender, CancellationToken ct) => await sender.Send(new UpdatePilaSettingsCommand(body), ct))
            .WithName("Payroll_Pila_UpdateSettings").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.Manage");

        group.MapGet("/layouts", async (ISender sender, CancellationToken ct) => await sender.Send(new ListPilaLayoutsQuery(), ct))
            .WithName("Payroll_Pila_Layouts").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.View");

        group.MapGet("/{year:int}/{month:int}/due-date", async (int year, int month, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPilaDueDateQuery((short)year, (byte)month), ct))
            .WithName("Payroll_Pila_DueDate").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.View");

        group.MapPost("/{year:int}/{month:int}/validate", async (int year, int month, ISender sender, CancellationToken ct) =>
                await sender.Send(new ValidatePilaQuery((short)year, (byte)month), ct))
            .WithName("Payroll_Pila_Validate").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.Generate");

        group.MapPost("/{year:int}/{month:int}/generate", async (int year, int month, GenerateBody? body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GeneratePilaCommand((short)year, (byte)month, body?.AcknowledgeWarnings ?? false), ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/pila/{result.Value.GenerationPublicId}", result.Value) : (object)result;
            })
            .WithName("Payroll_Pila_Generate").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.Generate");

        group.MapGet("/", async (int? year, int? month, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPilaGenerationsQuery(year is { } y ? (short)y : null, month is { } m ? (byte)m : null), ct))
            .WithName("Payroll_Pila_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetPilaGenerationQuery(id), ct))
            .WithName("Payroll_Pila_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.View");

        group.MapGet("/{id:guid}/lines/{lineNumber:int}/explanation", async (Guid id, int lineNumber, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPilaLineExplanationQuery(id, lineNumber), ct))
            .WithName("Payroll_Pila_LineExplanation").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.View");

        group.MapGet("/{id:guid}/file", async (Guid id, bool? acknowledgeDifference, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DownloadPilaFileQuery(id, acknowledgeDifference ?? false), ct);
                if (result.IsFailure) return (object)result;
                return Results.File(result.Value.Content, $"{result.Value.ContentType}; charset={result.Value.Encoding}", result.Value.FileName);
            })
            .WithName("Payroll_Pila_Download").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.View");

        group.MapPost("/{id:guid}/mark-uploaded", async (Guid id, MarkUploadedBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new MarkPilaUploadedCommand(id, body.UploadedAt, body.OperatorReference, body.OperatorFilingDate, body.PaidAt), ct))
            .WithName("Payroll_Pila_MarkUploaded").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.Pila.MarkUploaded");
    }
}
