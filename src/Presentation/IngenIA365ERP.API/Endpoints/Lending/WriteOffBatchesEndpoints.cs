using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.WriteOffs;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los lotes de castigo (feature 013, K3, US14, T0915; contracts/api.md §11.3 y la ruta nueva de T0909; FR-095, FR-096, C98): la lista y el detalle
/// (<c>Lending.Loans.Read</c>); armar el lote, agregar y quitar créditos y enviarlo al consejo (<c>Lending.WriteOffs.Prepare</c>); aplicarlo con el punto
/// del acta que lo aprobó y registrar el reporte al supervisor (<c>Lending.WriteOffs.Apply</c>). El lote lo decide el consejo por el punto
/// <c>Decision</c> de su sesión (sujeto <c>WriteOffBatch</c>, <see cref="AsuntoDeLoteDeCastigo"/>), no por una ruta propia. Toda escritura lleva
/// <c>Idempotency-Key</c>; fuera del alcance, el mismo 404 (§1.3). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class WriteOffBatchesEndpoints : ICarterModule
{
    public const string Leer = "Lending.Loans.Read";
    public const string Preparar = "Lending.WriteOffs.Prepare";
    public const string Aplicar = "Lending.WriteOffs.Apply";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var lotes = LendingGroup.Grupo(app, "/write-off-batches", "Cartera Castigos");

        lotes.MapGet("/", async (WriteOffBatchStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListWriteOffBatchesQuery(status, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_WriteOffBatches_List")
            .RequirePermission(Leer);

        lotes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetWriteOffBatchQuery(id), ct))
            .WithName("Lending_WriteOffBatches_Get")
            .RequirePermission(Leer);

        lotes.MapPost("/", async (LoteRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateWriteOffBatchCommand(body.LoanPublicIds ?? [], body.Reason ?? string.Empty, body.Reports)
                    { OperationKey = http.ClaveDeOperacion() }, ct), l => $"{LendingGroup.Prefijo}/write-off-batches/{l.PublicId}"))
            .WithName("Lending_WriteOffBatches_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Preparar);

        lotes.MapPost("/{id:guid}/items", async (Guid id, ItemRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new AddWriteOffBatchItemCommand(id, body.LoanPublicId, body.IrrecoverabilityAttachmentPublicId,
                    body.ReportAttachmentPublicId, body.ContributionsOffsetAttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_WriteOffBatches_AddItem")
            .ConClaveDeOperacion()
            .RequirePermission(Preparar);

        lotes.MapDelete("/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RemoveWriteOffBatchItemCommand(id, itemId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_WriteOffBatches_RemoveItem")
            .ConClaveDeOperacion()
            .RequirePermission(Preparar);

        lotes.MapPost("/{id:guid}/submit", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SubmitWriteOffBatchCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_WriteOffBatches_Submit")
            .ConClaveDeOperacion()
            .RequirePermission(Preparar);

        lotes.MapPost("/{id:guid}/apply", async (Guid id, AplicarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyWriteOffBatchCommand(id, body.SessionItemPublicId, body.AcknowledgeWarnings ?? false)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_WriteOffBatches_Apply")
            .ConClaveDeOperacion()
            .RequirePermission(Aplicar);

        lotes.MapPost("/{id:guid}/supervisor-report", async (Guid id, ReporteRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RegisterWriteOffSupervisorReportCommand(id, body.Reference ?? string.Empty, body.AttachmentPublicId)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_WriteOffBatches_SupervisorReport")
            .ConClaveDeOperacion()
            .RequirePermission(Aplicar);
    }

    /// <summary><c>{ loanPublicIds[], reason, reports: [{ loanPublicId, irrecoverabilityAttachmentPublicId, reportAttachmentPublicId, contributionsOffsetAttachmentPublicId }] }</c> (§11.3).</summary>
    public sealed record LoteRequest(IReadOnlyList<Guid>? LoanPublicIds, string? Reason, IReadOnlyList<WriteOffSupportInput>? Reports);

    /// <summary>Un crédito más con sus soportes (§11.3).</summary>
    public sealed record ItemRequest(Guid LoanPublicId, Guid? IrrecoverabilityAttachmentPublicId, Guid? ReportAttachmentPublicId,
        Guid? ContributionsOffsetAttachmentPublicId);

    /// <summary><c>{ sessionItemPublicId, acknowledgeWarnings? }</c>: el punto del acta del consejo que aprobó el lote.</summary>
    public sealed record AplicarRequest(Guid SessionItemPublicId, bool? AcknowledgeWarnings);

    /// <summary><c>{ reference, attachmentPublicId? }</c>: el radicado del reporte al supervisor y su soporte.</summary>
    public sealed record ReporteRequest(string? Reference, Guid? AttachmentPublicId);
}
