using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Rates;

/// <summary>
/// La usura certificada que se usa: la tasa (EA, fracción), el <c>PublicId</c> del valor nacional <c>USURA</c> de donde sale, la
/// clave de la modalidad y desde cuándo rige. La resuelve la aplicación con el lector de valores nacionales a la fecha; el motor no
/// conoce ninguna. (nuevo)
/// </summary>
public sealed record UsuraCertificada(decimal Tasa, Guid PublicId, string Clave, DateOnly VigenteDesde);

/// <summary>Un cargo que llega al deudor y computa para la usura (FR-021): un seguro que retiene o autoasegura, o un cargo al desembolso.</summary>
public sealed record CargoDeUsura(string Codigo, decimal Valor);

/// <summary>Qué dijo el control. (nuevo)</summary>
public enum VeredictoDeUsura
{
    /// <summary>La efectiva con cargos no supera la usura.</summary>
    Pasa = 1,

    /// <summary>La supera: bloquea la línea o la originación (<c>Lending.Usury.Exceeded</c>).</summary>
    Excede = 2,

    /// <summary>No hay usura certificada del día: la aplicación responde <c>NationalValues.Missing</c> y alerta.</summary>
    SinUsura = 3,
}

/// <summary>
/// El resultado de <see cref="ControlDeUsura.Evaluar"/> (feature 013, T28, R36; FR-021): la efectiva anual con los cargos
/// computables y sin ellos, la sobretasa de cargos (la diferencia, que el desembolso de US3 sella), la usura usada (valor,
/// <c>PublicId</c> y clave), la holgura (usura − efectiva; negativa si excede), los cargos computables y la explicación. (nuevo)
/// </summary>
public sealed record ResultadoDeUsura(
    VeredictoDeUsura Veredicto,
    decimal EfectivaConCargos,
    decimal EfectivaSinCargos,
    decimal SobretasaDeCargos,
    decimal? Usura,
    Guid? UsuraPublicId,
    string? ClaveDeUsura,
    decimal? Holgura,
    decimal CargosComputables,
    IReadOnlyList<CargoDeUsura> Cargos,
    ExplicacionDeCartera Explicacion)
{
    public bool Pasa => Veredicto == VeredictoDeUsura.Pasa;

    /// <summary>Lo que falta para quedar bajo la usura (positivo si excede).</summary>
    public decimal? Diferencia => Holgura is { } h ? -h : null;
}

/// <summary>
/// El control de usura dentro del motor (feature 013, T28, R36, M7; FR-021, SC-002): compara la <b>efectiva resultante</b> —la TIR
/// diaria de los flujos con sus <b>fechas reales</b> y los cargos computables, anualizada sobre 365 días (D3)—, nunca la tasa nominal
/// ni la pactada, con la usura de la modalidad certificada.
///
/// <list type="bullet">
/// <item>Flujos: el desembolso (monto menos los cargos que se descuentan al desembolsar) el día del desembolso; cada cuota (capital
/// + interés + seguros que computan) el día de su vencimiento. El día t es el número de días reales desde el desembolso.</item>
/// <item>Un seguro computa si la cooperativa retiene parte de la prima (<c>RetainedFraction &gt; 0</c>) o se autoasegura
/// (<c>SelfInsured</c>): computa la prima entera, del lado conservador, hasta que la contadora diga otra cosa (FR-021). El que no
/// retiene no computa.</item>
/// <item>TIR diaria por Newton con respaldo de bisección (<see cref="AritmeticaDecimal.Tir"/>), sembrada con la tasa diaria del plan;
/// efectiva = <c>(1 + TIR)^365 − 1</c>, a 18 decimales. Real/360 sobre una EA sube la efectiva y puede bloquear.</item>
/// <item>Sin usura: el resultado dice <see cref="VeredictoDeUsura.SinUsura"/> con las efectivas calculadas.</item>
/// </list>
/// Puro: sin IO ni reloj. (nuevo)
/// </summary>
public static class ControlDeUsura
{
    /// <summary>Días sobre los que se anualiza la TIR diaria: el año calendario de base fija (D3).</summary>
    public const int DiasDelAnio = ContadorDeDias.AnioReal;

    /// <summary>El código con que la aplicación responde una efectiva que supera la usura.</summary>
    public const string CodigoExcedida = "Lending.Usury.Exceeded";

