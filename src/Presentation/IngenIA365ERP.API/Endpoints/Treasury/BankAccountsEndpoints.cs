using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.LiquidityFund;
using IngenIA365ERP.Application.Treasury.Security;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// Las cuentas bancarias propias, sus saldos, el alcance por cuenta y el fondo de liquidez (feature 015, US10, T0146; US12, T0059; contracts/api.md §9). Las
/// escrituras llevan <c>Idempotency-Key</c>; cada ruta sólo reenvía al <see cref="ISender"/>. Los saldos son lectura sensible auditada. (nuevo)
/// </summary>
public class BankAccountsEndpoints : ICarterModule
{
    public const string Ver = "Treasury.BankAccounts.View";
    public const string Administrar = "Treasury.BankAccounts.Manage";
    public const string VerSaldos = "Treasury.BankBalances.View";
    public const string Alcance = "Treasury.Scope.Manage";
    public const string PedirUsoDelFondo = "Treasury.LiquidityFund.Request";
    public const string AprobarUsoDelFondo = "Treasury.LiquidityFund.Approve";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/bank-accounts", "Tesorería Cuentas bancarias");
        g.MapGet("/", async (bool? includeInactive, ISender s, CancellationToken ct) => await s.Send(new ListOwnBankAccountsQuery(includeInactive ?? false), ct))
            .WithName("Treasury_BankAccounts_List").RequirePermission(Ver);
        g.MapGet("/chart-accounts", async (Guid? bank, ISender s, CancellationToken ct) => await s.Send(new ListChartBankAccountsQuery(bank), ct))
            .WithName("Treasury_BankAccounts_ChartAccounts").RequirePermission(Administrar);
        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => await s.Send(new GetOwnBankAccountQuery(id), ct))
            .WithName("Treasury_BankAccounts_Get").RequirePermission(Ver);
        g.MapGet("/{id:guid}/balances", async (Guid id, DateOnly? date, ISender s, CancellationToken ct) =>
                await s.Send(new GetOwnBankAccountBalancesQuery(id, date), ct))
            .WithName("Treasury_BankAccounts_Balances").RequirePermission(VerSaldos);
        g.MapPost("/", async (CreateOwnBankAccountCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                TreasuryGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/treasury/bank-accounts/{v.PublicId}"))
            .WithName("Treasury_BankAccounts_Create").ConClaveDeOperacion().RequirePermission(Administrar);
        g.MapPut("/{id:guid}", async (Guid id, CuentaRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateOwnBankAccountCommand(id, body.Name ?? string.Empty, body.BranchPublicId, body.Purposes, body.IsGmfExempt,
                    body.BankThirdPartyPersonPublicId, body.ChartAccountPublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_BankAccounts_Update").ConClaveDeOperacion().RequirePermission(Administrar);
        g.MapPost("/{id:guid}/inactivate", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new InactivateOwnBankAccountCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_BankAccounts_Inactivate").ConClaveDeOperacion().RequirePermission(Administrar);

        // US12 (T0059): el alcance de Tesorería de un usuario, por cuenta propia.
        var usuarios = TreasuryGroup.Grupo(app, "/users", "Tesorería Alcance");
        usuarios.MapGet("/{userId:guid}/scope", async (Guid userId, ISender s, CancellationToken ct) => await s.Send(new GetTreasuryUserScopeQuery(userId), ct))
            .WithName("Treasury_UserScope_Get").RequirePermission(Alcance);
        usuarios.MapPut("/{userId:guid}/scope", async (Guid userId, AlcanceRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SetTreasuryUserScopeCommand(userId, body.BankAccountPublicIds ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_UserScope_Set").ConClaveDeOperacion().RequirePermission(Alcance);

        var fondo = TreasuryGroup.Grupo(app, "/liquidity-fund-uses", "Tesorería Fondo de liquidez");
        fondo.MapGet("/", async (Guid? bankAccount, LiquidityFundUseState? state, ISender s, CancellationToken ct) =>
                await s.Send(new ListLiquidityFundUsesQuery(bankAccount, state), ct))
            .WithName("Treasury_LiquidityFund_List").RequirePermission(new AlgunPermiso(PedirUsoDelFondo, AprobarUsoDelFondo));
        fondo.MapPost("/", async (RequestLiquidityFundUseCommand body, HttpContext http, ISender s, CancellationToken ct) =>
                TreasuryGroup.Creado(await s.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct), v => $"/api/treasury/liquidity-fund-uses/{v.PublicId}"))
            .WithName("Treasury_LiquidityFund_Request").ConClaveDeOperacion().RequirePermission(PedirUsoDelFondo);
        fondo.MapPost("/{id:guid}/approve", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApproveLiquidityFundUseCommand(id, body.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_LiquidityFund_Approve").ConClaveDeOperacion().RequirePermission(AprobarUsoDelFondo);
        fondo.MapPost("/{id:guid}/reject", async (Guid id, MotivoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RejectLiquidityFundUseCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_LiquidityFund_Reject").ConClaveDeOperacion().RequirePermission(AprobarUsoDelFondo);
    }

    public sealed record CuentaRequest(string? Name, Guid BranchPublicId, BankAccountPurpose Purposes, bool IsGmfExempt, Guid? BankThirdPartyPersonPublicId,
        Guid? ChartAccountPublicId);

    public sealed record AlcanceRequest(IReadOnlyList<Guid>? BankAccountPublicIds);

    public sealed record MotivoRequest(string? Reason);
}
