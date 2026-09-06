using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Payroll.Payments;
using IngenIA365ERP.Application.Payroll.Payslips;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>Feature 005 (contracts/api.md §5): relación de pago, marca de pago y comprobantes en PDF o por correo.</summary>
public sealed class PayrollPaymentsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll/runs/{runId:guid}")
            .WithTags("PayrollPayments")
            .RequireAuthorization();

        group.MapGet("/payments", async (Guid runId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPaymentRegisterQuery(runId), ct))
            .WithName("Payroll_Payments_Register")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Payments.View");

        group.MapPost("/payments", async (Guid runId, MarkBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new MarkPaymentsCommand(runId, body.EmployeePublicIds, body.PaidAt, body.Method, body.Reference), ct))
            .WithName("Payroll_Payments_Mark")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Payments.Mark");

        group.MapPost("/payments/{employeeId:guid}/revert", async (Guid runId, Guid employeeId, ReasonBody body, ISender sender, CancellationToken ct) =>
                await sender.Send(new RevertPaymentMarkCommand(runId, employeeId, body.Reason), ct))
            .WithName("Payroll_Payments_Revert")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Payments.Unmark");

        group.MapGet("/payslips/pdf", async (Guid runId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetAllPayslipsQuery(runId), ct);
                return result.IsSuccess ? Results.File(result.Value.Content, "application/pdf", result.Value.FileName) : (object)result;
            })
            .WithName("Payroll_Payslips_AllPdf")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Payslips.View");

        group.MapGet("/payslips/{employeeId:guid}/pdf", async (Guid runId, Guid employeeId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetPayslipQuery(runId, employeeId), ct);
                return result.IsSuccess ? Results.File(result.Value.Content, "application/pdf", result.Value.FileName) : (object)result;
            })
            .WithName("Payroll_Payslips_Pdf")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Payslips.View");

        group.MapPost("/payslips/send", async (Guid runId, SendBody? body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SendPayslipsCommand(runId, body?.EmployeePublicIds), ct))
            .WithName("Payroll_Payslips_Send")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Payslips.Send");

        group.MapGet("/payslips/deliveries", async (Guid runId, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPayslipDeliveriesQuery(runId), ct))
            .WithName("Payroll_Payslips_Deliveries")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Payslips.View");
    }

    public sealed record MarkBody(IReadOnlyList<Guid>? EmployeePublicIds, DateTime PaidAt, PayrollPaymentMethod Method, string? Reference);
    public sealed record ReasonBody(string Reason);
    public sealed record SendBody(IReadOnlyList<Guid>? EmployeePublicIds);
}
