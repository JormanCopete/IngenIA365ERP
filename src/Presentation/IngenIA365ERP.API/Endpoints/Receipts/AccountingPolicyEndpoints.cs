using Carter;
using IngenIA365ERP.API.Endpoints.Treasury;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Treasury.Common;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Receipts;

/// <summary>
/// El cambio de la política contable de Recaudo y de Tesorería (feature 015, US8, T0262; FR-221; api.md §16): <c>PUT /api/receipts/accounting-policy</c> con
/// <c>Receipts.Accounting.PolicyChange</c> y <c>PUT /api/treasury/accounting-policy</c> con <c>Treasury.Accounting.PolicyChange</c>; «bloquear» o «advertir» con
/// motivo y <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public class AccountingPolicyEndpoints : ICarterModule
{
    public const string CambiarEnRecaudo = "Receipts.Accounting.PolicyChange";
    public const string CambiarEnTesoreria = "Treasury.Accounting.PolicyChange";

    public sealed record PoliticaRequest(string? Policy, string? Reason, DateOnly? ValidFrom);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var r = ReceiptsGroup.Grupo(app, "/accounting-policy", "Recaudo Contabilidad");
        r.MapPut("/", async (PoliticaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SetReceiptsAccountingPolicyCommand(body.Policy ?? string.Empty, body.Reason ?? string.Empty, body.ValidFrom)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Receipts_AccountingPolicy_Set").ConClaveDeOperacion().RequirePermission(CambiarEnRecaudo);

        var t = TreasuryGroup.Grupo(app, "/accounting-policy", "Tesorería Contabilidad");
        t.MapPut("/", async (PoliticaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SetTreasuryAccountingPolicyCommand(body.Policy ?? string.Empty, body.Reason ?? string.Empty, body.ValidFrom)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_AccountingPolicy_Set").ConClaveDeOperacion().RequirePermission(CambiarEnTesoreria);
    }
}
