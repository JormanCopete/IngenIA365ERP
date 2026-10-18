using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// Una fila del historial de mora (<c>LND_LoanArrearsHistory</c>, data-model §23.5): crédito, mes, mora máxima del mes, origen y
/// <see cref="Secuencia"/> (orden de registro: entre dos cargas complementarias manda la más reciente). Las de origen
/// <c>MonthlyClose</c> sólo cuentan si su cierre existe y está vigente (<see cref="Vigente"/>).
/// </summary>
public sealed record EntradaDeHistorialDeMora(int LoanId, int Anio, int Mes, int MaxDiasDeMora, ArrearsHistoryOrigin Origen, long Secuencia)
{
    public bool Vigente { get; init; } = true;

    /// <summary>
    /// K6a (T1015; data-model §23.5 <c>PredecessorLoanNumber</c>): el número del crédito novado o refinanciado que <b>no nació en Cartera</b> y
    /// cuyo historial migrado se cargó en este crédito. Cuenta como otro eslabón de la cadena: si el mes lo tienen los dos, manda la mayor mora.
    /// </summary>
    public string? NumeroDelAntecesor { get; init; }
}

/// <summary>
/// Cómo se completan los meses de historial que faltan (<c>Riesgo.ReglaMesesFaltantes</c>, K6a, T1015; R57): con la <b>mayor mora observada</b>
/// en la ventana (el defecto, pendiente de validar), con la <b>mora del corte</b> de la migración, o con un número <b>fijo</b> de días
/// (<c>Riesgo.MesesFaltantesDiasFijos</c>). Los nombres son los valores del parámetro.
/// </summary>
public enum ReglaDeMesesFaltantes
{
    MayorObservada = 1,
    DelCorte = 2,
    Fija = 3,
}

/// <summary>La mora máxima de un mes del historial completado: observada (fila o transacciones) o puesta por la regla de meses faltantes.</summary>
public sealed record MoraMensualCompleta(int Anio, int Mes, int MaxDiasDeMora, bool PorRegla);

/// <summary>El historial con todos sus meses, los que se completaron por regla y la explicación.</summary>
public sealed record HistorialDeMoraCompletado(IReadOnlyList<MoraMensualCompleta> Meses, ReglaDeMesesFaltantes? ReglaAplicada, ExplicacionDeCartera Explicacion)
{
    public IReadOnlyList<MoraMensualCompleta> Completados => Meses.Where(m => m.PorRegla).ToList();
}

/// <summary>
/// Un crédito para su historial: <see cref="InicioDeLaCadena"/> es el desembolso del crédito o, si viene de novaciones o
/// refinanciaciones, del más antiguo de la cadena; <see cref="Antecesores"/> son los créditos cuya historia hereda (del más cercano al
/// más lejano).
/// </summary>
public sealed record CreditoParaHistorial(int LoanId, DateOnly InicioDeLaCadena)
{
    public IReadOnlyList<int> Antecesores { get; init; } = [];
}

/// <summary>Un mes del historial: la mora que manda, de qué fuente y de qué crédito, o que lo cubren las transacciones.</summary>
public sealed record MesDeHistorialDeMora(int Anio, int Mes, int? MaxDiasDeMora, ArrearsHistoryOrigin? Origen, int? LoanIdDeLaFuente, bool CubiertoPorTransacciones)
{
    public bool Cubierto => MaxDiasDeMora is not null || CubiertoPorTransacciones;
}

/// <summary>El historial construido: los meses de la ventana, si está completo y los que faltan.</summary>
public sealed record HistorialDeMoraConstruido(IReadOnlyList<MesDeHistorialDeMora> Meses, IReadOnlyList<MesDeHistorialDeMora> Faltantes, ExplicacionDeCartera Explicacion)
{
    public bool Completo => Faltantes.Count == 0;
}

