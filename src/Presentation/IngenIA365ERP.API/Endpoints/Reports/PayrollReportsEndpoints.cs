using Carter;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// Rutas heredadas de reportes de nómina. El comprobante de pago vive ahora en
/// <c>/api/payroll/runs/{runId}/payslips/{employeeId}/pdf</c> (feature 005, contracts/api.md §5)
/// y se emite sobre una liquidación aprobada, no sobre un período: la ruta vieja redirige con
/// 308 a la relación de pago de la corrida vigente del período. Resumen y certificado laboral
/// siguen sin implementar y responden 404 con mensaje, como antes.
/// </summary>
public class PayrollReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/payroll")
            .WithTags("Payroll Reports")
            .RequireAuthorization();

        group.MapGet("/payslip/{employeeId:guid}/{periodId:guid}/pdf", (Guid employeeId, Guid periodId) =>
                Results.Redirect($"/api/payroll/pay-periods/{periodId}/runs/current", permanent: true, preserveMethod: true))
            .WithName("GetPayslipPdf");

        group.MapGet("/summary/{periodId:guid}/pdf", (Guid periodId) =>
                Results.Redirect($"/api/payroll/pay-periods/{periodId}/runs/current", permanent: true, preserveMethod: true))
            .WithName("GetPayrollSummaryPdf");

        group.MapGet("/employment-certificate/{employeeId:guid}/pdf", (Guid employeeId) =>
                Results.NotFound(new { message = "Employment certificate query not yet implemented" }))
            .WithName("GetEmploymentCertificatePdf");
    }
}
