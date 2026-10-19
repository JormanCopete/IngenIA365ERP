using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Las condiciones selladas de un ahorro contractual con que se arma su plan (feature 014, US7, T181; data-model §2.1 «Contractual», §3.3; FR-024, FR-074): la
/// cuota pactada, cada cuánto se paga, el plazo en meses, el día en que empieza, la tasa base (la de la línea o la pactada), la bonificada por cumplimiento y la
/// de castigo si la línea las declara, la convención de días (la base B) y el redondeo de la línea. (nuevo)
/// </summary>
public sealed record CondicionesDeContractual(
    decimal Cuota,
    RepaymentPeriodicity Periodicidad,
    int PlazoMeses,
    DateOnly Inicio,
    decimal TasaBase,
    decimal? TasaBonificada,
    decimal? TasaCastigo,
    DayCountConvention Convencion,
    RedondeoDeCartera Redondeo);

/// <summary>Una cuota del plan del contractual: su número y su vencimiento (el fin de su período). (nuevo)</summary>
public sealed record CuotaDeContractual(int Numero, DateOnly Vencimiento);

/// <summary>
/// Un aporte al contrato con su fecha valor: una cuota pagada (o lo que se consignó por cuotas). El reverso de un aporte entra con su valor negativo y la misma
/// fecha. Lo que no es aporte —el interés abonado, la retención, el GMF, la bonificación— no entra. (nuevo)
/// </summary>
public sealed record AporteDeContractual(DateOnly Fecha, decimal Monto);

/// <summary>
/// El plan de un contractual (FR-024, FR-074): el vencimiento (el inicio más el plazo en meses), las cuotas con su vencimiento, el plazo de emisión que decide la
/// cuenta contable del capital (FR-073, CUIF 2125 corto y largo plazo), lo pactado (cuota × cuotas) y lo proyectado si cada cuota se paga en su vencimiento:
/// el interés a la tasa base, a la bonificada y la bonificación. (nuevo)
/// </summary>
public sealed record PlanDeContractualCalculado(
    CondicionesDeContractual Condiciones,
    DateOnly Vencimiento,
    IReadOnlyList<CuotaDeContractual> Cuotas,
    IssueTermBand PlazoDeEmision,
    decimal TotalPactado,
    decimal InteresProyectadoBase,
    decimal? InteresProyectadoBonificado,
    decimal? BonificacionProyectada)
{
    /// <summary>El valor final a la tasa base (lo pactado más su interés).</summary>
    public decimal ValorFinalBase => TotalPactado + InteresProyectadoBase;

    /// <summary>El valor final si se cumple: a la tasa bonificada o, sin bonificación, a la base.</summary>
    public decimal ValorFinalCumpliendo => TotalPactado + (InteresProyectadoBonificado ?? InteresProyectadoBase);

    /// <summary>La cuota que vence en <paramref name="dia"/> o, si ninguna, nula.</summary>
    public CuotaDeContractual? CuotaQueVenceEl(DateOnly dia) => Cuotas.FirstOrDefault(c => c.Vencimiento == dia);

    /// <summary>Las cuotas exigibles el día <paramref name="dia"/>: la primera desde el inicio, cada una de las siguientes desde el vencimiento de la anterior.</summary>
    public IReadOnlyList<CuotaDeContractual> CuotasEmpezadas(DateOnly dia)
    {
        var lista = new List<CuotaDeContractual>();
        var anterior = Condiciones.Inicio;
        foreach (var c in Cuotas)
        {
            if (dia < anterior) break;
            lista.Add(c);
            anterior = c.Vencimiento;
        }
        return lista;
    }
}

/// <summary>
/// El interés de unos aportes a dos tasas y su diferencia (FR-024): la bonificación por cumplimiento (bonificada − base) o la penalidad del incumplimiento
/// (base − castigo), cada interés redondeado con el redondeo sellado y la diferencia nunca negativa. (nuevo)
/// </summary>
public sealed record DiferenciaDeTasasDelContractual(decimal Aportes, decimal InteresBase, decimal InteresOtraTasa, decimal Diferencia, string Explicacion)
{
    /// <summary>El valor final a la otra tasa (los aportes más su interés).</summary>
    public decimal ValorFinalOtraTasa => Aportes + InteresOtraTasa;

