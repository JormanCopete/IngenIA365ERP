using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 005 (FR-037): toda cooperativa tiene un plan de nómina por defecto. Con un
/// solo plan las pantallas no preguntan; quien pague a un grupo con otra periodicidad
/// crea el segundo. Idempotente: si ya hay un plan por defecto, no toca nada.
///
/// <para>
/// Complementa la migración <c>NominaPlanesYPeriodos</c>, que rellena
/// <c>PayrollPlanId</c> en filas existentes; aquí se garantiza que las cooperativas
/// nuevas nazcan con el plan sin esperar a ninguna migración de datos.
/// </para>
/// </summary>
public sealed class PayrollPlansSeeder : IDataSeeder
{
    public int Order => 61;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;

        var hayPorDefecto = await db.PayrollPlans.IgnoreQueryFilters().AnyAsync(p => p.IsDefault, ct);
        if (hayPorDefecto) return 0;

        db.PayrollPlans.Add(new PayrollPlan
        {
            Code = PayrollPlan.DefaultCode,
            Name = "Nómina general",
            Periodicity = PayrollPeriodicity.Monthly,
            IsDefault = true,
            IsActive = true,
            CreatedBy = SeedContext.ParametricCreatedBy,
        });
        await db.SaveChangesAsync(ct);
        return 1;
    }
}
