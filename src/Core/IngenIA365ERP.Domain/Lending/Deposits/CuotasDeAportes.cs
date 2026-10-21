using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>Un período de cuota: año, mes y subperíodo, sus días y el vencimiento (feature 014, US3, T082). (nuevo)</summary>
public sealed record PeriodoDeCuota(int Anio, int Mes, int Subperiodo, DateOnly Desde, DateOnly Hasta, DateOnly Vencimiento)
{
    /// <summary>La clave ordenable del período (<c>AAAA-MM-s</c>), la que guarda la marca de la última cuota generada.</summary>
    public string Clave => Marca(Anio, Mes, Subperiodo);

    /// <summary>El período como lo leen el contrato y las pantallas: <c>AAAA-MM</c>, o <c>AAAA-MM-s</c> desde el segundo subperíodo.</summary>
    public string Texto => Subperiodo > 1
        ? string.Create(CultureInfo.InvariantCulture, $"{Anio:D4}-{Mes:D2}-{Subperiodo}")
        : string.Create(CultureInfo.InvariantCulture, $"{Anio:D4}-{Mes:D2}");

    /// <summary>La marca de agua del control de la cuenta (<c>LastInstallmentPeriod</c>, nvarchar(10)).</summary>
    public static string Marca(int anio, int mes, int subperiodo) => string.Create(CultureInfo.InvariantCulture, $"{anio:D4}-{mes:D2}-{subperiodo}");
}

/// <summary>
/// Lo que entra al cálculo de la cuota de aportes de una cuenta (feature 014, US3, T082; data-model §2.5): el modo y su valor o su fracción, la base que
/// el modo pide (el ingreso registrado de la persona o el salario mínimo vigente), el redondeo y la fracción del ahorro permanente. (nuevo)
/// </summary>
public sealed record EntradaDeCuotaDeAportes(
    ContributionAmountMode Modo,
    decimal? Valor,
    decimal? Fraccion,
    decimal? Ingreso,
    decimal? SalarioMinimo,
    decimal UnidadDeRedondeo,
    RoundingMode Redondeo,
    decimal? FraccionPermanente);

/// <summary>
/// La cuota calculada: el total del período y su reparto entre aportes y ahorro permanente (FR-049), con la explicación y la base usada. (nuevo)
/// </summary>
public sealed record CuotaDeAportesCalculada(decimal Total, decimal Aportes, decimal Permanente, decimal? Base, string Explicacion);

