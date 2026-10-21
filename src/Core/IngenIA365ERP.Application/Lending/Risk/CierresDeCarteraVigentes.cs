using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// Hasta qué día hay cierre de Cartera vigente (feature 013, US9, bloque 12C; FR-012, D16, US9-2): el último día del mes del cierre mensual vigente
/// más reciente —uno reabierto no cuenta; el base tampoco, porque lo anterior a su corte ya lo rechaza la regla del corte (T84)—. Reemplaza a
/// <see cref="SinCierresDeCartera"/>, el esqueleto de US5: desde aquí un pago con fecha valor en un mes cerrado exige
/// <c>Lending.Payments.BackdateBeforeClose</c> (<see cref="ReglasDeFechaValor"/>) y su efecto se reconoce en el primer mes sin cierre
/// (<c>FechaContable</c>). Hasta el bloque 12C el esqueleto seguía registrado y la regla no mordía aunque hubiera cierres. (nuevo)
/// </summary>
public sealed class CierresDeCarteraVigentes(IApplicationDbContext db) : ICierresDeCartera
{
    public Task<DateOnly?> CerradoHastaAsync(CancellationToken ct) =>
        db.MonthlyCloses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Kind == MonthlyCloseKind.Monthly
                        && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted))
            .MaxAsync(c => (DateOnly?)c.CloseDate, ct);
}