/// <summary>
/// El historial de mora de un crédito (feature 013, US9, T0695; T49, R57; FR-105, FR-139, FR-140):
/// <list type="bullet">
/// <item><b>Ventana</b>: los <c>mesesExigidos</c> meses que terminan en el de la fecha de referencia (la del primer cierre en pérdida
/// esperada), o desde el inicio de la cadena si el crédito es más joven.</item>
/// <item><b>Fuente que manda por mes</b>: el cierre mensual vigente, sobre la carga complementaria más reciente, sobre la migración.
/// Con novaciones y refinanciaciones se miran también los antecesores; si dos créditos de la cadena tienen el mes, cuenta la mayor
/// mora (lo conservador).</item>
/// <item><b>Meses posteriores al corte de la migración</b> (o todos, si el crédito no se migró): los cubren las transacciones aunque no
/// haya fila todavía.</item>
/// <item><b>Completo</b> si no falta ningún mes de la ventana. La regla de meses faltantes (<c>Riesgo.ReglaMesesFaltantes</c>) la aplica
/// <see cref="Completar"/> (K6a) para el modelo de pérdida esperada.</item>
/// </list>
/// Puro. (nuevo)
/// </summary>
public static class HistorialDeMora
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <param name="credito">El crédito y su cadena.</param>
    /// <param name="entradas">Las filas del crédito y de sus antecesores (las demás se ignoran).</param>
    /// <param name="corteDeMigracion">El corte de la carga con la que se activó; nulo si el crédito nació en Cartera.</param>
    /// <param name="fechaDeReferencia">La fecha desde la que se mira hacia atrás.</param>
    /// <param name="mesesExigidos">Los meses que pide <c>ReglaDePerdidaEsperada</c>.</param>
    public static HistorialDeMoraConstruido Construir(CreditoParaHistorial credito, IEnumerable<EntradaDeHistorialDeMora> entradas,
        DateOnly? corteDeMigracion, DateOnly fechaDeReferencia, int mesesExigidos)
    {
        ArgumentNullException.ThrowIfNull(credito);
        ArgumentNullException.ThrowIfNull(entradas);
        if (mesesExigidos <= 0) throw new ArgumentOutOfRangeException(nameof(mesesExigidos), mesesExigidos, "Los meses exigidos son mayores que cero.");

        var cadena = new HashSet<int>(credito.Antecesores) { credito.LoanId };
        var porMes = entradas.Where(e => cadena.Contains(e.LoanId) && (e.Origen != ArrearsHistoryOrigin.MonthlyClose || e.Vigente))
            .GroupBy(e => (e.Anio, e.Mes))
            .ToDictionary(g => g.Key, g => g.GroupBy(e => (e.LoanId, e.NumeroDelAntecesor)).Select(Manda).OrderByDescending(e => e.MaxDiasDeMora).ThenBy(e => e.LoanId).First());

        var referencia = Indice(fechaDeReferencia.Year, fechaDeReferencia.Month);
        var inicio = Math.Max(referencia - mesesExigidos + 1, Indice(credito.InicioDeLaCadena.Year, credito.InicioDeLaCadena.Month));
        var corte = corteDeMigracion is { } c ? Indice(c.Year, c.Month) : (int?)null;

        var meses = new List<MesDeHistorialDeMora>();
        for (var i = inicio; i <= referencia; i++)
        {
            var (anio, mes) = (i / 12, i % 12 + 1);
            var porTransacciones = corte is null || i > corte;
            meses.Add(porMes.TryGetValue((anio, mes), out var e)
                ? new(anio, mes, e.MaxDiasDeMora, e.Origen, e.LoanId, porTransacciones)
                : new(anio, mes, null, null, null, porTransacciones));
        }
        var faltantes = meses.Where(m => !m.Cubierto).ToList();

        var explicacion = new ExplicacionDeCartera()
            .Linea($"Ventana: {meses.Count.ToString(Inv)} meses hasta {fechaDeReferencia:yyyy-MM} (se piden {mesesExigidos.ToString(Inv)}; "
                + $"la cadena empieza en {credito.InicioDeLaCadena:yyyy-MM}"
                + (credito.Antecesores.Count > 0 ? $", con {credito.Antecesores.Count.ToString(Inv)} antecesor(es)" : string.Empty) + ").")
            .Linea(corteDeMigracion is { } cm
                ? $"Desde {cm.AddMonths(1):yyyy-MM} los meses los cubren las transacciones."
                : "El crédito nació en Cartera: las transacciones cubren todos sus meses.")
            .Linea(faltantes.Count == 0
                ? "Historial completo."
                : $"Faltan {faltantes.Count.ToString(Inv)} meses: {string.Join(", ", faltantes.Select(f => $"{f.Anio.ToString(Inv)}-{f.Mes.ToString("00", Inv)}"))}.");
        return new(meses, faltantes, explicacion);
    }

    /// <summary>
    /// K6a (T1015; FR-105, R57): el historial con <b>todos</b> sus meses para las variables del modelo: el mes con fila toma su mora; el que
    /// cubren las transacciones sin fila todavía (el mes que se está cerrando) la toma de <paramref name="moraDeTransacciones"/>; el que falta,
    /// la de la <paramref name="regla"/> —la mayor observada en la ventana (con la del corte si no hay ninguna), la del corte de la migración o
    /// los días fijos—. Lo que sale de la regla queda marcado (<see cref="MoraMensualCompleta.PorRegla"/>) y explicado.
    /// </summary>
    /// <exception cref="ArgumentException">Un mes cubierto por transacciones sin su mora, o la regla sin el dato que pide.</exception>
    public static HistorialDeMoraCompletado Completar(HistorialDeMoraConstruido historial, IReadOnlyDictionary<(int Anio, int Mes), int> moraDeTransacciones,
        ReglaDeMesesFaltantes regla, int? diasDelCorte, int? diasFijos)
    {
        ArgumentNullException.ThrowIfNull(historial);
        ArgumentNullException.ThrowIfNull(moraDeTransacciones);
        var observados = new List<(MesDeHistorialDeMora Mes, int? Dias)>();
        foreach (var m in historial.Meses)
        {
            if (m.MaxDiasDeMora is { } d) observados.Add((m, d));
            else if (m.CubiertoPorTransacciones)
                observados.Add((m, moraDeTransacciones.TryGetValue((m.Anio, m.Mes), out var t)
                    ? t
                    : throw new ArgumentException($"Falta la mora de {m.Anio.ToString(Inv)}-{m.Mes.ToString("00", Inv)}, que cubren las transacciones.",
                        nameof(moraDeTransacciones))));
            else observados.Add((m, null));
        }

        var explicacion = new ExplicacionDeCartera();
        if (historial.Completo)
        {
            explicacion.Linea("Historial completo: no se aplica la regla de meses faltantes.");
            return new(observados.Select(o => new MoraMensualCompleta(o.Mes.Anio, o.Mes.Mes, o.Dias!.Value, false)).ToList(), null, explicacion);
        }

        var vistos = observados.Where(o => o.Dias is not null).Select(o => o.Dias!.Value).ToList();
        var relleno = regla switch
        {
            ReglaDeMesesFaltantes.MayorObservada => vistos.Count > 0 ? vistos.Max() : diasDelCorte ?? 0,
            ReglaDeMesesFaltantes.DelCorte => diasDelCorte ?? throw new ArgumentException("La regla «DelCorte» necesita la mora del corte de la migración.", nameof(diasDelCorte)),
            ReglaDeMesesFaltantes.Fija => diasFijos ?? throw new ArgumentException(
                "La regla «Fija» necesita Riesgo.MesesFaltantesDiasFijos.", nameof(diasFijos)),
            _ => throw new ArgumentOutOfRangeException(nameof(regla), regla, "Regla de meses faltantes desconocida."),
        };
        if (relleno < 0) throw new ArgumentOutOfRangeException(nameof(diasFijos), relleno, "Los días de mora no son negativos.");
        explicacion.Linea($"Faltan {historial.Faltantes.Count.ToString(Inv)} meses: se completan con {relleno.ToString(Inv)} días ("
            + regla switch
            {
                ReglaDeMesesFaltantes.MayorObservada => vistos.Count > 0 ? "la mayor mora observada en la ventana" : "sin mora observada: la del corte o cero",
                ReglaDeMesesFaltantes.DelCorte => "la mora al corte de la migración",
                _ => "los días fijos de Riesgo.MesesFaltantesDiasFijos",
            } + $", regla {regla}).");
        return new(observados.Select(o => new MoraMensualCompleta(o.Mes.Anio, o.Mes.Mes, o.Dias ?? relleno, o.Dias is null)).ToList(), regla, explicacion);
    }

    /// <summary>La regla por el valor del parámetro <c>Riesgo.ReglaMesesFaltantes</c>.</summary>
    public static ReglaDeMesesFaltantes LeerRegla(string valor) =>
        Enum.TryParse<ReglaDeMesesFaltantes>(valor, ignoreCase: false, out var r) && Enum.IsDefined(r) && !int.TryParse(valor, out _)
            ? r
            : throw new ArgumentException($"«{valor}» no es una regla de meses faltantes.", nameof(valor));

    /// <summary>De las filas de un crédito en un mes, la que manda: cierre vigente, carga complementaria más reciente, migración.</summary>
    private static EntradaDeHistorialDeMora Manda(IEnumerable<EntradaDeHistorialDeMora> filas) =>
        filas.OrderByDescending(e => Prioridad(e.Origen)).ThenByDescending(e => e.Secuencia).First();

    private static int Prioridad(ArrearsHistoryOrigin origen) => origen switch
    {
        ArrearsHistoryOrigin.MonthlyClose => 3,
        ArrearsHistoryOrigin.ComplementaryLoad => 2,
        ArrearsHistoryOrigin.Migration => 1,
        _ => 0,
    };

    private static int Indice(int anio, int mes) => anio * 12 + mes - 1;
}