/// <summary>
/// El motor puro de las cuotas exigibles de aportes (feature 014, US3, T082; data-model §2.5, §3.5; FR-040, FR-049): el calendario de períodos por
/// periodicidad y el valor de la cuota por modo —valor fijo, fracción del ingreso registrado o fracción del salario mínimo— con el redondeo de la línea
/// y el reparto con el ahorro permanente. Sin reloj, sin base, sin valores legales: el salario mínimo y el ingreso entran por parámetro. (nuevo)
/// </summary>
public static class CuotasDeAportes
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Sin ingreso registrado no hay cuota por fracción del ingreso.</summary>
    public const string CodigoSinIngreso = "Lending.Contributions.IncomeMissing";

    /// <summary>Sin salario mínimo vigente no hay cuota por fracción del salario mínimo.</summary>
    public const string CodigoSinSalarioMinimo = "NationalValues.Missing";

    /// <summary>Una periodicidad que no admite cuotas periódicas (el pago único).</summary>
    public const string CodigoPeriodicidadInvalida = "Lending.Contributions.PeriodicityNotAllowed";

    // ------------------------------------------------------------------------------------------------------ calendario --

    /// <summary>¿Cuántos meses cubre un período de la periodicidad? 1 en las del mes o menores; 2, 3, 6 o 12 en las mayores. 0 = no admite cuotas.</summary>
    public static int MesesPorPeriodo(RepaymentPeriodicity p) => p switch
    {
        RepaymentPeriodicity.Weekly or RepaymentPeriodicity.TenDay or RepaymentPeriodicity.Biweekly or RepaymentPeriodicity.Monthly => 1,
        RepaymentPeriodicity.SinglePayment => 0,
        _ => (int)p / (int)RepaymentPeriodicity.Monthly,
    };

    /// <summary>
    /// Los subperíodos de un mes con límites fijos: semanal 1–7, 8–14, 15–21 y 22 al fin; decadal 1–10, 11–20 y 21 al fin; quincenal 1–15 y 16 al fin; el
    /// resto, el mes entero. El último absorbe los días que sobran.
    /// </summary>
    public static IReadOnlyList<(int Subperiodo, int DiaDesde, int DiaHasta)> Subperiodos(RepaymentPeriodicity p, int diasDelMes)
    {
        int[] inicios = p switch
        {
            RepaymentPeriodicity.Weekly => [1, 8, 15, 22],
            RepaymentPeriodicity.TenDay => [1, 11, 21],
            RepaymentPeriodicity.Biweekly => [1, 16],
            _ => [1],
        };
        return inicios.Select((d, i) => (i + 1, d, i + 1 < inicios.Length ? inicios[i + 1] - 1 : diasDelMes)).ToList();
    }

    /// <summary>El período que contiene <paramref name="dia"/>; nulo si la periodicidad no admite cuotas.</summary>
    public static PeriodoDeCuota? PeriodoDe(RepaymentPeriodicity p, byte? diaDeVencimiento, DateOnly dia)
    {
        var meses = MesesPorPeriodo(p);
        if (meses == 0) return null;
        if (meses > 1)
        {
            var primerMes = ((dia.Month - 1) / meses * meses) + 1;
            var desde = new DateOnly(dia.Year, primerMes, 1);
            var ultimoMes = desde.AddMonths(meses - 1);
            var hasta = new DateOnly(ultimoMes.Year, ultimoMes.Month, DateTime.DaysInMonth(ultimoMes.Year, ultimoMes.Month));
            return new PeriodoDeCuota(dia.Year, primerMes, 1, desde, hasta, Vencimiento(diaDeVencimiento, ultimoMes.Year, ultimoMes.Month, hasta));
        }
        var dias = DateTime.DaysInMonth(dia.Year, dia.Month);
        foreach (var (sub, d1, d2) in Subperiodos(p, dias))
        {
            if (dia.Day < d1 || dia.Day > d2) continue;
            var desde = new DateOnly(dia.Year, dia.Month, d1);
            var hasta = new DateOnly(dia.Year, dia.Month, d2);
            var vence = p == RepaymentPeriodicity.Monthly ? Vencimiento(diaDeVencimiento, dia.Year, dia.Month, hasta) : hasta;
            return new PeriodoDeCuota(dia.Year, dia.Month, sub, desde, hasta, vence);
        }
        throw new InvalidOperationException($"El día {dia:yyyy-MM-dd} no cae en ningún subperíodo de {p}.");
    }

    /// <summary>
    /// Los períodos que <b>ya empezaron</b> entre el que contiene <paramref name="desde"/> y el que contiene <paramref name="hasta"/>, en orden. Es lo
    /// que el paso de captaciones genera el día <paramref name="hasta"/> (una cuenta al día genera a lo sumo el del día; una atrasada, los que le
    /// faltan).
    /// </summary>
    public static IReadOnlyList<PeriodoDeCuota> Periodos(RepaymentPeriodicity p, byte? diaDeVencimiento, DateOnly desde, DateOnly hasta)
    {
        var lista = new List<PeriodoDeCuota>();
        if (hasta < desde) return lista;
        var actual = PeriodoDe(p, diaDeVencimiento, desde);
        while (actual is not null && actual.Desde <= hasta)
        {
            lista.Add(actual);
            actual = PeriodoDe(p, diaDeVencimiento, actual.Hasta.AddDays(1));
        }
        return lista;
    }

    private static DateOnly Vencimiento(byte? dia, int anio, int mes, DateOnly hasta) =>
        dia is { } d and > 0 ? new DateOnly(anio, mes, Math.Min(d, DateTime.DaysInMonth(anio, mes))) : hasta;

    // ------------------------------------------------------------------------------------------------------------ valor --

    /// <summary>
    /// La cuota de un período (FR-040, FR-049): el total por modo, redondeado a la unidad de la línea; la parte del permanente (fracción × total, al
    /// centavo y con el mismo redondeo de la línea) y la de aportes = total − permanente, así que la suma cuadra siempre. Falla sin la base que el modo
    /// pide.
    /// </summary>
    public static (CuotaDeAportesCalculada? Cuota, ProblemaDeCartera? Problema) Calcular(EntradaDeCuotaDeAportes e)
    {
        ArgumentNullException.ThrowIfNull(e);
        decimal bruto;
        decimal? baseUsada = null;
        string explicacion;
        switch (e.Modo)
        {
            case ContributionAmountMode.FixedAmount:
                bruto = e.Valor ?? 0m;
                explicacion = $"Valor fijo de la línea {N(bruto)}.";
                break;
            case ContributionAmountMode.IncomeFraction:
                if (e.Ingreso is not { } ingreso || ingreso <= 0m)
                    return (null, new ProblemaDeCartera(CodigoSinIngreso,
                        "El asociado no tiene ingreso registrado: la cuota de aportes es una fracción del ingreso y no se puede calcular."));
                baseUsada = ingreso;
                bruto = ingreso * (e.Fraccion ?? 0m);
                explicacion = $"Ingreso registrado {N(ingreso)} × {N(e.Fraccion ?? 0m)} = {N(bruto)}.";
                break;
            case ContributionAmountMode.MinimumWageFraction:
                if (e.SalarioMinimo is not { } smmlv || smmlv <= 0m)
                    return (null, new ProblemaDeCartera(CodigoSinSalarioMinimo,
                        "No hay salario mínimo vigente registrado: la cuota de aportes es una fracción del salario mínimo y no se puede calcular."));
                baseUsada = smmlv;
                bruto = smmlv * (e.Fraccion ?? 0m);
                explicacion = $"Salario mínimo {N(smmlv)} × {N(e.Fraccion ?? 0m)} = {N(bruto)}.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(e), e.Modo, "Modo de cuota desconocido.");
        }

        var total = Redondear(bruto, e.UnidadDeRedondeo, e.Redondeo);
        if (total != bruto) explicacion += $" Redondeada a {N(total)}.";
        var permanente = 0m;
        if (e.FraccionPermanente is { } f && f > 0m)
        {
            permanente = Math.Min(total, Redondear(total * f, RedondeoDeCartera.Centavo, e.Redondeo));
            explicacion += $" Ahorro permanente {N(f)} × {N(total)} = {N(permanente)}; aportes {N(total - permanente)}.";
        }
        return (new CuotaDeAportesCalculada(total, total - permanente, permanente, baseUsada, explicacion), null);
    }

    /// <summary>A la unidad de la línea con su modo; con unidad cero o negativa, al centavo.</summary>
    public static decimal Redondear(decimal valor, decimal unidad, RoundingMode modo) =>
        RedondeoDeCartera.AUnidad(valor, unidad > 0m ? unidad : RedondeoDeCartera.Centavo, modo);

    private static string N(decimal v) => v.ToString("#,##0.######", Inv);
}
