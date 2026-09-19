using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_WithholdingParameters] (nom_parretfte): un tramo de la tabla de retención
/// en la fuente por salarios (art. 383 E.T.) <b>propio de un plan de nómina</b>. Desde el
/// 2026-09-19 (pedido del dueño) el tramo cuelga del plan y no de la «empresa nómina» del legado,
/// y la liquidación lo usa: si el plan del período tiene tramos, reemplazan a los de
/// <c>RETEFTE_TABLA_UVT</c> de Parámetros legales para ese plan; si no tiene, aplica la tabla
/// legal. Tramos en UVT, tarifa marginal en porcentaje sobre el exceso del inicio del tramo más
/// las UVT fijas (<see cref="AdditionalUvt"/>). <see cref="UvtRangeEnd"/> en 0 = «en adelante».
/// <see cref="PayrollCompanyId"/> es la empresa nómina del legado (siempre 1) y se conserva sólo
/// por el modelo heredado.
/// </summary>
public class WithholdingParameter : AuditableEntity
{
    public int PayrollCompanyId { get; set; } = 1;
    public int PayrollPlanId { get; set; }
    public int UvtRangeStart { get; set; }
    public int UvtRangeEnd { get; set; }
    /// <summary>Tarifa marginal en porcentaje (19 = 19 %).</summary>
    public decimal Rate { get; set; }
    public int AdditionalUvt { get; set; }

    public PayrollPlan? PayrollPlan { get; set; }

    /// <summary>Null si «en adelante» (0 en el legado).</summary>
    public int? UvtRangeEndOrNull => UvtRangeEnd <= 0 ? null : UvtRangeEnd;
}
