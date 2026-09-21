using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Settlements.Severance;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>
/// Feature 010 US2 (contracts/api.md §3.2): cesantías e intereses del año —calcular, recalcular,
/// aprobar, reversar y descartar— y la consignación por fondo (relación, archivo plano y marca de
/// consignado). Permiso propio por tramo (<c>Payroll.Severance.*</c>); los endpoints sólo reenvían al
/// <c>ISender</c>. Lo que la corrida comparte con la ordinaria (detalle por empleado, relación de pago
/// de los intereses, comprobantes, cuadre, exportación) sigue en <c>/api/payroll/runs/{runId}</c>.
/// </summary>
public sealed class SeveranceEndpoints : ICarterModule
{
    public sealed record ApproveBody(bool Confirm, DateOnly? PostingDate, DateOnly? PayDate, bool ConfirmEmpty = false, bool ConfirmWithoutSegregation = false);
    public sealed record ReasonBody(string Reason);
    public sealed record MarkDepositedBody(DateOnly DepositedAt, string? Reference);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/settlements/severance")
            .WithTags("PayrollSeverance")
            .RequireAuthorization();

        group.MapGet("/", async (int? year, string? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListSeveranceRunsQuery(year, status), ct))
            .WithName("Payroll_Severance_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.View");

        group.MapPost("/", async (CalculateSeveranceCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/runs/{result.Value.RunPublicId}", result.Value) : (object)result;
            })
            .WithName("Payroll_Severance_Calculate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.Calculate");

        group.MapPost("/{runId:guid}/recalculate", async (Guid runId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RecalculateSeveranceCommand(runId), ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/runs/{result.Value.RunPublicId}", result.Value) : (object)result;
            })
            .WithName("Payroll_Severance_Recalculate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.Calculate");

        group.MapPost("/{runId:guid}/approve", async (Guid runId, ApproveBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApproveSeveranceCommand(runId, body.Confirm, body.PostingDate, body.PayDate, body.ConfirmEmpty, body.ConfirmWithoutSegregation), ct))
            .WithName("Payroll_Severance_Approve")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.Approve");

        group.MapPost("/{runId:guid}/reverse", async (Guid runId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReverseSeveranceCommand(runId, body.Reason), ct))
            .WithName("Payroll_Severance_Reverse")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.Reverse");

        group.MapPost("/{runId:guid}/discard", async (Guid runId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardSeveranceCommand(runId, body.Reason), ct))
            .WithName("Payroll_Severance_Discard")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.Calculate");

        group.MapGet("/{runId:guid}/deposit-schedule", async (Guid runId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetDepositScheduleQuery(runId), ct))
            .WithName("Payroll_Severance_DepositSchedule")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.View");

        group.MapGet("/{runId:guid}/deposit-schedule/{fundId:guid}/file", async (Guid runId, Guid fundId, Guid? formatId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetFundDepositFileQuery(runId, fundId, formatId), ct);
                return result.IsSuccess ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : (object)result;
            })
            .WithName("Payroll_Severance_FundDepositFile")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.View");

        group.MapPost("/{runId:guid}/funds/{fundId:guid}/mark-deposited", async (Guid runId, Guid fundId, MarkDepositedBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new MarkFundDepositedCommand(runId, fundId, body.DepositedAt, body.Reference), ct))
            .WithName("Payroll_Severance_MarkDeposited")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.MarkDeposited");
    }
}
