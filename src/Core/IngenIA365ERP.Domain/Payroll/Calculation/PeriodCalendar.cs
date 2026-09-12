using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// Calendario de períodos por periodicidad: qué número le toca a un período dentro de su
/// mes, cuál es el último del mes, y si su duración es coherente con el plan. Es lo que
/// necesitan las recurrentes «primero/último del mes» y la validación al crear períodos.
/// No calcula nada de dinero.
/// </summary>
public static class PeriodCalendar
{
    public sealed record Propuesta(byte SubPeriodNumber, short Year, byte Month);

    /// <summary>Cuántos períodos tiene un mes: 1, 2, 3; semanal hasta 5.</summary>
    public static byte PeriodosPorMes(PayrollPeriodicity periodicidad) => periodicidad switch
    {
        PayrollPeriodicity.Monthly => 1,
        PayrollPeriodicity.Biweekly => 2,
        PayrollPeriodicity.TenDay => 3,
        PayrollPeriodicity.Weekly => 5,
        _ => 1,
    };

    /// <summary>
    /// Propone sub-período y mes de imputación desde la fecha de inicio: quincena 1 si
    /// empieza el 1–15; década por tramos 1–10 / 11–20 / 21+; semana = ceil(día/7) acotada
    /// a 5. El mes es el del inicio; la persona lo puede cambiar.
    /// </summary>
    public static Propuesta Proponer(PayrollPeriodicity periodicidad, DateTime inicio)
    {
        var dia = inicio.Day;
        var dias = (int)periodicidad;
        byte sub = periodicidad switch
        {
            PayrollPeriodicity.Monthly => 1,
            PayrollPeriodicity.Weekly => (byte)Math.Min(5, (dia + dias - 1) / dias),
            _ => (byte)Math.Min(PeriodosPorMes(periodicidad), (dia + dias - 1) / dias),
        };
        return new Propuesta(sub, (short)inicio.Year, (byte)inicio.Month);
    }

    /// <summary>
    /// ¿Es el último período del mes? Mensual, quincenal y decadal lo saben por su número;
    /// en semanal depende de cuántas semanas tenga ese mes en el plan: se pasa el mayor
    /// número de semana ya creado para ese mes (o ninguno, y entonces la quinta).
    /// </summary>
    public static bool EsUltimoDelMes(PayrollPeriodicity periodicidad, byte subPeriodo, byte? mayorSemanaDelMes = null) =>
        periodicidad == PayrollPeriodicity.Weekly
            ? subPeriodo == (mayorSemanaDelMes ?? PeriodosPorMes(periodicidad))
            : subPeriodo == PeriodosPorMes(periodicidad);

    /// <summary>
    /// La duración tiene que ser la del plan; el último período del mes admite lo que el
    /// calendario le quite o le sume (un mes de 28 o 31, una tercera década de 8 u 11 días).
    /// Semanal es exacto: siete días, siempre.
    /// </summary>
    public static bool DuracionValida(PayrollPeriodicity periodicidad, DateTime inicio, DateTime fin)
    {
        var dias = (fin.Date - inicio.Date).Days + 1;
        var esperado = (int)periodicidad;
        if (dias == esperado) return true;
        if (periodicidad == PayrollPeriodicity.Weekly) return false;
        var terminaElMes = fin.Day == DateTime.DaysInMonth(fin.Year, fin.Month);
        return terminaElMes && dias >= esperado - 2 && dias <= esperado + 1;
    }

    public static string Etiqueta(PayrollPeriodicity periodicidad, byte subPeriodo) => periodicidad switch
    {
        PayrollPeriodicity.Monthly => "Mes",
        PayrollPeriodicity.Biweekly => $"Quincena {subPeriodo}",
        PayrollPeriodicity.TenDay => $"Década {subPeriodo}",
        PayrollPeriodicity.Weekly => $"Semana {subPeriodo}",
        _ => subPeriodo.ToString(),
    };

    public static string Nombre(PayrollPeriodicity periodicidad) => periodicidad switch
    {
        PayrollPeriodicity.Monthly => "Mensual",
        PayrollPeriodicity.Biweekly => "Quincenal",
        PayrollPeriodicity.TenDay => "Decadal",
        PayrollPeriodicity.Weekly => "Semanal",
        _ => periodicidad.ToString(),
    };
}
