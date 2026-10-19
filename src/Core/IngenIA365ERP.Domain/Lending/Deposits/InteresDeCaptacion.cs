using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Un tramo de tasa por saldo de una versión de línea de captación (data-model §2.2): de <see cref="Desde"/> a <see cref="Hasta"/> (nulo = abierto),
/// a la EA <see cref="TasaEA"/> (fracción). Los tramos de una versión son contiguos: el siguiente empieza un centavo después de donde termina éste. (nuevo)
/// </summary>
public sealed record TramoDeInteres(decimal Desde, decimal? Hasta, decimal TasaEA);

/// <summary>
/// La tasa que rige un día de una cuenta (R05; FR-020): la EA de la versión (fija o variable ya resuelta con su índice, piso y techo), sus tramos con su
/// modo, el saldo mínimo para ganar interés, la convención de días y la equivalencia de tasas de la versión, y la periodicidad con que se liquida (la
/// equivalencia lineal la necesita para la nominal). (nuevo)
/// </summary>
public sealed record TasaDeCaptacionDelDia(
    decimal TasaEA,
    IReadOnlyList<TramoDeInteres> Tramos,
    RateTierMode ModoDeTramos,
    decimal MinimoParaInteres,
    DayCountConvention Convencion,
    RateEquivalence Equivalencia,
    RepaymentPeriodicity Periodicidad)
{
    /// <summary>Sin tramos, toda la tasa es la de la versión.</summary>
    public bool TieneTramos => Tramos.Count > 0;
}

/// <summary>
/// Un día de la ventana de causación: el saldo de capital al cierre del día con fecha valor (la serie de <see cref="ReconstruccionDeCuenta.SaldosDiarios"/>),
/// la tasa que rige ese día y si la causación está suspendida (FR-065: embargo total o titular fallecido según la línea). (nuevo)
/// </summary>
public sealed record DiaDeCaptacion(DateOnly Dia, decimal Saldo, TasaDeCaptacionDelDia Tasa, bool Suspendido = false);

/// <summary>
/// Lo que pide el interés de una ventana (R05; FR-020, FR-060): el método de saldo, el fin de la ventana anterior (excluido: la última liquidación o el
/// fin del período anterior) y los días de la ventana en orden, sin huecos. (nuevo)
/// </summary>
public sealed record EntradaDeInteres(BalanceMethod Metodo, DateOnly InicioExcluido, IReadOnlyList<DiaDeCaptacion> Dias);

/// <summary>
/// El interés de un día con su explicación (FR-011; api.md §1.4): el saldo del día, la base sobre la que causa (el saldo, el promedio o el mínimo de lo
/// corrido según el método), la EA aplicada (la del tramo, o la ponderada si es escalonado), la tasa diaria, los días de la convención, el acumulado
/// exacto de la ventana hasta ese día, lo causado ese día a seis decimales y por qué causó cero si lo hizo. (nuevo)
/// </summary>
public sealed record InteresDelDia(
    DateOnly Dia,
    decimal Saldo,
    decimal Base,
    decimal TasaEA,
    decimal TasaDiaria,
    int Dias,
    decimal Acumulado,
    decimal Causado,
    bool BajoElMinimo,
    bool Suspendido,
    string Tramo,
    string Explicacion);

/// <summary>El interés de una ventana: cada día y lo causado a seis decimales (la suma de lo causado de cada día). (nuevo)</summary>
public sealed record InteresDelPeriodo(BalanceMethod Metodo, IReadOnlyList<InteresDelDia> Dias)
{
    /// <summary>Lo causado en la ventana, a seis decimales: <c>Seis(A(fin))</c>, igual a la suma de lo causado día a día.</summary>
    public decimal Causado => Dias.Sum(d => d.Causado);

    /// <summary>Lo causado hasta <paramref name="dia"/> (incluido).</summary>
    public decimal CausadoHasta(DateOnly dia) => Dias.Where(d => d.Dia <= dia).Sum(d => d.Causado);
}

