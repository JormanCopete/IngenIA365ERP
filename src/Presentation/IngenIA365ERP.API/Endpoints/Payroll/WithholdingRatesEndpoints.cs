using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.WithholdingRates;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>
/// Feature 010 (US7, contracts/api.md §6): porcentaje fijo de retención del procedimiento 2.
/// Calcular por semestre, ver la explicación mes a mes, aprobar (por ítem o en lote: cierra la
/// vigencia anterior y abre la nueva) y rechazar. Permiso por tramo.
/// </summary>
public sealed class WithholdingRatesEndpoints : ICarterModule
{
    public sealed record CalculateBody(short Year, byte Semester, IReadOnlyList<Guid>? EmployeePublicIds);
    public sealed record ApproveBatchBody(IReadOnlyList<Guid> CalculationPublicIds);
    public sealed record RejectBody(string Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/withholding-rates")
            .WithTags("PayrollWithholdingRates")
            .RequireAuthorization();

        group.MapGet("/", async (int? year, int? semester, string? status, Guid? employeeId, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListWithholdingRateCalculationsQuery(year is { } y ? (short)y : null, semester is { } s ? (byte)s : null,
                    Enum.TryParse<WithholdingRateCalculationStatus>(status, true, out var st) ? st : null, employeeId), ct))
            .WithName("Payroll_WithholdingRates_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.WithholdingRate.View");

        group.MapPost("/calculate", async (CalculateBody body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CalculateWithholdingRatesCommand(body.Year, body.Semester, body.EmployeePublicIds), ct);
                return result.IsSuccess ? Results.Created("/api/payroll/withholding-rates", result.Value) : (object)result;
            })
            .WithName("Payroll_WithholdingRates_Calculate").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.WithholdingRate.Calculate");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetWithholdingRateCalculationQuery(id), ct))
            .WithName("Payroll_WithholdingRates_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.WithholdingRate.View");

        group.MapPost("/{id:guid}/approve", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new ApproveWithholdingRateCommand(id), ct))
            .WithName("Payroll_WithholdingRates_Approve").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.WithholdingRate.Approve");

        group.MapPost("/approve", async (ApproveBatchBody body, ISender sender, CancellationToken ct) => await sender.Send(new ApproveWithholdingRatesBatchCommand(body.CalculationPublicIds), ct))
            .WithName("Payroll_WithholdingRates_ApproveBatch").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.WithholdingRate.Approve");

        group.MapPost("/{id:guid}/reject", async (Guid id, RejectBody body, ISender sender, CancellationToken ct) => await sender.Send(new RejectWithholdingRateCommand(id, body.Reason), ct))
            .WithName("Payroll_WithholdingRates_Reject").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission("Payroll.WithholdingRate.Approve");
    }
}
