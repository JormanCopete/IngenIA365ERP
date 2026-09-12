using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Las cinco clases de riesgo ARL (Decreto 1772 de 1994, art. 13) en
/// <c>PAY_WorkRiskRates</c>, que es la tabla que la ficha del empleado referencia por
/// <c>WorkRiskRateId</c>. Sin estas filas la pantalla no tiene qué ofrecer y toda
/// liquidación queda bloqueada con «Sin clase de riesgo ARL registrada en la ficha»
/// —fue lo que pasó en QA el 2026-09-11.
///
/// <para>
/// El motor <b>no</b> lee la tarifa de aquí: la clase es el <c>Code</c> (1..5) y el
/// porcentaje vigente vive en <c>ARL_CLASE_{I..V}_PCT</c> de los parámetros legales,
/// con vigencia. La columna <c>Rate</c> se rellena con el mismo valor inicial de ese
/// catálogo —una sola fuente— y es informativa: cambiarla no cambia ninguna nómina.
/// Idempotente por <c>Code</c>; nunca actualiza lo existente.
/// </para>
/// </summary>
public sealed class WorkRiskClassesSeeder : IDataSeeder
{
    public int Order => 73;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public static IReadOnlyList<WorkRiskRate> Catalogo()
    {
        var porcentajes = PayrollLegalParametersSeeder.Catalogo().ToDictionary(p => p.Code, p => p.Value ?? 0m);
        return
        [
            Clase(1, "I", "Riesgo mínimo", porcentajes),
            Clase(2, "II", "Riesgo bajo", porcentajes),
            Clase(3, "III", "Riesgo medio", porcentajes),
            Clase(4, "IV", "Riesgo alto", porcentajes),
            Clase(5, "V", "Riesgo máximo", porcentajes),
        ];
    }

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var existentes = await db.WorkRiskRates.IgnoreQueryFilters().Select(r => r.Code).ToListAsync(ct);

        var insertadas = 0;
        foreach (var clase in Catalogo())
        {
            if (existentes.Contains(clase.Code)) continue;
            db.WorkRiskRates.Add(clase);
            insertadas++;
        }
        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }

    private static WorkRiskRate Clase(int codigo, string romano, string descripcion, IReadOnlyDictionary<string, decimal> porcentajes) => new()
    {
        Code = codigo,
        Name = $"Clase {romano} - {descripcion}",
        ShortName = $"Clase {romano}",
        Rate = porcentajes[LegalParameterCodes.WorkRiskPct(codigo)],
        CreatedBy = SeedContext.ParametricCreatedBy,
    };
}