/// <summary>
/// El interés de una cuenta de captación (feature 014, US5, T127; research R05, D6–D9, D31; FR-020, FR-060, FR-061): puro, decimal y sin IO. Sobre los motores
/// de la 013 —<see cref="EquivalenciaDeTasas"/> (tasa diaria compuesta <c>(1 + EA)^(1/B) − 1</c> o lineal), <see cref="ContadorDeDias"/> (los días de cada
/// fecha en la convención) y la <b>diferencia de acumulados</b> de <c>Causacion</c> (lo causado el día d es <c>Seis(A(d)) − Seis(A(d − 1))</c>: el libro
/// nunca deriva)—:
/// <list type="bullet">
/// <item><b>Saldo diario</b> (defecto, D31): <c>A(d) = Σ interés(saldo_k, tasa_k, n_k)</c>, el interés de cada día sobre su saldo de cierre.</item>
/// <item><b>Saldo promedio</b>: <c>A(d) = Σ_k promedio_d · r_k(promedio_d) · n_k</c>, interés simple sobre el promedio de lo corrido; sin tramos ni cambio de
/// tasa da lo mismo que el diario (investigación §3.3: 6.884).</item>
/// <item><b>Saldo mínimo</b>: <c>A(d) = mínimo_d · (Π_k (1 + r_k(mínimo_d))^{n_k} − 1)</c>, la tasa del período sobre el mínimo de lo corrido (3.649 del
/// ejemplo). Con equivalencia lineal, la suma.</item>
/// <item><b>Tramos</b> (D8): <see cref="RateTierMode.WholeBalance"/> lleva todo el saldo a la tasa del tramo donde cae; <see cref="RateTierMode.Stepped"/>
/// cada porción a la de su tramo (<c>r_k(B)</c> es la tasa diaria efectiva de la base B ese día).</item>
/// <item>Un día con la base bajo el mínimo para interés, o con saldo cero o negativo, causa cero; un día suspendido no causa ni entra al promedio ni al
/// mínimo (FR-065).</item>
/// <item>La tasa puede cambiar cualquier día (versión nueva o reajuste de la variable): cada día lleva la suya.</item>
/// </list>
/// La causación guarda seis decimales (<c>MontoCausado</c>); la liquidación redondea al peso (o a la unidad de la versión) y la diferencia la absorbe la
/// misma operación (D9, <see cref="Liquidable"/>). Sin valores legales: las tasas llegan de la versión. (nuevo)
/// </summary>
public static class InteresDeCaptacion
{
    /// <summary>Decimales de la causación (alias <c>MontoCausado</c> 18,6, data-model §0.3).</summary>
    public const int DecimalesDeCausacion = 6;

    /// <summary>Un valor a los seis decimales de la causación, al par (como la hoja <c>calcular.py</c>).</summary>
    public static decimal Seis(decimal valor) => decimal.Round(valor, DecimalesDeCausacion, MidpointRounding.ToEven);

    /// <summary>La tasa diaria de una EA en la convención y la equivalencia de la versión.</summary>
    public static decimal TasaDiaria(decimal efectivaAnual, TasaDeCaptacionDelDia tasa)
    {
        ArgumentNullException.ThrowIfNull(tasa);
        var periodicidad = tasa.Periodicidad == RepaymentPeriodicity.SinglePayment ? RepaymentPeriodicity.Monthly : tasa.Periodicidad;
        return EquivalenciaDeTasas.TasaDiaria(efectivaAnual, tasa.Convencion, tasa.Equivalencia, periodicidad);
    }

