using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Application.Payroll.Terminations;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>
/// Feature 010, US3 (contracts/api.md §3.4): terminación del contrato y liquidación definitiva.
/// Registrar crea la corrida <c>Settlement</c> en borrador; los descuentos se consultan y se bajan
/// con motivo; aprobar cierra la ficha y aplica los recaudos en Cartera; reversar la reabre. El
/// catálogo de motivos de retiro vive aquí con <c>Manage</c>. Los endpoints sólo reenvían al
/// <c>ISender</c>; cada tramo lleva su permiso.
/// </summary>
public sealed class TerminationsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/settlements/terminations")
            .WithTags("PayrollTerminations")
            .RequireAuthorization();

        // ------------------------------------------------------------ terminaciones --

        group.MapGet("/", async (int? year, TerminationStatus? status, Guid? employeeId, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTerminationsQuery(year, status, employeeId), ct))
            .WithName("Payroll_Terminations_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.View");

        group.MapPost("/", async (RegisterTerminationCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/payroll/settlements/terminations/{result.Value.RunPublicId}", result.Value)
                    : (object)result;
            })
            .WithName("Payroll_Terminations_Register")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.Calculate");

        // ---------------------------------------------------------------- descuentos --

        group.MapGet("/{runId:guid}/deductions", async (Guid runId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetSettlementDeductionsQuery(runId), ct))
            .WithName("Payroll_Terminations_Deductions")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.View");

        group.MapPut("/{runId:guid}/deductions/{obligationId:guid}", async (Guid runId, Guid obligationId, AdjustDeductionBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new AdjustSettlementDeductionCommand(runId, obligationId, body.Applied, body.Reason), ct))
            .WithName("Payroll_Terminations_AdjustDeduction")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.AdjustDeduction");

        // -------------------------------------------------------------------- ciclo --

        group.MapPost("/{runId:guid}/recalculate", async (Guid runId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RecalculateSettlementCommand(runId), ct);
                return result.IsSuccess
                    ? Results.Created($"/api/payroll/settlements/terminations/{result.Value.RunPublicId}", result.Value)
                    : (object)result;
            })
            .WithName("Payroll_Terminations_Recalculate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.Calculate");

        group.MapPost("/{runId:guid}/approve", async (Guid runId, ApproveBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApproveSettlementCommand(runId, body.Confirm, body.PostingDate, body.ConfirmWithoutSegregation), ct))
            .WithName("Payroll_Terminations_Approve")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.Approve");

        group.MapPost("/{runId:guid}/reverse", async (Guid runId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReverseSettlementCommand(runId, body.Reason), ct))
            .WithName("Payroll_Terminations_Reverse")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.Reverse");

        group.MapPost("/{runId:guid}/discard", async (Guid runId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardSettlementCommand(runId, body.Reason), ct))
            .WithName("Payroll_Terminations_Discard")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.Calculate");

        // ---------------------------------------------------------------- documento --

        group.MapGet("/{runId:guid}/document", async (Guid runId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetSettlementDocumentQuery(runId), ct);
                return result.IsSuccess
                    ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
                    : (object)result;
            })
            .WithName("Payroll_Terminations_Document")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.View");

        group.MapGet("/{runId:guid}/late-payment-penalty", async (Guid runId, DateOnly? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLatePaymentPenaltyQuery(runId, asOf), ct))
            .WithName("Payroll_Terminations_LatePaymentPenalty")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.View");

        // ------------------------------------------------------------------ motivos --

        group.MapGet("/reasons", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTerminationReasonsQuery(includeInactive ?? false), ct))
            .WithName("Payroll_TerminationReasons_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.View");

        group.MapPost("/reasons", async (CreateTerminationReasonCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/payroll/settlements/terminations/reasons/{result.Value}", new { publicId = result.Value })
                    : (object)result;
            })
            .WithName("Payroll_TerminationReasons_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.Manage");

        group.MapPut("/reasons/{id:guid}", async (Guid id, UpdateTerminationReasonCommand command, ISender sender, CancellationToken ct) =>
                await sender.Send(command.PublicId == id ? command : command with { PublicId = id }, ct))
            .WithName("Payroll_TerminationReasons_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.Manage");

        group.MapPost("/reasons/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeactivateTerminationReasonCommand(id), ct))
            .WithName("Payroll_TerminationReasons_Deactivate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.Manage");
    }

    public sealed record AdjustDeductionBody(decimal Applied, string? Reason);
    public sealed record ApproveBody(bool Confirm, DateOnly? PostingDate = null, bool ConfirmWithoutSegregation = false);
    public sealed record ReasonBody(string Reason);
}
