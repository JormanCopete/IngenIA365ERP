using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Application.Treasury.Transfers;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// Los traslados entre cuentas propias y el cierre del mes de los cheques girados (feature 015, T1, fase 33, T0413; contracts/api.md §13; FR-104, FR-220): pedir, aprobar o
/// devolver, marcar enviado, confirmar con soporte o con la línea del extracto y anular lo no enviado; y correr el cierre del mes de los cheques de una cuenta. Las rutas
/// <c>/{id}/reject</c>, <c>/{id}/sent</c>, <c>/{id}/confirm</c>, <c>/{id}/void</c> y <c>GET /month-end/checks</c> son (nuevo), DT §2.7. (nuevo)
/// </summary>
public class TransfersEndpoints : ICarterModule
{
    public const string Pedir = "Treasury.Transfers.Request";
    public const string Aprobar = "Treasury.Transfers.Approve";
    public const string VerChequeras = "Treasury.Checkbooks.View";
    public const string AdministrarChequeras = "Treasury.Checkbooks.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = TreasuryGroup.Grupo(app, "/transfers", "Tesorería Traslados");
        g.MapGet("/", async (TransferState? state, ISender s, CancellationToken ct) => await s.Send(new ListTransfersQuery(state), ct))
            .WithName("Treasury_Transfers_List").RequirePermission(new AlgunPermiso(Pedir, Aprobar));
        g.MapPost("/", async (TrasladoRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                TreasuryGroup.Creado(await s.Send(new RequestTransferCommand(body.FromBankAccountPublicId ?? Guid.Empty, body.ToBankAccountPublicId ?? Guid.Empty,
                    body.Amount ?? 0m, body.Reason ?? string.Empty, body.SupportAttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct),
                    t => $"/api/treasury/transfers/{t.PublicId}"))
            .WithName("Treasury_Transfers_Request").ConClaveDeOperacion().RequirePermission(Pedir);
        g.MapPost("/{id:guid}/approve", async (Guid id, DecisionRequest? body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ApproveTransferCommand(id, body?.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Transfers_Approve").ConClaveDeOperacion().RequirePermission(Aprobar);
        g.MapPost("/{id:guid}/reject", async (Guid id, DecisionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RejectTransferCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Transfers_Reject").ConClaveDeOperacion().RequirePermission(Aprobar);
        g.MapPost("/{id:guid}/sent", async (Guid id, EnvioRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new MarkTransferSentCommand(id, body.SentOn, body.BankReference, body.LiquidityFundUsePublicId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Transfers_Sent").ConClaveDeOperacion().RequirePermission(Pedir);
        g.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmacionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new ConfirmTransferCommand(id, body.SupportAttachmentPublicId, body.StatementLinePublicId, body.ConfirmedOn)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Treasury_Transfers_Confirm").ConClaveDeOperacion().RequirePermission(Pedir);
        g.MapPost("/{id:guid}/void", async (Guid id, DecisionRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new VoidTransferCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Transfers_Void").ConClaveDeOperacion().RequirePermission(Pedir);

        var m = TreasuryGroup.Grupo(app, "/month-end", "Tesorería Cierre del mes");
        m.MapGet("/checks", async (Guid? account, int? year, ISender s, CancellationToken ct) => await s.Send(new ListMonthEndCheckRunsQuery(account, year), ct))
            .WithName("Treasury_MonthEnd_Checks_List").RequirePermission(new AlgunPermiso(VerChequeras, AdministrarChequeras));
        m.MapPost("/checks", async (CierreDeChequesRequest body, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new RunMonthEndChecksCommand(body.BankAccountPublicId ?? Guid.Empty, body.Year ?? 0, body.Month ?? 0) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_MonthEnd_Checks_Run").ConClaveDeOperacion().RequirePermission(AdministrarChequeras);
    }

    public sealed record TrasladoRequest(Guid? FromBankAccountPublicId, Guid? ToBankAccountPublicId, decimal? Amount, string? Reason, Guid? SupportAttachmentPublicId);

    public sealed record DecisionRequest(string? Reason);

    public sealed record EnvioRequest(DateOnly? SentOn, string? BankReference, Guid? LiquidityFundUsePublicId);

    public sealed record ConfirmacionRequest(Guid? SupportAttachmentPublicId, Guid? StatementLinePublicId, DateOnly? ConfirmedOn);

    public sealed record CierreDeChequesRequest(Guid? BankAccountPublicId, int? Year, int? Month);
}