    /// <summary>
    /// El interés de una base durante <paramref name="dias"/> días con la tasa del día (sin el mínimo para interés): cada porción a la tasa diaria de su
    /// tramo, compuesta <c>(1 + r)^n − 1</c> o lineal <c>r · n</c>. Devuelve el interés, la EA aplicada (la ponderada si es escalonado) y el rótulo del tramo.
    /// </summary>
    public static (decimal Interes, decimal TasaEA, decimal TasaDiaria, string Tramo) InteresDeLaBase(decimal @base, TasaDeCaptacionDelDia tasa, int dias)
    {
        ArgumentNullException.ThrowIfNull(tasa);
        if (@base <= 0m || dias <= 0) return (0m, TasaDelaBase(@base, tasa), 0m, RotuloDelTramo(@base, tasa));
        if (!tasa.TieneTramos)
        {
            var r = TasaDiaria(tasa.TasaEA, tasa);
            return (@base * EquivalenciaDeTasas.TasaDelPeriodo(r, dias, tasa.Equivalencia), tasa.TasaEA, r, "Sin tramos");
        }

        var tramos = tasa.Tramos.OrderBy(t => t.Desde).ToList();
        if (tasa.ModoDeTramos == RateTierMode.WholeBalance)
        {
            var tramo = TramoDe(@base, tramos);
            if (tramo is null) return (0m, 0m, 0m, "Fuera de los tramos");
            var r = TasaDiaria(tramo.TasaEA, tasa);
            return (@base * EquivalenciaDeTasas.TasaDelPeriodo(r, dias, tasa.Equivalencia), tramo.TasaEA, r, RotuloDelTramo(@base, tasa));
        }

        // Escalonado: cada porción entre el fin del tramo anterior y el fin del suyo, a su tasa.
        var interes = 0m;
        var abajo = 0m;
        var partes = new List<string>();
        for (var i = 0; i < tramos.Count; i++)
        {
            var t = tramos[i];
            var desde = i == 0 ? t.Desde : abajo;
            if (@base <= desde) break;
            var hasta = t.Hasta is { } h && h < @base ? h : @base;
            var porcion = hasta - desde;
            if (porcion > 0m)
            {
                var r = TasaDiaria(t.TasaEA, tasa);
                interes += porcion * EquivalenciaDeTasas.TasaDelPeriodo(r, dias, tasa.Equivalencia);
                partes.Add(string.Create(CultureInfo.InvariantCulture, $"{porcion:0.##} a {t.TasaEA:0.######}"));
            }
            if (t.Hasta is not { } fin) break;
            abajo = fin;
        }
        var diaria = interes == 0m ? 0m : interes / @base / dias;
        return (interes, TasaDelaBase(@base, tasa), diaria, "Escalonado: " + string.Join("; ", partes));
    }

    /// <summary>
    /// El interés de la ventana día por día (ver la clase). La entrada trae los días en orden desde el siguiente a <see cref="EntradaDeInteres.InicioExcluido"/>.
    /// </summary>
    public static InteresDelPeriodo Calcular(EntradaDeInteres entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var resultado = new List<InteresDelDia>(entrada.Dias.Count);
        var anterior = entrada.InicioExcluido;
        var acumuladoAnterior = 0m;
        var exacto = 0m;
        // Lo corrido para el promedio y el mínimo: los días no suspendidos con su peso en días de la convención.
        var sumaPonderada = 0m;
        var diasDelPromedio = 0;
        decimal? minimo = null;
        var corridos = new List<(TasaDeCaptacionDelDia Tasa, int Dias)>();

        foreach (var d in entrada.Dias)
        {
            if (d.Dia <= anterior)
                throw new ArgumentException($"Los días de la ventana van en orden y después de {entrada.InicioExcluido:yyyy-MM-dd}.", nameof(entrada));
            var n = ContadorDeDias.Dias(d.Tasa.Convencion, anterior, d.Dia);
            anterior = d.Dia;

            decimal @base;
            bool bajo;
            string tramo;
            decimal ea, diaria;
            if (d.Suspendido)
            {
                @base = 0m;
                bajo = false;
                tramo = "Suspendida";
                ea = 0m;
                diaria = 0m;
            }
            else
            {
                corridos.Add((d.Tasa, n));
                if (n > 0)
                {
                    sumaPonderada += d.Saldo * n;
                    diasDelPromedio += n;
                }
                minimo = minimo is { } m ? Math.Min(m, d.Saldo) : d.Saldo;
                @base = entrada.Metodo switch
                {
                    BalanceMethod.AverageBalance => diasDelPromedio == 0 ? d.Saldo : sumaPonderada / diasDelPromedio,
                    BalanceMethod.MinimumBalance => minimo ?? 0m,
                    _ => d.Saldo,
                };
                bajo = @base <= 0m || @base < d.Tasa.MinimoParaInteres;
                var deLaBase = InteresDeLaBase(@base, d.Tasa, Math.Max(n, 1));
                (ea, diaria, tramo) = (deLaBase.TasaEA, deLaBase.TasaDiaria, deLaBase.Tramo);

                exacto = entrada.Metodo switch
                {
                    BalanceMethod.AverageBalance => bajo ? 0m : Promedio(@base, corridos),
                    BalanceMethod.MinimumBalance => bajo ? 0m : Minimo(@base, corridos),
                    _ => exacto + (bajo ? 0m : InteresDeLaBase(@base, d.Tasa, n).Interes),
                };
            }

            var acumulado = Seis(exacto);
            var causado = acumulado - acumuladoAnterior;
            acumuladoAnterior = acumulado;
            resultado.Add(new InteresDelDia(d.Dia, d.Saldo, @base, ea, diaria, n, acumulado, causado, bajo, d.Suspendido, tramo,
                Explicar(entrada.Metodo, d, @base, ea, diaria, n, causado, bajo)));
        }
        return new InteresDelPeriodo(entrada.Metodo, resultado);
    }

