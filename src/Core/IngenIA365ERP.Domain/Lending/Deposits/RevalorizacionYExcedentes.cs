using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>Un movimiento de los aportes de una cuenta por su fecha valor (con signo: + entra, − sale; un reverso resta su original). (nuevo)</summary>
public sealed record MovimientoDeAportes(DateOnly FechaValor, decimal Monto);

/// <summary>
/// Un asociado ante la decisión de la asamblea (feature 014, US12, T201): su persona, su cuenta de aportes, los movimientos de sus aportes (para la base) y,
/// en una amortización, lo que se le puede devolver sin perforar su mínimo no reducible (nulo = sin tope). (nuevo)
/// </summary>
public sealed record AsociadoEnLaDistribucion(int PersonaId, int CuentaId, IReadOnlyList<MovimientoDeAportes> Movimientos, decimal? MaximoAmortizable = null);

/// <summary>
/// Lo que la asamblea decidió, tal como lo recibe el motor (feature 014, US12, T201; FR-045 a FR-047; D13): la clase, el ejercicio cuyos excedentes se
/// aplican, la base (saldo promedio del ejercicio o saldo al cierre), el porcentaje <b>o</b> el valor total decidido, el fondo aprobado en el acta, el límite
/// legal de la revalorización en fracción (el IPC del ejercicio; nulo = no aplica: excedentes y amortización), la unidad de redondeo de la entidad
/// (<c>Asamblea.Redondeo</c>: 1 = al peso, 0,01 = al centavo) y los asociados. (nuevo)
/// </summary>
public sealed record PedidoDeDistribucion(
    AssemblyDistributionKind Clase,
    int Ejercicio,
    RevaluationBase Base,
    decimal? Porcentaje,
    decimal? Valor,
    decimal Fondo,
    decimal? LimiteLegal,
    decimal UnidadDeRedondeo,
    IReadOnlyList<AsociadoEnLaDistribucion> Asociados);

/// <summary>
/// La línea de un asociado (T201): la base, lo calculado con la tasa aplicada (redondeado a la unidad de la entidad), lo que se le abona o devuelve después
/// del prorrateo al fondo y si el mínimo no reducible acotó su amortización. (nuevo)
/// </summary>
public sealed record LineaDeDistribucion(int PersonaId, int CuentaId, decimal Base, decimal Calculado, decimal Aplicado, bool LimitadaPorMinimo);

/// <summary>
/// El resultado del motor (T201): la tasa decidida (la del porcentaje, o el valor sobre la base total) y la aplicada (la decidida, o el límite legal si la
/// supera), lo calculado, si se prorrateó al fondo y con qué factor, lo aplicado (Σ de las líneas: igual al fondo si se prorrateó), lo que queda en el fondo,
/// las líneas en orden de persona y cuenta, y la explicación en texto para la pantalla. (nuevo)
/// </summary>
public sealed record DistribucionCalculada(
    decimal BaseTotal,
    decimal TasaDecidida,
    decimal TasaAplicada,
    bool LimitadaPorElLimiteLegal,
    decimal TotalCalculado,
    bool Prorrateada,
    decimal? Factor,
    decimal TotalAplicado,
    decimal Remanente,
    IReadOnlyList<LineaDeDistribucion> Lineas,
    IReadOnlyList<string> Explicacion);

/// <summary>
/// El motor puro de la revalorización de aportes, la distribución de excedentes y la amortización de aportes que decide la asamblea (feature 014, US12,
/// T201; FR-025, FR-045 a FR-047; D13; research §22). Sin reloj, sin base de datos y sin valores legales: el límite legal (el IPC del ejercicio, Ley 79 de
/// 1988 art. 54 num. 1) y el redondeo llegan del pedido.
///
/// <list type="number">
/// <item><b>Base</b> de cada asociado: el <b>saldo promedio diario</b> de sus aportes en el ejercicio (Σ del saldo de cada día del 1 de enero al 31 de
/// diciembre, por fecha valor, ÷ los días del año —365 o 366—; los días antes de su primer aporte valen cero) o el <b>saldo al cierre</b> (31 de diciembre),
/// a dos decimales al par. Sin base positiva no hay línea.</item>
/// <item><b>Tasa</b>: el porcentaje decidido, o el valor decidido sobre la base total. En la revalorización la tasa aplicada nunca supera el límite legal: si
/// lo supera, se aplica el límite y se informa.</item>
/// <item><b>Calculado</b> de cada línea: con porcentaje (o con la tasa limitada), base × tasa redondeado a la unidad mitad hacia arriba; con valor (sin
/// límite), el valor redondeado a la unidad repartido por la base con el método del mayor residuo, para que la suma sea exactamente el valor. En la
/// amortización, nunca más que lo amortizable del asociado (su mínimo no reducible no se perfora, FR-042).</item>
/// <item><b>Prorrateo al fondo</b> (US12-1): si lo calculado supera el fondo, el fondo (a la unidad, hacia abajo) se reparte por lo calculado con el mayor
/// residuo: la suma de lo aplicado es exactamente el fondo. Si no, se aplica lo calculado y el resto se queda en el fondo.</item>
/// </list>
/// El mayor residuo desempata por la persona y luego la cuenta (el orden es reproducible). (nuevo)
/// </summary>
public static class RevalorizacionYExcedentes
{
    /// <summary>Las unidades de redondeo que admite la entidad: al peso o al centavo.</summary>
    public static IReadOnlyList<decimal> UnidadesAdmitidas { get; } = [1m, 0.01m];

