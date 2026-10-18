using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Withdrawals;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// El retiro del asociado (feature 014, US10, T105; contracts/api.md §11; FR-076 a FR-080, FR-124), sobre <see cref="LendingGroup"/>:
/// <c>/api/lending/membership-withdrawals</c> —listar y ver, radicar y desistir (<c>Lending.MembershipWithdrawals.File</c>), aprobar o negar con el punto del
/// acta y aprobar la devolución pendiente (<c>.Approve</c>), la vista previa de la liquidación sin clave y liquidar (<c>.Settle</c>)—. Toda escritura con
/// <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class MembershipWithdrawalsEndpoints : ICarterModule
{
    public const string Radicar = "Lending.MembershipWithdrawals.File";
    public const string Aprobar = "Lending.MembershipWithdrawals.Approve";
    public const string Liquidar = "Lending.MembershipWithdrawals.Settle";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var retiros = LendingGroup.Grupo(app, "/membership-withdrawals", "Cartera Retiro del asociado");

        retiros.MapGet("/", async (MembershipWithdrawalStatus? status, MembershipWithdrawalCause? cause, Guid? personPublicId, int? page, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new ListMembershipWithdrawalsQuery(status, cause, personPublicId, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_MembershipWithdrawals_List")
            .RequirePermission(new AlgunPermiso(Radicar, Aprobar, Liquidar));

        retiros.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMembershipWithdrawalQuery(id), ct))
            .WithName("Lending_MembershipWithdrawals_Get")
            .RequirePermission(new AlgunPermiso(Radicar, Aprobar, Liquidar));

        retiros.MapPost("/", async (RadicacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new FileMembershipWithdrawalCommand(body.PersonPublicId, body.Cause, body.CauseDetail, body.RequestedOn,
                    body.SupportAttachmentPublicIds, body.Note) { OperationKey = http.ClaveDeOperacion() }, ct),
                    r => $"/api/lending/membership-withdrawals/{r.PublicId}"))
            .WithName("Lending_MembershipWithdrawals_File")
            .ConClaveDeOperacion()
            .RequirePermission(Radicar);

        retiros.MapPost("/{id:guid}/desist", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DesistMembershipWithdrawalCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_MembershipWithdrawals_Desist")
            .ConClaveDeOperacion()
            .RequirePermission(Radicar);

        retiros.MapPost("/{id:guid}/approve", async (Guid id, DecisionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DecideMembershipWithdrawalCommand(id, DecisionSobreRetiro.Approve, body.SessionItemPublicId, body.EffectiveDate,
                    body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_MembershipWithdrawals_Approve")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);

        retiros.MapPost("/{id:guid}/reject", async (Guid id, DecisionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DecideMembershipWithdrawalCommand(id, DecisionSobreRetiro.Reject, body.SessionItemPublicId, null,
                    body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_MembershipWithdrawals_Reject")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);

        retiros.MapPost("/{id:guid}/settlement/preview", async (Guid id, VistaPreviaRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewMembershipWithdrawalSettlementQuery(id, body.Date, body.LossDeduction), ct))
            .WithName("Lending_MembershipWithdrawals_PreviewSettlement")
            .RequirePermission(Liquidar);

        retiros.MapPost("/{id:guid}/settle", async (Guid id, LiquidacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SettleMembershipWithdrawalCommand(id, body.Date, body.ExpectedPreviewSha256 ?? string.Empty, body.LossDeduction,
                    body.Refund ?? new MembershipRefundInput(FormaDeDevolucionDeAportes.DisbursementOrder), body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_MembershipWithdrawals_Settle")
            .ConClaveDeOperacion()
            .RequirePermission(Liquidar);

        retiros.MapPost("/{id:guid}/refund/approve", async (Guid id, AprobarDevolucionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApproveMembershipRefundCommand(id, body.ApprovalPublicId, body.Payee, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_MembershipWithdrawals_ApproveRefund")
            .ConClaveDeOperacion()
            .RequirePermission(Aprobar);
    }

    /// <summary>El cuerpo de <c>POST /</c>: el asociado, la causa (y su detalle si es otra), la fecha de la solicitud, los soportes y una nota.</summary>
    public sealed record RadicacionRequest(Guid PersonPublicId, MembershipWithdrawalCause Cause, string? CauseDetail, DateOnly? RequestedOn,
        IReadOnlyList<Guid>? SupportAttachmentPublicIds, string? Note);

    /// <summary>El cuerpo de <c>/approve</c> y <c>/reject</c>: el punto del acta cerrada, la fecha del retiro (al aprobar) y el motivo.</summary>
    public sealed record DecisionRequest(Guid SessionItemPublicId, DateOnly? EffectiveDate, string? Reason);

    /// <summary>El cuerpo de la vista previa: la fecha y, si el acta lo dispuso, el descuento por pérdidas.</summary>
    public sealed record VistaPreviaRequest(DateOnly? Date, LossDeductionInput? LossDeduction);

    /// <summary>El cuerpo de <c>/settle</c>: la fecha, la huella de la vista previa, el descuento por pérdidas, la forma de la devolución y el motivo.</summary>
    public sealed record LiquidacionRequest(DateOnly? Date, string? ExpectedPreviewSha256, LossDeductionInput? LossDeduction, MembershipRefundInput? Refund,
        string? Reason);

    /// <summary>El cuerpo de <c>/refund/approve</c>: la aprobación con monto (US4), el beneficiario bancario y el motivo.</summary>
    public sealed record AprobarDevolucionRequest(Guid? ApprovalPublicId, PayeeBankInput? Payee, string? Reason);

    /// <summary>El motivo de un desistimiento.</summary>
    public sealed record MotivoRequest(string? Reason);
}