    /// <summary>El valor final a la tasa base.</summary>
    public decimal ValorFinalBase => Aportes + InteresBase;
}

/// <summary>Una cuota tal como la mide el cumplimiento: su número, su vencimiento, su valor, lo pagado y si está anulada. (nuevo)</summary>
public sealed record CuotaParaCumplimiento(int Numero, DateOnly Vencimiento, decimal Valor, decimal Pagado, bool Anulada = false)
{
    public decimal Pendiente => Anulada ? 0m : Math.Max(0m, Valor - Pagado);
}

/// <summary>
/// El cumplimiento de un contractual a un día (FR-074): las cuotas generadas, las pagadas, las atrasadas —vencidas hace más de los días de gracia y con saldo
/// pendiente— con sus números, si superan la tolerancia de la línea (entonces el contrato queda incumplido) y si está completo (todas las cuotas del plan
/// pagadas). (nuevo)
/// </summary>
public sealed record CumplimientoDelContractual(
    int Generadas,
    int Pagadas,
    int Atrasadas,
    IReadOnlyList<int> NumerosAtrasados,
    decimal ValorAtrasado,
    int Tolerancia,
    bool SuperaLaTolerancia,
    bool Completo);

/// <summary>
/// El motor del ahorro contractual o programado (feature 014, US7, T181; research §19; FR-024, FR-074, FR-075; investigación financiera §3.4). Puro: sin IO,
/// sin reloj y sin valores legales (las tasas, la convención, el redondeo, la tolerancia y la gracia llegan por parámetro). Reutiliza
/// <see cref="EquivalenciaDeTasas"/> (la tasa diaria compuesta de la base, a 18 decimales) y <see cref="ContadorDeDias"/>.
///
/// <list type="bullet">
/// <item><b>Plan</b> (<see cref="Plan"/>): el vencimiento es el inicio más el plazo en meses; las cuotas vencen cada período de la periodicidad desde el
/// inicio (las de meses enteros por meses calendario, las menores por días: semanal 7, decadal 10, quincenal 15) hasta el vencimiento.</item>
/// <item><b>Interés de los aportes</b> (<see cref="Interes"/>): con saldo diario y equivalencia compuesta, lo que gana un aporte A de su fecha valor al corte
/// —N días en la convención— es <c>A × ((1 + r_d)^N − 1)</c> aunque se capitalice cada período, porque capitalizar a la misma tasa compuesta no cambia el
/// valor final. Así la bonificación y la reliquidación se miden sobre los aportes del contrato, sin la retención ni el redondeo de cada liquidación
/// (investigación §3.4: 12 cuotas mensuales de 200.000 al 6 % EA dan 2.465.306; al 8 %, 2.486.777; la bonificación es 21.471).</item>
/// <item><b>Bonificación</b> (<see cref="Bonificacion"/>): el interés de los aportes a la tasa bonificada menos el de la base, cada uno redondeado.</item>
/// <item><b>Reliquidación a la tasa de castigo</b> (<see cref="Reliquidar"/>): el de la base menos el del castigo (lo que el incumplimiento le quita).</item>
/// <item><b>Cumplimiento</b> (<see cref="Cumplimiento"/>): una cuota está atrasada si venció hace más de los días de gracia y tiene saldo pendiente; más
/// atrasadas que las toleradas, incumplido.</item>
/// <item><b>Plazo de emisión</b> (<see cref="PlazoDeEmision"/>): el del CDAT por el plazo pactado con meses de 30 (CUIF 2125 separa corto y largo plazo).</item>
/// </list>
/// (nuevo)
/// </summary>
public static class AhorroContractual
{
    /// <summary>El plazo fuera del rango de la línea.</summary>
    public const string CodigoPlazoFueraDeRango = "Lending.Contractual.TermOutOfRange";

