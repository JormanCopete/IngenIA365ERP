using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Reversals;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>Los saldos a favor de pagadores y el saldo a favor de Cartera devuelto por el recaudador (feature 015, US5, T0180; api.md §4). (nuevo)</summary>
public class CreditBalancesEndpoints : ICarterModule
{
    public const string Ver = "Receipts.CreditBalances.View";
    public const string Resolver = "Receipts.CreditBalances.Resolve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = ReceiptsGroup.Grupo(app, "/payer-credit-balances", "Recaudo Saldos a favor");
        g.MapGet("/", async (PayerCreditBalanceState? state, Guid? person, ISender s, CancellationToken ct) =>
                await s.Send(new ListPayerCreditBalancesQuery(state, person), ct))
            .WithName("Receipts_PayerCreditBalances_List").RequirePermission(new AlgunPermiso(Ver, Resolver));
        g.MapPost("/{id:guid}/refund-by-order", async (Guid id, DevolucionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RefundPayerCreditBalanceByOrderCommand(id, body.Amount, body.PersonBankAccountPublicId, body.Form ?? "Transfer")
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Receipts_PayerCreditBalances_RefundByOrder").ConClaveDeOperacion().RequirePermission(Resolver);
        g.MapPost("/{id:guid}/apply", async (Guid id, AplicacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApplyPayerCreditBalanceCommand(id, body.Amount, body.ReceiptPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_PayerCreditBalances_Apply").ConClaveDeOperacion().RequirePermission(Resolver);

        var cartera = ReceiptsGroup.Grupo(app, "/lending-credit-balances", "Recaudo Saldos a favor");
        cartera.MapPost("/refund-by-order", async (RefundLendingCreditBalanceByOrderCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_LendingCreditBalances_RefundByOrder").ConClaveDeOperacion().RequirePermission(Resolver);
    }

    public sealed record DevolucionRequest(decimal Amount, Guid? PersonBankAccountPublicId, string? Form);

    public sealed record AplicacionRequest(decimal Amount, Guid ReceiptPublicId);
}
