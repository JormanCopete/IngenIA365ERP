namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// El retiro esperado del ahorro a la vista medido con la historia (feature 014, respuesta 100 B del dueño, E250; Circular Básica Contable y Financiera,
/// Título IV, Capítulo III, Anexo 1): el máximo retiro probable del día a un nivel de confianza (por defecto 95 %, el percentil por el método del rango más
/// cercano de las salidas netas diarias) escalado por la raíz del horizonte (30 días). Motor puro: sin base, sin reloj. (nuevo)
/// </summary>
public static class RetiroEsperadoDeLaVista
{
    /// <summary>
    /// El retiro esperado en el horizonte: el percentil <paramref name="confianza"/> de <paramref name="salidasNetasDiarias"/> (cada día, lo retirado menos lo
    /// consignado, nunca negativo) por la raíz de <paramref name="horizonteDias"/>, al centavo. Sin días, cero.
    /// </summary>
    public static decimal Calcular(IReadOnlyList<decimal> salidasNetasDiarias, decimal confianza, int horizonteDias)
    {
        ArgumentNullException.ThrowIfNull(salidasNetasDiarias);
        if (confianza is <= 0m or >= 1m) throw new ArgumentOutOfRangeException(nameof(confianza), "La confianza va entre 0 y 1, sin incluirlos.");
        if (horizonteDias < 1) throw new ArgumentOutOfRangeException(nameof(horizonteDias), "El horizonte es de al menos un día.");
        if (salidasNetasDiarias.Count == 0) return 0m;
        var ordenadas = salidasNetasDiarias.Select(s => Math.Max(0m, s)).Order().ToList();
        var rango = (int)Math.Ceiling(confianza * ordenadas.Count);
        var percentil = ordenadas[Math.Clamp(rango, 1, ordenadas.Count) - 1];
        return decimal.Round(percentil * RaizCuadrada(horizonteDias), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>La raíz cuadrada en decimal (Newton), sin pasar por coma flotante: el motor de Cartera es decimal de punta a punta.</summary>
    public static decimal RaizCuadrada(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (n == 0) return 0m;
        decimal x = n;
        for (var i = 0; i < 60; i++)
        {
            var siguiente = (x + (n / x)) / 2m;
            if (siguiente == x) break;
            x = siguiente;
        }
        return x;
    }

    /// <summary>La fracción del saldo a la vista que representa el retiro esperado (nunca más de 1; sin saldo, cero).</summary>
    public static decimal Fraccion(decimal retiroEsperado, decimal saldoALaVista) =>
        saldoALaVista <= 0m ? 0m : Math.Min(1m, decimal.Round(retiroEsperado / saldoALaVista, 6, MidpointRounding.AwayFromZero));
}
