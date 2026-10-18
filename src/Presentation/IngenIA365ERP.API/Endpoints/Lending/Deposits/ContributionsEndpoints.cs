using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Los aportes sociales (feature 014, US3, T093; contracts/api.md §7), sobre <see cref="LendingGroup"/>: <c>/api/lending/contributions</c> —el resumen de
/// una persona, las cuotas exigibles, las devoluciones sin retiro y las cesiones entre asociados con su aprobación por otra persona y su rechazo—. Leer
/// exige <c>Lending.Accounts.Read</c>; pedir, aprobar y negar, <c>Lending.Contributions.Manage</c> con <c>Idempotency-Key</c>. La cuota la genera el
/// ciclo diario y la pagan los recaudadores por el contrato de cuentas: aquí no hay ruta de pago. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ContributionsEndpoints : ICarterModule
{
    public const string Leer = MemberAccountsEndpoints.Leer;
    public const string Administrar = "Lending.Contributions.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var aportes = LendingGroup.Grupo(app, "/contributions", "Cartera Aportes");

        aportes.MapGet("/summary", async (Guid personPublicId, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetContributionsSummaryQuery(personPublicId, date), ct))
            .WithName("Lending_Contributions_Summary")
            .RequirePermission(Leer);

        aportes.MapGet("/installments", async (Guid? personPublicId, EstadoDeCuotaDeCuenta? status, string? period, string? line, int? page, int? pageSize,
                    ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAccountInstallmentsQuery(personPublicId, status, period, line, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Contributions_Installments")
            .RequirePermission(Leer);

        // --- devoluciones sin retiro (FR-043) ---
        aportes.MapGet("/refunds", async (ContributionRequestStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListContributionRequestsQuery(ContributionRequestKind.Refund, status, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Contributions_Refunds")
            .RequirePermission(Leer);

        aportes.MapPost("/refunds", async (DevolucionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RequestContributionRefundCommand(body.AccountPublicId, body.Amount, body.Cause, body.SessionItemPublicId,
                    body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct), s => $"/api/lending/contributions/refunds/{s.PublicId}"))
            .WithName("Lending_Contributions_RequestRefund")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        aportes.MapPost("/refunds/{refundId:guid}/approve", async (Guid refundId, AprobarDevolucionRequest body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new ApproveContributionRefundCommand(refundId, body.Form, body.Payee, body.TargetAccountPublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Contributions_ApproveRefund")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        aportes.MapPost("/refunds/{refundId:guid}/reject", async (Guid refundId, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RejectContributionRequestCommand(refundId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Contributions_RejectRefund")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        // --- cesiones entre asociados (FR-048) ---
        aportes.MapGet("/assignments", async (ContributionRequestStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListContributionRequestsQuery(ContributionRequestKind.Assignment, status, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Contributions_Assignments")
            .RequirePermission(Leer);

        aportes.MapPost("/assignments", async (CesionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RequestContributionAssignmentCommand(body.FromAccountPublicId, body.ToPersonPublicId, body.Amount,
                    body.SessionItemPublicId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct),
                    s => $"/api/lending/contributions/assignments/{s.PublicId}"))
            .WithName("Lending_Contributions_RequestAssignment")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        aportes.MapPost("/assignments/{assignmentId:guid}/approve", async (Guid assignmentId, MotivoRequest body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new ApproveContributionAssignmentCommand(assignmentId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Contributions_ApproveAssignment")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        aportes.MapPost("/assignments/{assignmentId:guid}/reject", async (Guid assignmentId, MotivoRequest body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new RejectContributionRequestCommand(assignmentId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Contributions_RejectAssignment")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo de <c>POST /refunds</c>: la cuenta de aportes, el valor, la causa, el punto del acta que lo aprueba y el motivo.</summary>
    public sealed record DevolucionRequest(Guid AccountPublicId, decimal Amount, ContributionRefundCause Cause, Guid? SessionItemPublicId, string? Reason);

    /// <summary>El cuerpo de la aprobación de una devolución: la forma (orden de giro en K7), el beneficiario bancario y el motivo.</summary>
    public sealed record AprobarDevolucionRequest(FormaDeDevolucionDeAportes Form, PayeeBankInput? Payee, Guid? TargetAccountPublicId, string? Reason);

    /// <summary>El cuerpo de <c>POST /assignments</c>: la cuenta que cede, la persona que recibe, el valor, el punto del acta y el motivo.</summary>
    public sealed record CesionRequest(Guid FromAccountPublicId, Guid ToPersonPublicId, decimal Amount, Guid? SessionItemPublicId, string? Reason);

    /// <summary>El motivo de una aprobación o un rechazo.</summary>
    public sealed record MotivoRequest(string? Reason);
}
