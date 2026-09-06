using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Novelties;

/// <summary>
/// FR-003: los días de una novedad con fechas que cruzan el fin del período quedan
/// registrados y aparecen automáticamente en el período siguiente. Este servicio crea
/// esa novedad «trasladada» (<c>Origin = CarryOver</c>, <c>CarriedFromNoveltyId</c>) en el
/// siguiente período abierto del plan si ya existe, y la crea después, al abrir el
/// período, si todavía no existía. Encadena: una incapacidad de 45 días cruza dos
/// períodos y produce dos traslados. No guarda: el comando que la invoca guarda.
/// </summary>
public sealed class CarryOverNoveltiesService(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
{
    /// <summary>Crea el traslado de <paramref name="novelty"/> en el siguiente período abierto, si lo hay. Devuelve cuántas novedades creó.</summary>
    public async Task<int> CreateCarryOverAsync(PayrollNovelty novelty, PayPeriod period, CancellationToken ct)
    {
        if (novelty.CarryOverDays <= 0 || novelty.EndDate is null) return 0;

        var siguiente = await SiguientePeriodoAbiertoAsync(period, ct);
        if (siguiente is null) return 0;

        var creada = Crear(novelty, siguiente);
        if (creada is null) return 0;
        db.PayrollNovelties.Add(creada);
        var total = 1;
        if (creada.CarryOverDays > 0)
            total += await CreateCarryOverAsync(creada, siguiente, ct);
        return total;
    }

    /// <summary>
    /// Al abrir un período: materializa los traslados pendientes de las novedades del
    /// período anterior del plan que cruzan hacia este y aún no tienen traslado.
    /// </summary>
    public async Task<int> MaterializePendingAsync(PayPeriod nuevo, CancellationToken ct)
    {
        var anterior = await db.PayPeriods.AsNoTracking()
            .Where(p => p.PayrollPlanId == nuevo.PayrollPlanId && p.EndDate < nuevo.StartDate)
            .OrderByDescending(p => p.EndDate)
            .FirstOrDefaultAsync(ct);
        if (anterior is null) return 0;

        var pendientes = await db.PayrollNovelties
            .Where(n => n.PayPeriodId == anterior.Id && n.Status == NoveltyStatus.Active && n.CarryOverDays > 0
                        && n.EndDate != null && n.EndDate >= nuevo.StartDate
                        && !db.PayrollNovelties.Any(h => h.CarriedFromNoveltyId == n.Id && h.Status == NoveltyStatus.Active))
            .ToListAsync(ct);

        var total = 0;
        foreach (var n in pendientes)
        {
            var creada = Crear(n, nuevo);
            if (creada is null) continue;
            db.PayrollNovelties.Add(creada);
            total++;
        }
        return total;
    }

    /// <summary>Anula los traslados activos que nacieron de <paramref name="novelty"/> en períodos aún editables (al corregir o anular la origen).</summary>
    public async Task<int> CancelCarryOversAsync(PayrollNovelty novelty, string reason, CancellationToken ct)
    {
        var hijos = await db.PayrollNovelties
            .Include(h => h.PayPeriod)
            .Where(h => h.CarriedFromNoveltyId == novelty.Id && h.Status == NoveltyStatus.Active)
            .ToListAsync(ct);
        var total = 0;
        foreach (var h in hijos)
        {
            if (h.PayPeriod is { Status: PayPeriodStatus.Approved or PayPeriodStatus.Reversed }) continue;
            h.Status = NoveltyStatus.Cancelled;
            h.StatusReason = reason;
            h.UpdatedAt = clock.UtcNow;
            h.UpdatedBy = user.UserName;
            total++;
            total += await CancelCarryOversAsync(h, reason, ct);
        }
        return total;
    }

    private async Task<PayPeriod?> SiguientePeriodoAbiertoAsync(PayPeriod period, CancellationToken ct) =>
        await db.PayPeriods
            .Where(p => p.PayrollPlanId == period.PayrollPlanId && p.StartDate > period.EndDate
                        && (p.Status == PayPeriodStatus.Open || p.Status == PayPeriodStatus.Calculated))
            .OrderBy(p => p.StartDate)
            .FirstOrDefaultAsync(ct);

    private PayrollNovelty? Crear(PayrollNovelty origen, PayPeriod destino)
    {
        var desde = destino.StartDate.Date;
        var hasta = origen.EndDate!.Value.Date;
        if (hasta < desde) return null;

        var total = CalendarConventions.Days(desde, hasta);
        var enPeriodo = CalendarConventions.Days(desde, hasta < destino.EndDate.Date ? hasta : destino.EndDate.Date);

        return new PayrollNovelty
        {
            PayPeriodId = destino.Id,
            EmployeeId = origen.EmployeeId,
            ConceptDefinitionId = origen.ConceptDefinitionId,
            ConceptCode = origen.ConceptCode,
            StartDate = desde,
            EndDate = hasta,
            DaysInPeriod = enPeriodo,
            CarryOverDays = Math.Max(0, total - enPeriodo),
            Notes = $"Traslado de la novedad del {origen.StartDate:dd/MM/yyyy} al {origen.EndDate:dd/MM/yyyy}" +
                    (string.IsNullOrWhiteSpace(origen.Notes) ? string.Empty : $": {origen.Notes}"),
            Status = NoveltyStatus.Active,
            Origin = NoveltyOrigin.CarryOver,
            CarriedFromNoveltyId = origen.Id,
            CreatedAt = clock.UtcNow,
            CreatedBy = user.UserName,
        };
    }
}
