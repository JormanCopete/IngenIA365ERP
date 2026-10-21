namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>Cómo se contabiliza una disminución del deterioro (<c>Contabilidad.DisminucionDeDeterioro</c>, pregunta C2).</summary>
public enum DisminucionDeDeterioro
{
    /// <summary>Primero contra el gasto llevado en el mismo ejercicio (práctica colombiana, el defecto); el resto a recuperación.</summary>
    MismoEjercicioPrimero = 1,

    /// <summary>Toda la disminución a recuperación.</summary>
    TodoARecuperacion = 2,
}

/// <summary>
/// El movimiento del deterioro de un cierre frente al saldo vigente (contracts/contabilidad.md §11.3; C2, C22): lo que sube (<c>Increase</c>) y
/// lo que baja, partido en lo que vuelve al gasto del mismo ejercicio (<c>DecreaseSameYear</c>) y lo que va a recuperación
/// (<c>DecreasePriorYears</c>). Exactamente uno de <see cref="Aumento"/> y la disminución es distinto de cero.
/// </summary>
public sealed record MovimientoDelDeterioro(decimal Aumento, decimal DisminucionDelEjercicio, decimal DisminucionDeEjerciciosAnteriores)
{
    public static MovimientoDelDeterioro Ninguno { get; } = new(0m, 0m, 0m);

    public decimal Disminucion => DisminucionDelEjercicio + DisminucionDeEjerciciosAnteriores;

    /// <summary>El neto con signo: positivo si sube.</summary>
    public decimal Neto => Aumento - Disminucion;

    /// <summary>Los movimientos con su código (C22), en el orden en que se emiten; los ceros no van.</summary>
    public IEnumerable<(string Codigo, decimal Importe)> Partidas()
    {
        if (Aumento != 0m) yield return (MovimientosDelDeterioro.Aumentar, Aumento);
        if (DisminucionDelEjercicio != 0m) yield return (MovimientosDelDeterioro.DisminuirDelEjercicio, DisminucionDelEjercicio);
        if (DisminucionDeEjerciciosAnteriores != 0m) yield return (MovimientosDelDeterioro.DisminuirDeEjerciciosAnteriores, DisminucionDeEjerciciosAnteriores);
    }
}

/// <summary>
/// La clasificación del deterioro en delta de un cierre (feature 013, US9, T0705; T48; contracts/contabilidad.md §11.3): la diferencia entre el
/// deterioro calculado y el <b>saldo vigente</b> (el del cierre anterior, ya reclasificado a la categoría nueva) es un aumento al gasto o una
/// disminución que, con <see cref="DisminucionDeDeterioro.MismoEjercicioPrimero"/>, vuelve primero contra el gasto que se llevó en el mismo
/// ejercicio —por crédito y concepto, o por dimensión del general— y el resto va a recuperación. El gasto del ejercicio se reconstruye de los
/// cierres vigentes del año: cada aumento lo suma y cada disminución del ejercicio lo consume. Pura, sin valores legales. (nuevo)
/// </summary>
public static class MovimientosDelDeterioro
{
    public const string Aumentar = "Increase";
    public const string DisminuirDelEjercicio = "DecreaseSameYear";
    public const string DisminuirDeEjerciciosAnteriores = "DecreasePriorYears";
    public const string Trasladar = "Transfer";

    /// <summary>La regla del parámetro por su texto; uno desconocido rige como el defecto.</summary>
    public static DisminucionDeDeterioro Regla(string? texto) =>
        string.Equals(texto, nameof(DisminucionDeDeterioro.TodoARecuperacion), StringComparison.Ordinal)
            ? DisminucionDeDeterioro.TodoARecuperacion
            : DisminucionDeDeterioro.MismoEjercicioPrimero;

    /// <summary>
    /// El gasto que queda llevado en el ejercicio después de recorrer <paramref name="historia"/>: el primer valor es el saldo al empezar el
    /// ejercicio (el cierre de diciembre anterior, el base o cero) y los siguientes, los de cada cierre vigente del año en orden.
    /// </summary>
    public static decimal GastoDelEjercicio(IReadOnlyList<decimal> historia, DisminucionDeDeterioro regla)
    {
        ArgumentNullException.ThrowIfNull(historia);
        var gasto = 0m;
        for (var i = 1; i < historia.Count; i++)
        {
            var m = Clasificar(historia[i - 1], historia[i], gasto, regla);
            gasto += m.Aumento - m.DisminucionDelEjercicio;
        }
        return gasto;
    }

    /// <summary>El movimiento de <paramref name="anterior"/> a <paramref name="nuevo"/> con el gasto del ejercicio llevado hasta ahí.</summary>
    public static MovimientoDelDeterioro Clasificar(decimal anterior, decimal nuevo, decimal gastoDelEjercicio, DisminucionDeDeterioro regla)
    {
        if (gastoDelEjercicio < 0m) throw new ArgumentOutOfRangeException(nameof(gastoDelEjercicio), gastoDelEjercicio, "El gasto del ejercicio no es negativo.");
        var diferencia = nuevo - anterior;
        if (diferencia > 0m) return new MovimientoDelDeterioro(diferencia, 0m, 0m);
        if (diferencia == 0m) return MovimientoDelDeterioro.Ninguno;
        var baja = -diferencia;
        var delEjercicio = regla == DisminucionDeDeterioro.MismoEjercicioPrimero ? Math.Min(baja, gastoDelEjercicio) : 0m;
        return new MovimientoDelDeterioro(0m, delEjercicio, baja - delEjercicio);
    }

    /// <summary>El movimiento del cierre con la historia del ejercicio (<see cref="GastoDelEjercicio"/>) y el valor nuevo.</summary>
    public static MovimientoDelDeterioro DelCierre(IReadOnlyList<decimal> historia, decimal nuevo, DisminucionDeDeterioro regla)
    {
        ArgumentNullException.ThrowIfNull(historia);
        if (historia.Count == 0) throw new ArgumentException("La historia lleva al menos el saldo vigente.", nameof(historia));
        return Clasificar(historia[^1], nuevo, GastoDelEjercicio(historia, regla), regla);
    }
}
