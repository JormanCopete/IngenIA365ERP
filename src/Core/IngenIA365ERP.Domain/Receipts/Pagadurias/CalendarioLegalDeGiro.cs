using IngenIA365ERP.Domain.Common.Calendar;

namespace IngenIA365ERP.Domain.Receipts.Pagadurias;

/// <summary>Lo de una pagaduría en un período: lo enviado, lo respondido (descontado), lo girado, lo pendiente y si va tarde. (nuevo)</summary>
public sealed record EstadoDelPeriodoDePagaduria(decimal Enviado, decimal Descontado, decimal Girado, decimal Pendiente, DateOnly? FechaLegal, bool Tardio);

/// <summary>
/// El calendario legal de giro de las pagadurías (feature 015, R1, T0383; FR-082; Ley 1527 art. 6): la fecha legal es la fecha de pago de la nómina más
/// <c>Libranza.DiasHabilesGiro</c> días hábiles (el parámetro, nunca un literal) contados desde el día siguiente. Va tarde lo que sigue pendiente pasada esa fecha. Puro e
/// informativo: nada se bloquea. (nuevo)
/// </summary>
public static class CalendarioLegalDeGiro
{
    public static DateOnly FechaLegal(CalendarioHabil calendario, DateOnly pagoDeLaNomina, int diasHabiles)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        return calendario.SumarHabiles(pagoDeLaNomina, Math.Max(0, diasHabiles));
    }

    public static EstadoDelPeriodoDePagaduria Estado(decimal enviado, decimal descontado, decimal girado, DateOnly? fechaLegal, DateOnly hoy)
    {
        var pendiente = Math.Max(0m, descontado - girado);
        return new EstadoDelPeriodoDePagaduria(enviado, descontado, girado, pendiente, fechaLegal, fechaLegal is { } f && hoy > f && pendiente > 0m);
    }
}
