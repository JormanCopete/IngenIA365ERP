using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Checks;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// Los cheques recibidos (feature 015, US15, T0219; contracts/api.md §4): la lista, los giradores con devoluciones, las causales, la custodia del posfechado, las
/// transiciones manuales (consignado, en canje, pagado) y la devolución con reverso de sólo sus porciones. Cada escritura lleva <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public class ReceivedChecksEndpoints : ICarterModule
{
    public const string Ver = "Receipts.Checks.View";
    public const string Gestionar = "Receipts.Checks.Manage";
    public const string Devolver = "Receipts.Checks.Return";
    public const string Registrar = "Receipts.Receipt.Register";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var todos = new AlgunPermiso(Ver, Gestionar, Devolver);
        var g = ReceiptsGroup.Grupo(app, "/received-checks", "Recaudo Cheques recibidos");
        g.MapGet("/", async (ReceivedCheckState? state, DateOnly? from, DateOnly? to, ISender s, CancellationToken ct) =>
                await s.Send(new ListReceivedChecksQuery(state, from, to), ct))
            .WithName("Receipts_ReceivedChecks_List").RequirePermission(todos);
        g.MapGet("/drawers-with-returns", async (Guid? person, string? account, ISender s, CancellationToken ct) =>
                await s.Send(new ListDrawersWithReturnsQuery(person, account), ct))
            .WithName("Receipts_ReceivedChecks_DrawersWithReturns").RequirePermission(new AlgunPermiso(Ver, Gestionar, Devolver, Registrar));
        g.MapGet("/return-causes", async (ISender s, CancellationToken ct) => await s.Send(new ListCheckReturnCausesQuery(), ct))
            .WithName("Receipts_ReceivedChecks_ReturnCauses").RequirePermission(todos);
        g.MapPost("/custody", async (CustodiaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterCheckInCustodyCommand(body.PayerPersonPublicId, body.BranchPublicId, body.Amount, body.Check, body.Note)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_ReceivedChecks_Custody").ConClaveDeOperacion().RequirePermission(new AlgunPermiso(Gestionar, Registrar));
        g.MapPost("/{id:guid}/deposit", async (Guid id, ConsignacionRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkReceivedCheckDepositedCommand(id, body?.RemittancePublicId, body?.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_ReceivedChecks_Deposit").ConClaveDeOperacion().RequirePermission(Gestionar);
        g.MapPost("/{id:guid}/clearing", async (Guid id, NotaRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkReceivedCheckInClearingCommand(id, body?.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_ReceivedChecks_Clearing").ConClaveDeOperacion().RequirePermission(Gestionar);
        g.MapPost("/{id:guid}/paid", async (Guid id, NotaRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkReceivedCheckPaidCommand(id, body?.Note) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_ReceivedChecks_Paid").ConClaveDeOperacion().RequirePermission(Gestionar);
        g.MapPost("/{id:guid}/return", async (Guid id, DevolucionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ReturnReceivedCheckCommand(id, body.CauseCode ?? string.Empty, body.SupportAttachmentPublicId, body.Note)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_ReceivedChecks_Return").ConClaveDeOperacion().RequirePermission(Devolver);
    }

    public sealed record CustodiaRequest(Guid PayerPersonPublicId, Guid BranchPublicId, decimal Amount, ChequeDeCajaDto Check, string? Note);

    public sealed record ConsignacionRequest(Guid? RemittancePublicId, string? Note);

    public sealed record NotaRequest(string? Note);

    public sealed record DevolucionRequest(string? CauseCode, Guid? SupportAttachmentPublicId, string? Note);
}