    /// <summary>Evalúa el plan contra la usura; <paramref name="cargosAlDesembolso"/> se descuentan del dinero que recibe el deudor.</summary>
    public static ResultadoDeUsura Evaluar(PlanDePagos plan, UsuraCertificada? usura, IReadOnlyList<CargoDeUsura>? cargosAlDesembolso = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        cargosAlDesembolso ??= [];
        var inicio = plan.Entrada.FechaDesembolso;
        var alDesembolso = cargosAlDesembolso.Sum(c => c.Valor);

        var sinCargos = new List<(DateOnly, decimal)> { (inicio, -plan.Entrada.Monto) };
        var conCargos = new List<(DateOnly, decimal)> { (inicio, -(plan.Entrada.Monto - alDesembolso)) };
        foreach (var c in plan.Cuotas)
        {
            sinCargos.Add((c.Vencimiento, c.Capital + c.Interes));
            conCargos.Add((c.Vencimiento, c.Capital + c.Interes + c.SeguroComputable));
        }

        var cargos = plan.Cuotas.SelectMany(c => c.Seguros).Where(s => s.Computa)
            .GroupBy(s => s.Codigo, StringComparer.Ordinal)
            .Select(g => new CargoDeUsura(g.Key, g.Sum(s => s.Valor)))
            .Concat(cargosAlDesembolso)
            .Where(c => c.Valor != 0m)
            .ToList();
        var totalDeCargos = cargos.Sum(c => c.Valor);

        var efectivaSinCargos = EfectivaAnual(inicio, sinCargos, plan.TasaDiaria);
        var efectivaConCargos = totalDeCargos == 0m ? efectivaSinCargos : EfectivaAnual(inicio, conCargos, plan.TasaDiaria);
        var sobretasa = efectivaConCargos - efectivaSinCargos;

        var explicacion = new ExplicacionDeCartera()
            .Linea($"Efectiva resultante: TIR diaria de los flujos con fechas reales, anualizada sobre {DiasDelAnio} días: "
                + $"{ExplicacionDeCartera.Numero(efectivaConCargos)} con cargos, {ExplicacionDeCartera.Numero(efectivaSinCargos)} sin cargos "
                + $"(sobretasa de cargos {ExplicacionDeCartera.Numero(sobretasa)}).");
        explicacion.Linea(cargos.Count == 0
            ? "Ningún cargo computa para la usura."
            : "Cargos que computan: " + string.Join(", ", cargos.Select(c => $"{c.Codigo} {ExplicacionDeCartera.Numero(c.Valor)}")) + ".");

        if (usura is null)
        {
            explicacion.Linea("No hay usura certificada para la modalidad a la fecha: no se puede afirmar que la línea la respete.");
            return new(VeredictoDeUsura.SinUsura, efectivaConCargos, efectivaSinCargos, sobretasa, null, null, null, null, totalDeCargos, cargos, explicacion);
        }

        var holgura = usura.Tasa - efectivaConCargos;
        var veredicto = efectivaConCargos > usura.Tasa ? VeredictoDeUsura.Excede : VeredictoDeUsura.Pasa;
        explicacion.Linea($"Usura {usura.Clave} vigente desde {ExplicacionDeCartera.Fecha(usura.VigenteDesde)}: {ExplicacionDeCartera.Numero(usura.Tasa)} "
            + $"(valor nacional {usura.PublicId:D}); holgura {ExplicacionDeCartera.Numero(holgura)}: "
            + (veredicto == VeredictoDeUsura.Pasa ? "pasa." : "la supera."));
        return new(veredicto, efectivaConCargos, efectivaSinCargos, sobretasa, usura.Tasa, usura.PublicId, usura.Clave, holgura, totalDeCargos, cargos, explicacion);
    }

    /// <summary>
    /// La efectiva anual de flujos fechados: la TIR diaria r de <c>Σ F / (1 + r)^t</c> con t los días reales desde
    /// <paramref name="inicio"/>, y <c>(1 + r)^365 − 1</c> a 18 decimales.
    /// </summary>
    public static decimal EfectivaAnual(DateOnly inicio, IReadOnlyList<(DateOnly Fecha, decimal Valor)> flujos, decimal? semillaDiaria = null)
    {
        ArgumentNullException.ThrowIfNull(flujos);
        if (flujos.Count == 0) throw new ArgumentException("No hay flujos.", nameof(flujos));
        var ultimo = flujos.Max(f => f.Fecha.DayNumber) - inicio.DayNumber;
        if (flujos.Any(f => f.Fecha < inicio)) throw new ArgumentException("Un flujo es anterior al desembolso.", nameof(flujos));

        var porDia = new decimal[ultimo + 1];
        foreach (var (fecha, valor) in flujos) porDia[fecha.DayNumber - inicio.DayNumber] += valor;

        var tir = AritmeticaDecimal.Tir(porDia, semillaDiaria);
        return EquivalenciaDeTasas.EfectivaDeTasaDiaria(tir.Tasa, DiasDelAnio);
    }
}
