using Carter;
using IngenIA365ERP.Application.Payroll.PayrollProcessing.Commands.ProcessPayroll;
using IngenIA365ERP.Application.Payroll.PayrollProcessing.Commands.RegisterPayrollEntry;
using IngenIA365ERP.Application.Payroll.PayrollProcessing.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Payroll;

public class PayrollProcessingEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll")
            .WithTags("PayrollProcessing")
            .RequireAuthorization();

        // Payroll entries (novedades)
        group.MapPost("/entries", async (RegisterPayrollEntryCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/payroll/entries/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("RegisterPayrollEntry");

        // Process payroll (liquidar)
        group.MapPost("/process/{periodId:guid}", async (Guid periodId, ISender sender) =>
        {
            var result = await sender.Send(new ProcessPayrollCommand(periodId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ProcessPayroll");

        // Payroll summary
        group.MapGet("/summary/{periodId:guid}", async (Guid periodId, ISender sender) =>
        {
            var result = await sender.Send(new ListPayrollSummaryQuery(periodId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPayrollSummary");

        // Payroll detail (all employees or one)
        group.MapGet("/detail/{periodId:guid}", async (Guid periodId, Guid? employeeId, ISender sender) =>
        {
            var result = await sender.Send(new GetPayrollDetailQuery(periodId, employeeId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPayrollDetail");

        // Payslip for one employee
        group.MapGet("/payslip/{periodId:guid}/{employeeId:guid}",
            async (Guid periodId, Guid employeeId, ISender sender) =>
        {
            var result = await sender.Send(new GetPayslipQuery(periodId, employeeId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPayslip");
    }
}
