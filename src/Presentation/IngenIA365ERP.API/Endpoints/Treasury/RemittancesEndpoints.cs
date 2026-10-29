using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Remittances;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// Las remesas de efectivo (feature 015, US19, T0230; contracts/api.md §12): la lista con los días en tránsito, la confirmación de la remesa al banco con soporte o con la
/// línea del extracto, y el efectivo que sale del banco a una bóveda. La remesa de la bóveda al banco la despacha Recaudo (<c>POST /api/receipts/vaults/{id}/remittances</c>). (nuevo)
/// </summary>
public class RemittancesEndpoints : ICarterModule
{
    public const string Ver = "Treasury.Remittances.View";
    public const string Confirmar = "Treasury.Remittances.Confirm";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/remittances", "Tesorería Remesas");
        g.MapGet("/", async (RemittanceState? state, ISender s, CancellationToken ct) => await s.Send(new ListCashRemittancesQuery(state), ct))
            .WithName("Treasury_Remittances_List").RequirePermission(new AlgunPermiso(Ver, Confirmar));
        g.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ConfirmRemittanceCommand(id, body.SupportAttachmentPublicId, body.StatementLinePublicId, body.ConfirmedOn)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Treasury_Remittances_Confirm").ConClaveDeOperacion().RequirePermission(Confirmar);
        g.MapPost("/bank-to-vault", async (BancoABovedaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RegisterBankToVaultRemittanceCommand(body.BankAccountPublicId, body.VaultPublicId, body.BranchPublicId, body.Amount, body.BagSeal,
                    body.MinutesAttachmentPublicId, body.LiquidityFundUsePublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Remittances_BankToVault").ConClaveDeOperacion().RequirePermission(Confirmar);
    }

    public sealed record ConfirmacionRequest(Guid? SupportAttachmentPublicId, Guid? StatementLinePublicId, DateOnly? ConfirmedOn);

    public sealed record BancoABovedaRequest(Guid BankAccountPublicId, Guid VaultPublicId, Guid BranchPublicId, decimal Amount, string? BagSeal, Guid? MinutesAttachmentPublicId,
        Guid? LiquidityFundUsePublicId);
}
