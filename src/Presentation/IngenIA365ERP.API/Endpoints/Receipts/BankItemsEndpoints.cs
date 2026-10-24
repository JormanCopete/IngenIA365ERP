using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// Los abonos de los extractos que Recaudo identifica (feature 015, US6, T0198; contracts/api.md §5): consultar, identificar y aplicar con división, dejar como partida,
/// clasificar con aprobador presente (428 y <c>X-Approval-Request</c>) y decidir los posibles duplicados. Cada escritura lleva <c>Idempotency-Key</c>; cada ruta sólo
/// reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class BankItemsEndpoints : ICarterModule
{
    public const string Ver = "Receipts.BankItems.View";
    public const string Identificar = "Receipts.BankItems.Identify";
    public const string Clasificar = "Receipts.BankItems.Classify";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/bank-items", "Recaudo Abonos");
        g.MapGet("/", async (Guid? account, DateOnly? from, DateOnly? to, string? reference, bool? onlyPending, int? page, int? pageSize, ISender s, CancellationToken ct) =>
                await s.Send(new ListBankItemsQuery(account, from, to, reference, onlyPending ?? true, page ?? 1, pageSize ?? 50), ct))
            .WithName("Receipts_BankItems_List").RequirePermission(new AlgunPermiso(Ver, Identificar, Clasificar));
        g.MapGet("/{lineId:guid}/identifications", async (Guid lineId, ISender s, CancellationToken ct) => await s.Send(new ListBankItemIdentificationsQuery(lineId), ct))
            .WithName("Receipts_BankItems_Identifications").RequirePermission(new AlgunPermiso(Ver, Identificar, Clasificar));
        g.MapPost("/{lineId:guid}/identify", async (Guid lineId, IdentificacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new IdentifyBankItemCommand(lineId, body.Parts ?? [], body.PayerPersonPublicId, body.RemainderAsUnidentified, body.Note, body.RunPublicId,
                    body.MeansClass ?? PaymentMeansClass.Transfer, body.IdentificationRulePublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_BankItems_Identify").ConClaveDeOperacion().RequirePermission(Identificar);
        g.MapPost("/{lineId:guid}/unidentified", async (Guid lineId, NotaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkBankItemUnidentifiedCommand(lineId, body.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_BankItems_Unidentified").ConClaveDeOperacion().RequirePermission(Identificar);
        g.MapPost("/{lineId:guid}/classify", async (Guid lineId, ClasificacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ClassifyBankItemCommand(lineId, body.MiscConceptPublicId, body.Reason ?? string.Empty, body.Amount, body.PaymentOrderPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(), ApprovalRequestPublicId = SessionsEndpoints.SolicitudDeAprobacion(http),
                }, ct))
            .WithName("Receipts_BankItems_Classify").ConClaveDeOperacion().RequirePermission(Clasificar);
        g.MapPost("/{lineId:guid}/duplicate-decision", async (Guid lineId, DuplicadoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DecideBankItemDuplicateCommand(lineId, body.IsDuplicate, body.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_BankItems_DuplicateDecision").ConClaveDeOperacion().RequirePermission(Identificar);
    }

    public sealed record IdentificacionRequest(IReadOnlyList<ParteDeAbono>? Parts, Guid? PayerPersonPublicId, bool RemainderAsUnidentified, string? Note, Guid? RunPublicId,
        PaymentMeansClass? MeansClass, Guid? IdentificationRulePublicId = null);

    public sealed record NotaRequest(string? Note);

    public sealed record ClasificacionRequest(Guid MiscConceptPublicId, string? Reason, decimal? Amount, Guid? PaymentOrderPublicId);

    public sealed record DuplicadoRequest(bool IsDuplicate, string? Note);
}
