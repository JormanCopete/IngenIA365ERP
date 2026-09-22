using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>
/// Feature 010, US1 (contracts/api.md §3.1): la prima de servicios del semestre bajo
/// <c>/api/payroll/settlements/service-bonus</c>. Sólo lo que la nómina ordinaria no tiene:
/// listar, calcular por año y semestre, recalcular, aprobar, reversar y descartar, cada tramo con
/// su permiso <c>Payroll.ServiceBonus.*</c>. Detalle por empleado, relación de pago, comprobantes,
/// cuadre y exportación son las rutas de la 005 sobre <c>/api/payroll/runs/{runId}</c> (§2).
/// Los endpoints sólo reenvían al <c>ISender</c> (Principio III).
/// </summary>
public sealed class ServiceBonusEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/settlements/service-bonus")
            .WithTags("PayrollServiceBonus")
            .RequireAuthorization();

        group.MapGet("/", async (int? year, string? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListServiceBonusRunsQuery(year, status), ct))
            .WithName("Payroll_ServiceBonus_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.ServiceBonus.View");

        // Los excluidos no se guardan con la corrida: la pestaña «Excluidos» los vuelve a derivar (lectura).
        group.MapGet("/{runId:guid}/excluded", async (Guid runId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetServiceBonusExclusionsQuery(runId), ct))
            .WithName("Payroll_ServiceBonus_Excluded")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.ServiceBonus.View");

        group.MapPost("/", async (CalculateServiceBonusCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/runs/{result.Value.RunPublicId}", result.Value) : (object)result;
            })
            .WithName("Payroll_ServiceBonus_Calculate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.ServiceBonus.Calculate");

        group.MapPost("/{runId:guid}/recalculate", async (Guid runId, RecalculateBody? body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RecalculateServiceBonusCommand(runId, body?.EmployeePublicIds), ct);
                return result.IsSuccess ? Results.Created($"/api/payroll/runs/{result.Value.RunPublicId}", result.Value) : (object)result;
            })
            .WithName("Payroll_ServiceBonus_Recalculate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.ServiceBonus.Calculate");

        group.MapPost("/{runId:guid}/approve", async (Guid runId, ApproveBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApproveServiceBonusCommand(runId, body.Confirm, body.PostingDate, body.ConfirmEmpty, body.ConfirmWithoutSegregation), ct))
            .WithName("Payroll_ServiceBonus_Approve")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.ServiceBonus.Approve");

        group.MapPost("/{runId:guid}/reverse", async (Guid runId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReverseServiceBonusCommand(runId, body.Reason), ct))
            .WithName("Payroll_ServiceBonus_Reverse")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.ServiceBonus.Reverse");

        // Descartar el borrador lleva el permiso de calcular: quien puede crearlo puede desecharlo (§3.1).
        group.MapPost("/{runId:guid}/discard", async (Guid runId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardServiceBonusCommand(runId, body.Reason), ct))
            .WithName("Payroll_ServiceBonus_Discard")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.ServiceBonus.Calculate");
    }

    public sealed record RecalculateBody(IReadOnlyList<Guid>? EmployeePublicIds);

    public sealed record ApproveBody(bool Confirm, DateOnly? PostingDate = null, bool ConfirmEmpty = false, bool ConfirmWithoutSegregation = false);

    public sealed record ReasonBody(string Reason);
}
