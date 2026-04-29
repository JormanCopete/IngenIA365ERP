using Carter;
using IngenIA365ERP.Application.Lending.PayrollDeductions.Commands.ProcessPayrollDeduction;
using IngenIA365ERP.Application.Lending.PayrollDeductions.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class PayrollDeductionsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/payroll-deductions")
            .WithTags("LendingPayrollDeductions")
            .RequireAuthorization();

        group.MapPost("/{periodId:guid}", async (Guid periodId, ISender sender) =>
        {
            var result = await sender.Send(new ProcessPayrollDeductionCommand(periodId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ProcessPayrollDeduction");

        group.MapGet("/{periodId:guid}/preview", async (Guid periodId, ISender sender) =>
        {
            var result = await sender.Send(new GetPayrollDeductionPreviewQuery(periodId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetPayrollDeductionPreview");
    }
}
