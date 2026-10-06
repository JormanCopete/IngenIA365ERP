using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Common.NationalValues;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Calendar;

/// <summary>
/// <see cref="ICalendarioHabil"/> sobre los <c>FESTIVO</c> replicados (feature 013, T40). Un festivo es una serie de una sola
/// fecha (clave = la fecha); cada clave se resuelve con <see cref="VigenciaBitemporal"/> para respetar una corrección. (nuevo)
/// </summary>
public sealed class CalendarioDeDiasHabiles(IApplicationDbContext db) : ICalendarioHabil
{
    public async Task<CalendarioHabil> ObtenerAsync(DateOnly desde, DateOnly hasta, IReadOnlyCollection<DayOfWeek> diasDeDescanso,
        DateTime? informacionHasta = null, CancellationToken ct = default)
    {
        if (hasta < desde) (desde, hasta) = (hasta, desde);
        var r = informacionHasta ?? DateTime.MaxValue;
        var festivos = await db.NationalValues.AsNoTracking()
            .Where(v => v.Code == CodigosDeValorNacional.Festivo && v.ValidFrom >= desde && v.ValidFrom <= hasta && !v.IsDeleted)
            .ToListAsync(ct);

        var noHabiles = new List<DateOnly>();
        foreach (var serie in festivos.GroupBy(f => f.Key))
        {
            if (!DateOnly.TryParseExact(serie.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)) continue;
            if (VigenciaBitemporal.Vigente(serie, fecha, r) is not null) noHabiles.Add(fecha);
        }
        return new CalendarioHabil(noHabiles, diasDeDescanso);
    }
}