    public static DistribucionCalculada Calcular(PedidoDeDistribucion pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.Porcentaje.HasValue == pedido.Valor.HasValue)
            throw new ArgumentException("La decisión lleva un porcentaje o un valor, uno de los dos.", nameof(pedido));
        if (pedido.Porcentaje is < 0m || pedido.Valor is < 0m)
            throw new ArgumentOutOfRangeException(nameof(pedido), "El porcentaje y el valor no son negativos.");
        if (pedido.Fondo < 0m) throw new ArgumentOutOfRangeException(nameof(pedido), "El fondo no es negativo.");
        if (pedido.LimiteLegal is < 0m) throw new ArgumentOutOfRangeException(nameof(pedido), "El límite legal no es negativo.");
        if (!UnidadesAdmitidas.Contains(pedido.UnidadDeRedondeo))
            throw new ArgumentOutOfRangeException(nameof(pedido), "La unidad de redondeo es 1 (al peso) o 0,01 (al centavo).");

        var unidad = pedido.UnidadDeRedondeo;
        var explicacion = new List<string>();
        var bases = pedido.Asociados
            .Select(a => (a.PersonaId, a.CuentaId, a.MaximoAmortizable,
                Base: pedido.Base == RevaluationBase.YearEndBalance ? SaldoAlCierre(a.Movimientos, pedido.Ejercicio) : SaldoPromedio(a.Movimientos, pedido.Ejercicio)))
            .Where(a => a.Base > 0m)
            .OrderBy(a => a.PersonaId).ThenBy(a => a.CuentaId)
            .ToList();
        var baseTotal = bases.Sum(b => b.Base);
        explicacion.Add(string.Create(CultureInfo.InvariantCulture,
            $"Base: {(pedido.Base == RevaluationBase.YearEndBalance ? "saldo de aportes al 31 de diciembre" : "saldo promedio diario de aportes")} de {pedido.Ejercicio}, "
            + $"{bases.Count} asociado(s), total {baseTotal:N2}."));

        if (bases.Count == 0 || baseTotal == 0m)
        {
            explicacion.Add("Ningún asociado tiene base positiva: no hay nada que distribuir.");
            return new DistribucionCalculada(0m, pedido.Porcentaje ?? 0m, 0m, false, 0m, false, null, 0m, pedido.Fondo, [], explicacion);
        }

        var tasaDecidida = pedido.Porcentaje ?? pedido.Valor!.Value / baseTotal;
        var limitada = pedido.Clase == AssemblyDistributionKind.Revaluation && pedido.LimiteLegal is { } limite && tasaDecidida > limite;
        var tasa = limitada ? pedido.LimiteLegal!.Value : tasaDecidida;
        if (pedido.Porcentaje is { } p)
            explicacion.Add(string.Create(CultureInfo.InvariantCulture, $"Porcentaje decidido: {p * 100m:0.######} %."));
        else
            explicacion.Add(string.Create(CultureInfo.InvariantCulture,
                $"Valor decidido: {pedido.Valor:N2}, el {tasaDecidida * 100m:0.######} % de la base total."));
        if (limitada)
            explicacion.Add(string.Create(CultureInfo.InvariantCulture,
                $"La revalorización no supera el límite legal del ejercicio ({pedido.LimiteLegal!.Value * 100m:0.######} %): se aplica el límite."));

        // Lo calculado por línea: el valor exacto repartido por la base si se decidió un valor y no lo cortó el límite; si no, base × tasa a la unidad.
        IReadOnlyList<decimal> calculado = pedido.Valor is { } valor && !limitada
            ? MayorResto(Abajo(valor, unidad), bases.Select(b => b.Base).ToList(), unidad)
            : bases.Select(b => Redondear(b.Base * tasa, unidad)).ToList();

        var acotadas = new bool[bases.Count];
        if (pedido.Clase == AssemblyDistributionKind.Amortization)
        {
            var c = calculado.ToArray();
            for (var i = 0; i < bases.Count; i++)
                if (bases[i].MaximoAmortizable is { } maximo && c[i] > Math.Max(0m, Abajo(maximo, unidad)))
                {
                    c[i] = Math.Max(0m, Abajo(maximo, unidad));
                    acotadas[i] = true;
                }
            calculado = c;
            if (acotadas.Any(a => a))
                explicacion.Add($"{acotadas.Count(a => a)} asociado(s) se amortizan hasta su mínimo no reducible: no se les devuelve más.");
        }