    /// <summary>La cuota bajo el mínimo de la línea.</summary>
    public const string CodigoCuotaBajoElMinimo = "Lending.Contractual.InstallmentBelowMinimum";

    /// <summary>Un plazo tan corto que no alcanza para una cuota de la periodicidad.</summary>
    public const string CodigoSinCuotas = "Lending.Contractual.NoInstallments";

    /// <summary>Meses de 30 días: el valor de <see cref="RepaymentPeriodicity.Monthly"/> (nunca un literal, CH39).</summary>
    private const int DiasDelMes = (int)RepaymentPeriodicity.Monthly;

    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    /// <summary>El vencimiento del contrato: el inicio más el plazo en meses calendario.</summary>
    public static DateOnly Vencimiento(DateOnly inicio, int plazoMeses)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(plazoMeses, 1);
        return inicio.AddMonths(plazoMeses);
    }

    /// <summary>El plazo de emisión (FR-073) por el plazo pactado en meses (meses de 30 días, como el CDAT; pendiente de validar por la contadora).</summary>
    public static IssueTermBand PlazoDeEmision(int plazoMeses) => PlanDeCdat.PlazoDeEmision(Math.Max(1, plazoMeses) * DiasDelMes);

    /// <summary>
    /// Las cuotas del plan: una por cada período de la periodicidad desde el inicio, con vencimiento hasta el del contrato. Las de meses enteros vencen el mismo
    /// día del mes que el inicio (o el último si el mes es más corto); las menores, cada tantos días.
    /// </summary>
    public static IReadOnlyList<CuotaDeContractual> Cuotas(DateOnly inicio, RepaymentPeriodicity periodicidad, DateOnly vencimiento)
    {
        if (periodicidad == RepaymentPeriodicity.SinglePayment || !Enum.IsDefined(periodicidad))
            throw new ArgumentOutOfRangeException(nameof(periodicidad), periodicidad, "La cuota del contractual es periódica.");
        var meses = CuotasDeAportes.MesesPorPeriodo(periodicidad);
        var porMeses = (int)periodicidad >= DiasDelMes;
        var lista = new List<CuotaDeContractual>();
        for (var k = 1; ; k++)
        {
            var vence = porMeses ? inicio.AddMonths(k * meses) : inicio.AddDays(k * (int)periodicidad);
            if (vence > vencimiento) break;
            lista.Add(new CuotaDeContractual(k, vence));
        }
        return lista;
    }

    /// <summary>
    /// Nulo si el contrato cabe en la línea: la cuota no baja del mínimo, el plazo está en el rango en meses y alcanza para al menos una cuota de la
    /// periodicidad.
    /// </summary>
    public static ProblemaDeCartera? ValidarContrato(decimal cuota, int plazoMeses, RepaymentPeriodicity periodicidad, decimal? cuotaMinima, int? mesesMinimo,
        int? mesesMaximo)
    {
        if (cuota <= 0m || (cuotaMinima is { } minima && cuota < minima))
            return ProblemaDeCartera.Con(CodigoCuotaBajoElMinimo,
                $"La cuota de {cuota.ToString("N0", Es)} no alcanza el mínimo de la línea ({(cuotaMinima ?? 0m).ToString("N0", Es)}).", "installment", cuota);
        if (plazoMeses < 1 || (mesesMinimo is { } min && plazoMeses < min) || (mesesMaximo is { } max && plazoMeses > max))
            return ProblemaDeCartera.Con(CodigoPlazoFueraDeRango,
                $"El plazo de {plazoMeses} meses está fuera del rango de la línea ({mesesMinimo?.ToString(Es) ?? "1"} a {mesesMaximo?.ToString(Es) ?? "sin máximo"} meses).",
                "termMonths", plazoMeses);
        if (periodicidad == RepaymentPeriodicity.SinglePayment || !Enum.IsDefined(periodicidad))
            return ProblemaDeCartera.Con(CodigoSinCuotas, "La cuota del contractual es periódica.", "installmentPeriodicity", periodicidad.ToString());
        return null;
    }

    /// <summary>
    /// El plan de unas condiciones (ver la clase): las cuotas, lo pactado y lo proyectado si cada cuota se paga el día de su vencimiento, al vencimiento del
    /// contrato. Sin cuotas (un plazo menor que la periodicidad) falla con <see cref="CodigoSinCuotas"/>.
    /// </summary>
    public static PlanDeContractualCalculado Plan(CondicionesDeContractual c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var vencimiento = Vencimiento(c.Inicio, c.PlazoMeses);
        var cuotas = Cuotas(c.Inicio, c.Periodicidad, vencimiento);
        if (cuotas.Count == 0)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoSinCuotas,
                $"Un plazo de {c.PlazoMeses} meses no alcanza para una cuota «{c.Periodicidad}».", "termMonths", c.PlazoMeses));
        var aportes = cuotas.Select(q => new AporteDeContractual(q.Vencimiento, c.Cuota)).ToList();
        var interesBase = c.Redondeo.Redondear(Interes(aportes, c.TasaBase, c.Convencion, vencimiento));
        decimal? interesBonificado = c.TasaBonificada is { } b ? c.Redondeo.Redondear(Interes(aportes, b, c.Convencion, vencimiento)) : null;
        decimal? bonificacion = interesBonificado is { } ib ? Math.Max(0m, ib - interesBase) : null;
        return new PlanDeContractualCalculado(c, vencimiento, cuotas, PlazoDeEmision(c.PlazoMeses), c.Cuota * cuotas.Count, interesBase, interesBonificado,
            bonificacion);
    }

    /// <summary>
    /// El interés exacto (sin redondear) de unos aportes a una tasa efectiva anual hasta <paramref name="corte"/>: Σ <c>A × ((1 + r_d)^N − 1)</c> con N los
    /// días de la fecha del aporte (excluida) al corte (incluido) en la convención. Un aporte con fecha en el corte o después no gana.
    /// </summary>
    public static decimal Interes(IEnumerable<AporteDeContractual> aportes, decimal tasaEfectivaAnual, DayCountConvention convencion, DateOnly corte)
    {
        ArgumentNullException.ThrowIfNull(aportes);
        var r = EquivalenciaDeTasas.TasaDiaria(tasaEfectivaAnual, convencion, RateEquivalence.Compound, RepaymentPeriodicity.SinglePayment);
        var total = 0m;
        foreach (var a in aportes)
        {
            if (a.Monto == 0m || a.Fecha >= corte) continue;
            var dias = ContadorDeDias.Dias(convencion, a.Fecha, corte);
            total += a.Monto * EquivalenciaDeTasas.TasaDelPeriodo(r, dias, RateEquivalence.Compound);
        }
        return total;
    }

    /// <summary>
    /// La bonificación por cumplimiento (FR-024, FR-075): el interés de los aportes a la tasa bonificada menos el de la tasa base, al corte, cada uno con el
    /// redondeo sellado; nunca negativa.
    /// </summary>
    public static DiferenciaDeTasasDelContractual Bonificacion(IReadOnlyList<AporteDeContractual> aportes, decimal tasaBase, decimal tasaBonificada,
        DayCountConvention convencion, DateOnly corte, RedondeoDeCartera redondeo)
    {
        ArgumentNullException.ThrowIfNull(redondeo);
        var total = aportes.Where(a => a.Fecha <= corte).Sum(a => a.Monto);
        var interesBase = redondeo.Redondear(Interes(aportes, tasaBase, convencion, corte));
        var bonificado = redondeo.Redondear(Interes(aportes, tasaBonificada, convencion, corte));
        var diferencia = Math.Max(0m, bonificado - interesBase);
        return new DiferenciaDeTasasDelContractual(total, interesBase, bonificado, diferencia,
            $"Aportes {total.ToString("N2", Es)} al {corte:yyyy-MM-dd}: interés a la tasa base {tasaBase.ToString("P4", Es)} = {interesBase.ToString("N2", Es)}; "
            + $"a la bonificada {tasaBonificada.ToString("P4", Es)} = {bonificado.ToString("N2", Es)}; bonificación {diferencia.ToString("N2", Es)}.");
    }

    /// <summary>
    /// La reliquidación a la tasa de castigo (FR-024, FR-074): el interés de los aportes a la tasa base menos el de la tasa de castigo, al corte, cada uno con
    /// el redondeo sellado; nunca negativa (un castigo mayor que la base no da interés de más).
    /// </summary>
    public static DiferenciaDeTasasDelContractual Reliquidar(IReadOnlyList<AporteDeContractual> aportes, decimal tasaBase, decimal tasaCastigo,
        DayCountConvention convencion, DateOnly corte, RedondeoDeCartera redondeo)
    {
        ArgumentNullException.ThrowIfNull(redondeo);
        var total = aportes.Where(a => a.Fecha <= corte).Sum(a => a.Monto);
        var interesBase = redondeo.Redondear(Interes(aportes, tasaBase, convencion, corte));
        var castigo = redondeo.Redondear(Interes(aportes, tasaCastigo, convencion, corte));
        var diferencia = Math.Max(0m, interesBase - castigo);
        return new DiferenciaDeTasasDelContractual(total, interesBase, castigo, diferencia,
            $"Aportes {total.ToString("N2", Es)} al {corte:yyyy-MM-dd}: interés a la tasa base {tasaBase.ToString("P4", Es)} = {interesBase.ToString("N2", Es)}; "
            + $"a la de castigo {tasaCastigo.ToString("P4", Es)} = {castigo.ToString("N2", Es)}; la reliquidación quita {diferencia.ToString("N2", Es)}.");
    }

    /// <summary>
    /// El cumplimiento a <paramref name="dia"/> (FR-074): una cuota está atrasada si su vencimiento más los días de gracia ya pasó (es anterior al día) y le
    /// queda saldo; con más atrasadas que la tolerancia, el contrato queda incumplido. Completo = las <paramref name="cuotasDelPlan"/> cuotas pagadas.
    /// </summary>
    public static CumplimientoDelContractual Cumplimiento(IReadOnlyList<CuotaParaCumplimiento> cuotas, int cuotasDelPlan, DateOnly dia, int tolerancia,
        int diasDeGracia)
    {
        ArgumentNullException.ThrowIfNull(cuotas);
        var vivas = cuotas.Where(c => !c.Anulada).ToList();
        var atrasadas = vivas.Where(c => c.Pendiente > 0m && c.Vencimiento.AddDays(Math.Max(0, diasDeGracia)) < dia).OrderBy(c => c.Numero).ToList();
        var pagadas = vivas.Count(c => c.Pendiente == 0m);
        var toleradas = Math.Max(0, tolerancia);
        return new CumplimientoDelContractual(vivas.Count, pagadas, atrasadas.Count, atrasadas.Select(c => c.Numero).ToList(), atrasadas.Sum(c => c.Pendiente),
            toleradas, atrasadas.Count > toleradas, pagadas >= cuotasDelPlan && vivas.All(c => c.Pendiente == 0m));
    }

    /// <summary>
    /// Los aportes de las primeras <paramref name="pagadas"/> cuotas del plan fechados en su vencimiento: lo que un contrato migrado aportó antes del corte
    /// (la carga trae cuántas cuotas iban pagadas, no sus fechas; pendiente de validar por la contadora).
    /// </summary>
    public static IReadOnlyList<AporteDeContractual> AportesDelPlan(PlanDeContractualCalculado plan, int pagadas) =>
        plan.Cuotas.Take(Math.Max(0, pagadas)).Select(q => new AporteDeContractual(q.Vencimiento, plan.Condiciones.Cuota)).ToList();
}
