namespace IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

/// <summary>
/// Qué días de la semana son hábiles para contar vacaciones (política por empresa
/// <c>SemanaLaboral</c>, con vigencia; research R6). La ley no define el sábado: el CST
/// art. 172 sólo hace obligatorio el descanso dominical y los conceptos de Mintrabajo lo
/// dejan a la jornada pactada. Por eso es una decisión de la cooperativa, no un valor
/// legal, y el defecto («lunes a sábado») lo eligió el dueño.
/// </summary>
public enum SemanaLaboral
{
    LunesASabado = 0,
    LunesAViernes = 1,
}
