using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Common.NationalValues;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Calendar;

/// <summary>
/// El calendario de días hábiles de la cooperativa (feature 013, T40, R35): arma el <see cref="CalendarioHabil"/> puro con los
/// <c>FESTIVO</c> nacionales <b>replicados</b> (<c>COR_NationalValues</c>) —nunca con <c>PAY_Holidays</c>, que tiene los
/// festivos «Manual» de Nómina (pregunta E5)— y los días de descanso que diga quien lo pide. (nuevo)
/// </summary>
public interface ICalendarioHabil
{
    /// <summary>El calendario que cubre de <paramref name="desde"/> a <paramref name="hasta"/> (el rango de los festivos que carga).</summary>
    Task<CalendarioHabil> ObtenerAsync(DateOnly desde, DateOnly hasta, IReadOnlyCollection<DayOfWeek> diasDeDescanso,
        DateTime? informacionHasta = null, CancellationToken ct = default);
}

/// <summary>Los días de descanso habituales. (nuevo)</summary>
public static class DiasDeDescanso
{
    /// <summary>Para la fecha exigible del art. 829 del Código de Comercio sólo el domingo es de descanso (pregunta D2).</summary>
    public static IReadOnlyCollection<DayOfWeek> SoloDomingo { get; } = [DayOfWeek.Sunday];

    /// <summary>Para los plazos del titular (Leyes 1581 y 1266) el sábado tampoco es hábil.</summary>
    public static IReadOnlyCollection<DayOfWeek> SabadoYDomingo { get; } = [DayOfWeek.Saturday, DayOfWeek.Sunday];
}
