using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Reversals;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// La anulación de recibos (feature 015, US5, T0180; contracts/api.md §4). Las escrituras llevan <c>Idempotency-Key</c>; cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class VoidingsEndpoints : ICarterModule
{
    public const string Pedir = "Receipts.Voiding.Request";
    public const string Aprobar = "Receipts.Voiding.Approve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var recibos = ReceiptsGroup.Grupo(app, "/receipts", "Recaudo Anulaciones");
        recibos.MapPost("/{id:guid}/voiding", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RequestReceiptVoidingCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Receipts_RequestVoiding").ConClaveDeOperacion().RequirePermission(Pedir);

        var anulaciones = ReceiptsGroup.Grupo(app, "/voidings", "Recaudo Anulaciones");
        anulaciones.MapGet("/", async (ReceiptVoidingState? state, ISender s, CancellationToken ct) => await s.Send(new ListReceiptVoidingsQuery(state), ct))
            .WithName("Receipts_Voidings_List").RequirePermission(new AlgunPermiso(Pedir, Aprobar));
        anulaciones.MapPost("/{id:guid}/approve", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApproveReceiptVoidingCommand(id, body.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Voidings_Approve").ConClaveDeOperacion().RequirePermission(Aprobar);
        anulaciones.MapPost("/{id:guid}/reject", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RejectReceiptVoidingCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_Voidings_Reject").ConClaveDeOperacion().RequirePermission(Aprobar);
        // T0179 (FR-156): el documento de una anulación aplicada.
        anulaciones.MapGet("/{id:guid}/document", async (Guid id, ISender s, Application.Receipts.Sessions.IDocumentosDeRecaudoEnPdf pdf, CancellationToken ct) =>
            {
                var r = await s.Send(new GetVoidingDocumentQuery(id), ct);
                return r.IsSuccess ? Results.File(pdf.Anulacion(r.Value), "application/pdf", $"anulacion-{r.Value.Number}.pdf") : (object)r;
            })
            .WithName("Receipts_Voidings_Document").RequirePermission(new AlgunPermiso(Pedir, Aprobar));
    }

    public sealed record MotivoRequest(string? Reason);
}
