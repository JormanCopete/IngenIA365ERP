using Carter;

namespace IngenIA365ERP.API.Endpoints.Payroll;

/// <summary>
/// Restos del cálculo preliminar de nómina, retirado en la feature 005.
///
/// <para>
/// Aquí vivían <c>POST /api/payroll/process/{periodId}</c> y
/// <c>POST /api/payroll/entries</c>: un cálculo con salud al 4 %, pensión al 4 %,
/// aportes del empleador al 8,5 % y 12 % fijos en el programa, salario dividido entre
/// 30 sin mirar ingresos ni retiros, sin auxilio de transporte, sin provisiones, sin
/// tabla de retención y sin explicación de ningún valor. Ninguna nómina real lo usó.
/// Se eliminan sin alias: no tenían consumidores (las dos pantallas eran marcadores).
/// </para>
///
/// <para>
/// Las tres consultas que había (<c>summary</c>, <c>detail</c>, <c>payslip</c>) se
/// redirigen (308) a las rutas nuevas de corridas, que son las que leen las tablas
/// nuevas. El alias se retira en la versión siguiente (contracts/api.md §7).
/// </para>
/// </summary>
public class PayrollProcessingEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll")
            .WithTags("PayrollProcessing (alias retirados)")
            .RequireAuthorization();

        group.MapGet("/summary/{periodId:guid}", (Guid periodId) =>
                Results.Redirect($"/api/payroll/pay-periods/{periodId}/runs/current", permanent: true, preserveMethod: true))
            .WithName("GetPayrollSummary_Alias");

        group.MapGet("/detail/{periodId:guid}", (Guid periodId, Guid? employeeId) =>
                Results.Redirect(
                    employeeId is null
                        ? $"/api/payroll/pay-periods/{periodId}/runs/current"
                        : $"/api/payroll/pay-periods/{periodId}/runs/current?employeeId={employeeId}",
                    permanent: true, preserveMethod: true))
            .WithName("GetPayrollDetail_Alias");

        group.MapGet("/payslip/{periodId:guid}/{employeeId:guid}", (Guid periodId, Guid employeeId) =>
                Results.Redirect($"/api/payroll/pay-periods/{periodId}/runs/current/payslips/{employeeId}", permanent: true, preserveMethod: true))
            .WithName("GetPayslip_Alias");
    }
}
