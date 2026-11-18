using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Periods;

/// <summary>
/// Lo que Inventario ofrece al cierre del mes contable (2026-11-18, <c>Contabilidad.CostoDeVenta</c>): si quedan costos diferidos de ese mes
/// —o de uno anterior— que el cierre del mes de inventario todavía no envió, el mes contable no cierra: el comprobante del costo llegaría a
/// un mes cerrado. Se pregunta por <see cref="IEstadoOperativoParaElCierre"/>, como a Recaudo y Tesorería, sin que Contabilidad lea tablas de
/// Inventario. Sólo lee.
/// </summary>
public sealed class CostoPendienteParaElCierre(IApplicationDbContext db) : IEstadoOperativoParaElCierre
{
    public const string Modulo = "INV";

    public const string Codigo = "Accounting.Period.InventoryCostPending";

    public async Task<IReadOnlyList<PendienteDeCierreDto>> PendientesAsync(int anio, int mes, CancellationToken ct)
    {
        var meses = await db.DeferredCosts.AsNoTracking()
            .Where(d => d.PeriodCostPostingId == null && (d.Year < anio || (d.Year == anio && d.Month <= mes)))
            .Select(d => new { d.Year, d.Month }).Distinct().ToListAsync(ct);
        if (meses.Count == 0) return [];

        var lista = string.Join(", ", meses.OrderBy(m => m.Year).ThenBy(m => m.Month).Select(m => $"{m.Year:0000}-{m.Month:00}"));
        return
        [
            new PendienteDeCierreDto(Modulo, Codigo,
                $"El costo de ventas de inventario de {lista} se contabiliza al cierre del mes de inventario y todavía no se ha enviado: " +
                "cierre el mes en Inventario › Períodos y vuelva a intentarlo.",
                Blocks: true),
        ];
    }
}
