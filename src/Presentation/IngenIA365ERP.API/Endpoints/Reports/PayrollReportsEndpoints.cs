using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using MediatR;

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

        // ---- Feature 006: centro de reportes. format = json (pantalla) | xlsx | pdf | docx ----
        // La entrega (JSON o archivo) es la misma que usa contabilidad: EntregaDeInformes (feature 009).
        group.MapGet("/comprobante", async (Guid runId, Guid employeeId, string? format, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new ComprobantePorEmpleadoReportQuery(runId, employeeId), ct), format, "comprobante-nomina"))
            .WithName("Reportes_Nomina_Comprobante").RequirePermission("Payroll.Runs.View");

        group.MapGet("/resumen", async (Guid runId, string? format, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new ResumenCorridaReportQuery(runId), ct), format, "resumen-corrida"))
            .WithName("Reportes_Nomina_Resumen").RequirePermission("Payroll.Runs.View");

        group.MapGet("/detalle", async (Guid runId, string? format, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new DetalleEmpleadoConceptoReportQuery(runId), ct), format, "detalle-empleado-concepto"))
            .WithName("Reportes_Nomina_Detalle").RequirePermission("Payroll.Runs.View");

        group.MapGet("/novedades", async (Guid periodId, bool? incluirAnuladas, string? format, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new NovedadesPeriodoReportQuery(periodId, incluirAnuladas ?? true), ct), format, "novedades-periodo"))
            .WithName("Reportes_Nomina_Novedades").RequirePermission("Payroll.Runs.View");

        group.MapGet("/historico", async (Guid employeeId, DateTime desde, DateTime hasta, string? format, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new HistoricoEmpleadoReportQuery(employeeId, desde, hasta), ct), format, "historico-empleado"))
            .WithName("Reportes_Nomina_Historico").RequirePermission("Payroll.Runs.View");
    }
}
