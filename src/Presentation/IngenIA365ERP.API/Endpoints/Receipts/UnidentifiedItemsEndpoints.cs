using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>Las partidas por identificar (feature 015, US6, T0198; contracts/api.md §5). (nuevo)</summary>
public class UnidentifiedItemsEndpoints : ICarterModule
{
    public const string Ver = "Receipts.UnidentifiedItems.View";
    public const string Resolver = "Receipts.UnidentifiedItems.Resolve";
    public const string AIngreso = "Receipts.UnidentifiedItems.MoveToIncome";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/unidentified-items", "Recaudo Partidas");
        g.MapGet("/", async (int? aging, Domain.Enums.Receipts.UnidentifiedItemState? state, string? depositor, bool? onlyOpen, ISender s, CancellationToken ct) =>
                await s.Send(new ListUnidentifiedItemsQuery(aging, state, depositor, onlyOpen ?? true), ct))
            .WithName("Receipts_UnidentifiedItems_List").RequirePermission(new AlgunPermiso(Ver, Resolver, AIngreso));
        g.MapPost("/{id:guid}/identify", async (Guid id, IdentificacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new IdentifyUnidentifiedItemCommand(id, body.Parts ?? [], body.PayerPersonPublicId, body.Note, body.MeansClass ?? PaymentMeansClass.Transfer)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_UnidentifiedItems_Identify").ConClaveDeOperacion().RequirePermission(Resolver);
        g.MapPost("/{id:guid}/classify", async (Guid id, ClasificacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ClassifyUnidentifiedItemCommand(id, body.MiscConceptPublicId, body.Reason ?? string.Empty, body.Amount)
                {
                    OperationKey = http.ClaveDeOperacion(), ApprovalRequestPublicId = SessionsEndpoints.SolicitudDeAprobacion(http),
                }, ct))
            .WithName("Receipts_UnidentifiedItems_Classify").ConClaveDeOperacion().RequirePermission(Resolver);
        g.MapPost("/{id:guid}/refund-by-order", async (Guid id, DevolucionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RefundUnidentifiedItemByOrderCommand(id, body.BeneficiaryPersonPublicId, body.Amount, body.PersonBankAccountPublicId, body.Form ?? "Transfer",
                    body.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_UnidentifiedItems_RefundByOrder").ConClaveDeOperacion().RequirePermission(Resolver);
        g.MapPost("/{id:guid}/move-to-income", async (Guid id, IngresoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MoveUnidentifiedItemToIncomeCommand(id, body.MiscConceptPublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(), ApprovalRequestPublicId = SessionsEndpoints.SolicitudDeAprobacion(http),
                }, ct))
            .WithName("Receipts_UnidentifiedItems_MoveToIncome").ConClaveDeOperacion().RequirePermission(AIngreso);
    }

    public sealed record IdentificacionRequest(IReadOnlyList<ParteDeAbono>? Parts, Guid? PayerPersonPublicId, string? Note, PaymentMeansClass? MeansClass);

    public sealed record ClasificacionRequest(Guid MiscConceptPublicId, string? Reason, decimal? Amount);

    public sealed record DevolucionRequest(Guid BeneficiaryPersonPublicId, decimal? Amount, Guid? PersonBankAccountPublicId, string? Form, string? Note);

    public sealed record IngresoRequest(Guid MiscConceptPublicId, string? Reason);
}
