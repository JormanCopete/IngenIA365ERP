using System.Globalization;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>De qué lado está un flujo de la medición de liquidez (Circular Básica Contable y Financiera, Título IV, Capítulo III, Anexos 1 y 3). (nuevo)</summary>
public enum TipoDeFlujoDeLiquidez
{
    /// <summary>Entradas contractuales (la cartera que vence, ajustada por la mora).</summary>
    Entrada = 1,

    /// <summary>Salidas contractuales (CDAT y ahorro contractual con capital e intereses hasta el vencimiento).</summary>
    SalidaContractual = 2,

    /// <summary>Salidas no contractuales (el retiro esperado del ahorro a la vista, del permanente y de los aportes).</summary>
    SalidaNoContractual = 3,
}

/// <summary>Un flujo: su lado, su concepto, los días desde la fecha de medición hasta que ocurre (cero o menos: la primera banda) y el monto. (nuevo)</summary>
public sealed record FlujoDeLiquidez(TipoDeFlujoDeLiquidez Tipo, string Concepto, int Dias, decimal Monto);

/// <summary>Un activo líquido con su descuento (fracción): el neto es el valor por (1 − descuento). (nuevo)</summary>
public sealed record ActivoLiquido(string Concepto, decimal Valor, decimal Descuento)
{
    public decimal Neto => Math.Round(Valor * (1m - Descuento), 2, MidpointRounding.AwayFromZero);
}

/// <summary>Lo de una banda: entradas, salidas, la brecha de la banda, la acumulada y el IRL acumulado hasta ella (nulo sin salidas acumuladas). (nuevo)</summary>
public sealed record BandaDeRiesgoDeLiquidez(
    string Banda,
    int? HastaDias,
    decimal Entradas,
    decimal SalidasContractuales,
    decimal SalidasNoContractuales,
    decimal Brecha,
    decimal BrechaAcumulada,
    decimal? IrlAcumulado);

/// <summary>La medición: los activos líquidos netos, las bandas, el IRL más bajo de las bandas y los flujos que quedaron fuera del horizonte. (nuevo)</summary>
public sealed record MedicionDeRiesgoDeLiquidez(
    decimal ActivosLiquidosNetos,
    IReadOnlyList<BandaDeRiesgoDeLiquidez> Bandas,
    decimal? IrlMinimo,
    string? BandaDelIrlMinimo,
    decimal FueraDelHorizonte);

/// <summary>
/// Motor puro de la medición del riesgo de liquidez (feature 014, US15, T214; research §24): el indicador de riesgo de liquidez (IRL) y la brecha de liquidez
/// por bandas de tiempo. Sin reloj ni base. Las bandas, los descuentos y los flujos los arma Application con los parámetros de la cooperativa:
/// <list type="bullet">
/// <item><b>Brecha</b> de cada banda = entradas − salidas contractuales − salidas no contractuales; la acumulada suma las anteriores (Anexo 3: brecha de
/// liquidez).</item>
/// <item><b>IRL</b> acumulado hasta una banda = (activos líquidos netos + entradas acumuladas) ÷ (salidas acumuladas) (Anexo 1: IRLr). El límite lo pone quien
/// llama (el parámetro <c>RiesgoDeLiquidez.UmbralIrl</c>, con el piso legal).</item>
/// <item>Un flujo después de la última banda con tope queda fuera del horizonte si no hay banda abierta (se informa el total).</item>
/// </list>
/// El IRL se redondea a cuatro decimales y los montos al centavo. (nuevo)
/// </summary>
public static class RiesgoDeLiquidez
{
    private const int DecimalesDelIrl = 4;
    private const int Centavos = 2;

    /// <param name="bandas">Los topes en días de cada banda, ascendentes; un nulo al final es la banda abierta.</param>
    public static MedicionDeRiesgoDeLiquidez Medir(IReadOnlyList<int?> bandas, IReadOnlyList<ActivoLiquido> activos, IReadOnlyList<FlujoDeLiquidez> flujos)
    {
        ArgumentNullException.ThrowIfNull(bandas);
        ArgumentNullException.ThrowIfNull(activos);
        ArgumentNullException.ThrowIfNull(flujos);
        if (bandas.Count == 0) throw new ArgumentException("La medición necesita al menos una banda.", nameof(bandas));
        var topes = bandas.Where(b => b is not null).Select(b => b!.Value).Distinct().Order().Select(b => (int?)b).ToList();
        if (bandas.Any(b => b is null)) topes.Add(null);

        var aln = activos.Sum(a => a.Neto);
        var porBanda = topes.Select(_ => (Entradas: 0m, Contractuales: 0m, NoContractuales: 0m)).ToArray();
        var fuera = 0m;
        foreach (var f in flujos.Where(f => f.Monto != 0m))
        {
            var i = Indice(topes, f.Dias);
            if (i < 0)
            {
                fuera += f.Monto;
                continue;
            }
            porBanda[i] = f.Tipo switch
            {
                TipoDeFlujoDeLiquidez.Entrada => porBanda[i] with { Entradas = porBanda[i].Entradas + f.Monto },
                TipoDeFlujoDeLiquidez.SalidaContractual => porBanda[i] with { Contractuales = porBanda[i].Contractuales + f.Monto },
                _ => porBanda[i] with { NoContractuales = porBanda[i].NoContractuales + f.Monto },
            };
        }

        var resultado = new List<BandaDeRiesgoDeLiquidez>(topes.Count);
        decimal acumulada = 0m, entradasAcumuladas = 0m, salidasAcumuladas = 0m;
        for (var i = 0; i < topes.Count; i++)
        {
            var (e, c, n) = porBanda[i];
            e = Math.Round(e, Centavos, MidpointRounding.AwayFromZero);
            c = Math.Round(c, Centavos, MidpointRounding.AwayFromZero);
            n = Math.Round(n, Centavos, MidpointRounding.AwayFromZero);
            var brecha = e - c - n;
            acumulada += brecha;
            entradasAcumuladas += e;
            salidasAcumuladas += c + n;
            decimal? irl = salidasAcumuladas > 0m
                ? Math.Round((aln + entradasAcumuladas) / salidasAcumuladas, DecimalesDelIrl, MidpointRounding.AwayFromZero)
                : null;
            resultado.Add(new BandaDeRiesgoDeLiquidez(Nombre(topes, i), topes[i], e, c, n, brecha, acumulada, irl));
        }
        var minimo = resultado.Where(b => b.IrlAcumulado is not null).OrderBy(b => b.IrlAcumulado).FirstOrDefault();
        return new MedicionDeRiesgoDeLiquidez(aln, resultado, minimo?.IrlAcumulado, minimo?.Banda, Math.Round(fuera, Centavos, MidpointRounding.AwayFromZero));
    }

    private static int Indice(IReadOnlyList<int?> topes, int dias)
    {
        for (var i = 0; i < topes.Count; i++)
            if (topes[i] is not { } hasta || dias <= hasta) return i;
        return -1;
    }

    /// <summary>El nombre de la banda («1 a 15 días», «16 a 30 días», «más de 90 días»).</summary>
    public static string Nombre(IReadOnlyList<int?> topes, int indice)
    {
        ArgumentNullException.ThrowIfNull(topes);
        var desde = indice == 0 ? 1 : (topes[indice - 1] ?? 0) + 1;
        return topes[indice] is { } hasta
            ? string.Create(CultureInfo.InvariantCulture, $"{desde} a {hasta} días")
            : string.Create(CultureInfo.InvariantCulture, $"más de {desde - 1} días");
    }
}