        var totalCalculado = calculado.Sum();
        var fondo = Abajo(pedido.Fondo, unidad);
        IReadOnlyList<decimal> aplicado;
        decimal? factor = null;
        var prorrateada = totalCalculado > fondo;
        if (prorrateada)
        {
            aplicado = MayorResto(fondo, calculado, unidad);
            factor = decimal.Round(fondo / totalCalculado, 6, MidpointRounding.ToEven);
            explicacion.Add(string.Create(CultureInfo.InvariantCulture,
                $"Lo calculado ({totalCalculado:N2}) supera el fondo aprobado ({fondo:N2}): se prorratea al fondo (factor {factor:0.######})."));
        }
        else
        {
            aplicado = calculado;
            explicacion.Add(string.Create(CultureInfo.InvariantCulture,
                $"Lo calculado ({totalCalculado:N2}) cabe en el fondo aprobado ({pedido.Fondo:N2}); quedan {pedido.Fondo - totalCalculado:N2} en el fondo."));
        }

        var lineas = bases.Select((b, i) => new LineaDeDistribucion(b.PersonaId, b.CuentaId, b.Base, calculado[i], aplicado[i], acotadas[i]))
            .Where(l => l.Calculado != 0m || l.Aplicado != 0m || l.LimitadaPorMinimo)
            .ToList();
        var totalAplicado = aplicado.Sum();
        return new DistribucionCalculada(baseTotal, tasaDecidida, tasa, limitada, totalCalculado, prorrateada, factor, totalAplicado, pedido.Fondo - totalAplicado,
            lineas, explicacion);
    }

    /// <summary>
    /// El saldo promedio diario de los aportes en el ejercicio (D13): Σ del saldo de cada día del 1 de enero al 31 de diciembre por fecha valor ÷ los días del
    /// año, a dos decimales al par. Lo anterior al ejercicio cuenta desde el primer día; lo posterior, no.
    /// </summary>
    public static decimal SaldoPromedio(IReadOnlyList<MovimientoDeAportes> movimientos, int ejercicio)
    {
        ArgumentNullException.ThrowIfNull(movimientos);
        var inicio = new DateOnly(ejercicio, 1, 1);
        var cierre = new DateOnly(ejercicio, 12, 31);
        var dias = cierre.DayNumber - inicio.DayNumber + 1;
        var suma = 0m;
        foreach (var m in movimientos)
        {
            if (m.FechaValor > cierre) continue;
            var desde = m.FechaValor < inicio ? inicio : m.FechaValor;
            suma += m.Monto * (cierre.DayNumber - desde.DayNumber + 1);
        }
        return decimal.Round(suma / dias, 2, MidpointRounding.ToEven);
    }

    /// <summary>El saldo de los aportes al 31 de diciembre del ejercicio (por fecha valor).</summary>
    public static decimal SaldoAlCierre(IReadOnlyList<MovimientoDeAportes> movimientos, int ejercicio)
    {
        ArgumentNullException.ThrowIfNull(movimientos);
        var cierre = new DateOnly(ejercicio, 12, 31);
        return movimientos.Where(m => m.FechaValor <= cierre).Sum(m => m.Monto);
    }

    /// <summary>
    /// Reparte <paramref name="total"/> (múltiplo de la unidad) entre los pesos con el método del mayor residuo: cada parte es su porción hacia abajo a la
    /// unidad y las unidades que faltan van a los mayores residuos (en empate, a la primera). La suma es exactamente el total.
    /// </summary>
    public static IReadOnlyList<decimal> MayorResto(decimal total, IReadOnlyList<decimal> pesos, decimal unidad)
    {
        ArgumentNullException.ThrowIfNull(pesos);
        var suma = pesos.Sum();
        if (pesos.Count == 0 || suma <= 0m || total <= 0m) return pesos.Select(_ => 0m).ToList();
        var exactas = pesos.Select(p => total * p / suma).ToList();
        var partes = exactas.Select(e => Abajo(e, unidad)).ToArray();
        var faltan = (int)decimal.Round((total - partes.Sum()) / unidad, 0, MidpointRounding.ToEven);
        foreach (var i in exactas.Select((e, i) => (Resto: e - partes[i], i)).OrderByDescending(x => x.Resto).ThenBy(x => x.i).Take(Math.Max(0, faltan))
                     .Select(x => x.i))
            partes[i] += unidad;
        return partes;
    }

    /// <summary>A la unidad, mitad hacia arriba (el redondeo de la entidad).</summary>
    public static decimal Redondear(decimal valor, decimal unidad) =>
        decimal.Round(valor / unidad, 0, MidpointRounding.AwayFromZero) * unidad;

    /// <summary>A la unidad, hacia abajo (lo que cabe en un fondo).</summary>
    public static decimal Abajo(decimal valor, decimal unidad) => decimal.Floor(valor / unidad) * unidad;
}
