namespace IngenIA365ERP.Domain.Transport;

/// <summary>
/// El período de facturación de servicios: el mes calendario (feature 020, FR-034), como texto <c>aaaamm</c>.
/// </summary>
public readonly record struct PeriodoDeServicio(int Anio, int Mes)
{
    public DateOnly Inicio => new(Anio, Mes, 1);

    public DateOnly Fin => Inicio.AddMonths(1).AddDays(-1);

    public int Dias => DateTime.DaysInMonth(Anio, Mes);

    public string Codigo => $"{Anio:0000}{Mes:00}";

    public static bool TryParse(string? texto, out PeriodoDeServicio periodo)
    {
        periodo = default;
        if (texto is not { Length: 6 } || !int.TryParse(texto[..4], out var a) || !int.TryParse(texto[4..], out var m) || m is < 1 or > 12 || a < 2000) return false;
        periodo = new PeriodoDeServicio(a, m);
        return true;
    }

    public static PeriodoDeServicio De(DateOnly fecha) => new(fecha.Year, fecha.Month);

    public string Texto()
    {
        string[] meses = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];
        return $"{meses[Mes - 1]} {Anio}";
    }
}

/// <summary>
/// Cuánto del mes se cobra a una suscripción que empieza o termina dentro de él (feature 020, R9, T101; FR-034): el mes completo (la
/// política por defecto) o la fracción de días cubiertos, a cuatro decimales, que va como cantidad de la línea.
/// </summary>
public static class Prorrateo
{
    /// <summary>La cantidad a facturar: 1 a mes completo; si se prorratea, días cubiertos ÷ días del mes. Cero si no se cruza con el mes.</summary>
    public static decimal Cantidad(PeriodoDeServicio periodo, DateOnly desde, DateOnly? hasta, bool prorratear)
    {
        var inicio = desde > periodo.Inicio ? desde : periodo.Inicio;
        var fin = hasta is { } h && h < periodo.Fin ? h : periodo.Fin;
        if (fin < inicio) return 0m;
        if (!prorratear) return 1m;
        var dias = fin.DayNumber - inicio.DayNumber + 1;
        return Math.Round((decimal)dias / periodo.Dias, 4, MidpointRounding.AwayFromZero);
    }
}

/// <summary>
/// Reparte el valor a pagar de una factura entre sus líneas para el pago de cada servicio (feature 020, R10, T102): proporcional al peso de
/// cada línea (su neto más sus impuestos), redondeado a dos decimales, con el residuo en la última línea para que la suma sea exacta. Sin
/// retenciones el reparto devuelve los mismos pesos.
/// </summary>
public static class RepartoDePagosPorLinea
{
    public static IReadOnlyList<decimal> Repartir(decimal total, IReadOnlyList<decimal> pesos)
    {
        ArgumentNullException.ThrowIfNull(pesos);
        if (pesos.Count == 0) return [];
        var suma = pesos.Sum();
        var partes = new decimal[pesos.Count];
        var asignado = 0m;
        for (var i = 0; i < pesos.Count - 1; i++)
        {
            partes[i] = suma == 0m ? 0m : Math.Round(total * pesos[i] / suma, 2, MidpointRounding.AwayFromZero);
            asignado += partes[i];
        }
        partes[^1] = total - asignado;
        return partes;
    }
}