    /// <summary>Lo que se liquida de lo causado: a la unidad y con el modo de redondeo de la versión (D9).</summary>
    public static decimal Liquidable(decimal causado, RedondeoDeCartera redondeo)
    {
        ArgumentNullException.ThrowIfNull(redondeo);
        return causado <= 0m ? 0m : redondeo.Redondear(causado);
    }

    /// <summary>
    /// El período de liquidación que contiene <paramref name="fecha"/> (FR-061): con <paramref name="diaDeLiquidacion"/> y una periodicidad de meses, los
    /// períodos terminan ese día (o el último del mes si es más corto) en los meses del ciclo, alineados con enero; sin él, el período calendario de la
    /// periodicidad (el de los cargos, <see cref="CargosDeCuenta.PeriodoDe"/>). Nulo con pago único (la cuenta se liquida al cancelarse).
    /// </summary>
    public static (DateOnly Desde, DateOnly Hasta)? PeriodoDeLiquidacion(RepaymentPeriodicity periodicidad, byte? diaDeLiquidacion, DateOnly fecha)
    {
        if (periodicidad == RepaymentPeriodicity.SinglePayment) return null;
        var mensual = (int)RepaymentPeriodicity.Monthly;
        if (diaDeLiquidacion is not { } dia || (int)periodicidad < mensual) return CargosDeCuenta.PeriodoDe(periodicidad, fecha);

        var meses = (int)periodicidad / mensual;
        DateOnly FinEn(int anio, int mes) => new(anio, mes, Math.Min(dia, DateTime.DaysInMonth(anio, mes)));
        // El fin del ciclo que contiene el mes de la fecha; si la fecha ya lo pasó, el del ciclo siguiente; si no llega al del ciclo anterior, ése.
        var mesDelCiclo = ((fecha.Month - 1) / meses + 1) * meses;
        var primeroDelCiclo = new DateOnly(fecha.Year, mesDelCiclo, 1);
        var fin = FinEn(fecha.Year, mesDelCiclo);
        if (fecha > fin)
        {
            var siguiente = primeroDelCiclo.AddMonths(meses);
            fin = FinEn(siguiente.Year, siguiente.Month);
        }
        else
        {
            var previo = primeroDelCiclo.AddMonths(-meses);
            var finDelPrevio = FinEn(previo.Year, previo.Month);
            if (fecha <= finDelPrevio) fin = finDelPrevio;
        }
        var antes = new DateOnly(fin.Year, fin.Month, 1).AddMonths(-meses);
        var finPrevio = FinEn(antes.Year, antes.Month);
        return (finPrevio.AddDays(1), fin);
    }

