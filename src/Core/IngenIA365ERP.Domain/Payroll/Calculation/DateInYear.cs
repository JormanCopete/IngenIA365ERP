using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// Lectura de un parámetro legal de tipo <see cref="LegalParameterKind.DateInYear"/> (D-07):
/// <c>Value</c> guarda <c>MMDD</c> como número —630 es el 30 de junio, 1220 el 20 de
/// diciembre, 214 el 14 de febrero— y aquí se convierte en la fecha de un año concreto. Las
/// fechas límite de la prima, la consignación de cesantías y los intereses sólo sirven para el
/// aviso del calendario; ningún cálculo depende de ellas, y por eso no hay ningún día ni mes
/// escrito en este archivo: el valor viene siempre de <c>PAY_LegalParameters</c>.
/// </summary>
public static class DateInYear
{
    private const int MesesPorAnio = 12;
    private const int FactorMes = 100;

    /// <summary>
    /// Convierte el <c>MMDD</c> guardado en la fecha de <paramref name="anio"/>. Un valor que no
    /// sea una fecha posible (mes 13, día 32, 30 de febrero) es un dato mal digitado y se
    /// rechaza con el motivo, nunca se «corrige» en silencio.
    /// </summary>
    public static DateOnly Leer(decimal valor, int anio)
    {
        if (valor != decimal.Truncate(valor) || valor <= 0)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "Una fecha del año se guarda como MMDD entero (630 = 30 de junio).");

        var entero = (int)valor;
        var mes = entero / FactorMes;
        var dia = entero % FactorMes;

        if (mes < 1 || mes > MesesPorAnio)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, $"El mes {mes} no existe: el valor MMDD debe estar entre 101 y 1231.");
        if (dia < 1 || dia > DateTime.DaysInMonth(anio, mes))
            throw new ArgumentOutOfRangeException(nameof(valor), valor, $"El día {dia} no existe en el mes {mes} de {anio}.");

        return new DateOnly(anio, mes, dia);
    }

    /// <summary>Lo inverso: el <c>MMDD</c> que se guarda para una fecha (el año se descarta).</summary>
    public static int Escribir(DateOnly fecha) => fecha.Month * FactorMes + fecha.Day;

    /// <summary>Versión sin excepción para las pantallas: <c>false</c> si el valor no es una fecha.</summary>
    public static bool TryLeer(decimal? valor, int anio, out DateOnly fecha)
    {
        fecha = default;
        if (valor is null) return false;
        try
        {
            fecha = Leer(valor.Value, anio);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
