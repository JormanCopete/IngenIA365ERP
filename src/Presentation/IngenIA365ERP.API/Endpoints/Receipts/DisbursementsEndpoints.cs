using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.Disbursements;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// Los egresos de caja (feature 015, R1, US4, T0374; contracts/api.md §4): entregar dinero por la caja con la autorización del módulo, anularlo si el dinero vuelve
/// y su documento firmado. Las escrituras llevan <c>Idempotency-Key</c>; lo que pide aprobador presente se repite con <c>X-Approval-Request</c>. Cada ruta sólo
/// reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DisbursementsEndpoints : ICarterModule
{
    public const string Ver = "Receipts.Disbursements.View";
    public const string Registrar = "Receipts.Disbursements.Register";
    public const string Aprobar = "Receipts.Disbursements.Approve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/cash-disbursements", "Recaudo Egresos de caja");
        g.MapGet("/", async (Guid? session, DateOnly? date, ISender s, CancellationToken ct) => await s.Send(new ListCashDisbursementsQuery(session, date), ct))
            .WithName("Receipts_CashDisbursements_List").RequirePermission(new AlgunPermiso(Ver, Registrar));
        g.MapPost("/", async (RegisterCashDisbursementCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                ReceiptsGroup.Creado(await s.Send(body with
                {
                    OperationKey = http.ClaveDeOperacion(),
                    ApprovalRequestPublicId = SessionsEndpoints.SolicitudDeAprobacion(http),
                }, ct), v => $"/api/receipts/cash-disbursements/{v.PublicId}"))
            .WithName("Receipts_CashDisbursements_Register").ConClaveDeOperacion().RequirePermission(Registrar);
        g.MapPost("/{id:guid}/void", async (Guid id, AnularEgresoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new VoidCashDisbursementCommand(id, body.SessionPublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                    ApprovalRequestPublicId = SessionsEndpoints.SolicitudDeAprobacion(http),
                }, ct))
            .WithName("Receipts_CashDisbursements_Void").ConClaveDeOperacion().RequirePermission(Registrar);
        g.MapGet("/{id:guid}/document", async (Guid id, ISender s, Application.Receipts.Sessions.IDocumentosDeRecaudoEnPdf pdf, CancellationToken ct) =>
            {
                var r = await s.Send(new GetCashDisbursementDocumentQuery(id), ct);
                return r.IsSuccess ? Results.File(pdf.Egreso(r.Value), "application/pdf", $"egreso-{r.Value.Number}.pdf") : (object)r;
            })
            .WithName("Receipts_CashDisbursements_Document").RequirePermission(new AlgunPermiso(Ver, Registrar));
    }

    /// <summary>La anulación: la sesión abierta donde vuelve el dinero y el motivo.</summary>
    public sealed record AnularEgresoRequest(Guid SessionPublicId, string? Reason);
}