    // ----------------------------------------------------------------------------------------------------- piezas --

    /// <summary>Promedio: interés simple sobre la base con la tasa diaria efectiva de cada día corrido.</summary>
    private static decimal Promedio(decimal @base, IReadOnlyList<(TasaDeCaptacionDelDia Tasa, int Dias)> corridos)
    {
        var total = 0m;
        foreach (var (tasa, dias) in corridos)
            if (dias > 0) total += TasaEfectivaDiaria(@base, tasa) * dias;
        return @base * total;
    }

    /// <summary>Mínimo: la tasa del período sobre la base, compuesta día por día (o la suma si la equivalencia es lineal).</summary>
    private static decimal Minimo(decimal @base, IReadOnlyList<(TasaDeCaptacionDelDia Tasa, int Dias)> corridos)
    {
        var factor = 1m;
        var suma = 0m;
        var lineal = corridos.Count > 0 && corridos[^1].Tasa.Equivalencia == RateEquivalence.Linear;
        foreach (var (tasa, dias) in corridos)
        {
            if (dias <= 0) continue;
            var r = TasaEfectivaDiaria(@base, tasa);
            if (lineal) suma += r * dias;
            else factor *= AritmeticaDecimal.Potencia(1m + r, dias);
        }
        return @base * (lineal ? suma : factor - 1m);
    }

    /// <summary>La tasa diaria efectiva de una base un día (la del tramo, o la ponderada de sus porciones si es escalonado).</summary>
    private static decimal TasaEfectivaDiaria(decimal @base, TasaDeCaptacionDelDia tasa) =>
        @base <= 0m ? 0m : InteresDeLaBase(@base, tasa, 1).Interes / @base;

    private static TramoDeInteres? TramoDe(decimal @base, IReadOnlyList<TramoDeInteres> tramos) =>
        tramos.LastOrDefault(t => @base >= t.Desde && (t.Hasta is not { } h || @base <= h))
        ?? tramos.LastOrDefault(t => @base >= t.Desde);

    private static decimal TasaDelaBase(decimal @base, TasaDeCaptacionDelDia tasa) =>
        !tasa.TieneTramos ? tasa.TasaEA : TramoDe(@base, tasa.Tramos.OrderBy(t => t.Desde).ToList())?.TasaEA ?? 0m;

    private static string RotuloDelTramo(decimal @base, TasaDeCaptacionDelDia tasa)
    {
        if (!tasa.TieneTramos) return "Sin tramos";
        var ordenados = tasa.Tramos.OrderBy(t => t.Desde).ToList();
        var tramo = TramoDe(@base, ordenados);
        return tramo is null
            ? "Fuera de los tramos"
            : string.Create(CultureInfo.InvariantCulture, $"Tramo {ordenados.IndexOf(tramo) + 1} ({tramo.Desde:0.##} a {(tramo.Hasta is { } h ? h.ToString("0.##", CultureInfo.InvariantCulture) : "sin tope")})");
    }

    private static string Explicar(BalanceMethod metodo, DiaDeCaptacion d, decimal @base, decimal ea, decimal diaria, int n, decimal causado, bool bajo)
    {
        if (d.Suspendido) return "Causación suspendida (FR-065): el día no causa.";
        if (n == 0) return "El día no causa en la convención de 30/360.";
        if (bajo)
            return string.Create(CultureInfo.InvariantCulture,
                $"Base {@base:0.##} bajo el mínimo para interés ({d.Tasa.MinimoParaInteres:0.##}) o sin saldo: el día causa cero.");
        var deBase = metodo switch
        {
            BalanceMethod.AverageBalance => "saldo promedio de lo corrido",
            BalanceMethod.MinimumBalance => "saldo mínimo de lo corrido",
            _ => "saldo del día",
        };
        return string.Create(CultureInfo.InvariantCulture,
            $"Base {@base:0.##} ({deBase}), EA {ea:0.######}, tasa diaria {diaria:0.##################} por {n} día(s): causa {causado:0.######}.");
    }
}
