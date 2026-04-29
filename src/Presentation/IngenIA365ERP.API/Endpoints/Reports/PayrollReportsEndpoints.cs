using Carter;
using IngenIA365ERP.API.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

public class PayrollReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/payroll")
            .WithTags("Payroll Reports")
            .RequireAuthorization();

        // Payslip PDF
        group.MapGet("/payslip/{employeeId:guid}/{periodId:guid}/pdf", async (Guid employeeId, Guid periodId, ISender sender) =>
        {
            // TODO: Query employee payroll entries for the period, build PayslipDto, generate PDF
            // var dto = new PayslipDto(...);
            // var pdf = PayslipReport.Generate(dto);
            // return Results.File(pdf, "application/pdf", $"Nomina_{dto.EmployeeNit}_{dto.PeriodName}.pdf");
            return Results.NotFound(new { message = "Payslip query not yet implemented" });
        }).WithName("GetPayslipPdf");

        // Payroll Summary PDF
        group.MapGet("/summary/{periodId:guid}/pdf", async (Guid periodId, ISender sender) =>
        {
            // TODO: Query all payroll entries for the period, build PayrollSummaryDto, generate PDF
            // var dto = new PayrollSummaryDto(...);
            // var pdf = PayrollSummaryReport.Generate(dto);
            // return Results.File(pdf, "application/pdf", $"ResumenNomina_{dto.PeriodName}.pdf");
            return Results.NotFound(new { message = "Payroll summary query not yet implemented" });
        }).WithName("GetPayrollSummaryPdf");

        // Employment Certificate PDF
        group.MapGet("/employment-certificate/{employeeId:guid}/pdf", async (Guid employeeId, ISender sender) =>
        {
            // TODO: Query employee + company data, build EmploymentCertificateDto, generate PDF
            // var dto = new EmploymentCertificateDto(...);
            // var pdf = EmploymentCertificateReport.Generate(dto);
            // return Results.File(pdf, "application/pdf", $"CertLaboral_{dto.EmployeeNit}.pdf");
            return Results.NotFound(new { message = "Employment certificate query not yet implemented" });
        }).WithName("GetEmploymentCertificatePdf");
    }
}
