using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Replay;

/// <summary>
/// Una fila del libro de Cartera por (día valor, clase, concepto, cuota) con su importe con signo (data-model §5.1). La reconstrucción
/// produce las teóricas; el verificador compara contra éstas las reales de <c>LND_LoanTransactions</c> mapeadas a la misma forma.
/// </summary>
public sealed record MovimientoDeLibro(DateOnly Dia, LoanTransactionKind Clase, LoanConcept Concepto, int? Cuota, decimal Monto)
{
    public string? OperacionId { get; init; }

    /// <summary>
    /// K6b (T1054, T1059): en un crédito en UVR, el valor de la fila en UVR (cero en la corrección monetaria, que es sólo el cambio de la UVR) y la
    /// UVR del día con que se valoró en pesos; nulos en un crédito en pesos. El libro los copia en <c>UvrAmount</c> y <c>UvrValue</c>.
    /// </summary>
    public decimal? MontoEnUvr { get; init; }

    public decimal? ValorUvr { get; init; }
}

/// <summary>Una diferencia del libro contra la reconstrucción por (día valor, concepto, cuota): lo que <c>Conciliar</c> registra.</summary>
public sealed record DiferenciaDeLibro(DateOnly Dia, LoanConcept Concepto, int? Cuota, decimal Libro, decimal Reconstruccion)
{
    /// <summary>Reconstrucción − libro: lo que falta registrar (con signo).</summary>
    public decimal Diferencia => Reconstruccion - Libro;

    /// <summary>
    /// La diferencia es de los hechos de las operaciones (pagos, anticipos, saldos a favor) y no de la causación; sólo la marca
    /// <see cref="LibroTeorico.DiferenciasPorFamilia"/> (US5).
    /// </summary>
    public bool DeHechos { get; init; }
}

/// <summary>
/// El libro <b>teórico</b> de un crédito (feature 013, US4, T0488; T24, T33, R32): las filas que el libro debería tener según la
/// reconstrucción a (D, R) —los hechos de las operaciones y la causación día a día, <c>U(A(d)) − U(A(d−1))</c>—. La invariante de
/// data-model §5.3 es Σ libro = Σ libro teórico por (día, concepto, cuota) para todo día causado; <see cref="Diferencias"/> la mide y
/// sus filas son las que <c>RegistroDeCartera.Conciliar</c> registra como ajuste. (nuevo)
/// </summary>
public sealed class LibroTeorico
{
    private readonly List<MovimientoDeLibro> _movimientos;

    public LibroTeorico(IEnumerable<MovimientoDeLibro> movimientos)
    {
        ArgumentNullException.ThrowIfNull(movimientos);
        _movimientos = movimientos
            .OrderBy(m => m.Dia)
            .ThenBy(m => m.Clase == LoanTransactionKind.Accrual ? 1 : 0)
            .ThenBy(m => (int)m.Concepto)
            .ThenBy(m => m.Cuota ?? 0)
            .ToList();
    }

    public static LibroTeorico Vacio { get; } = new([]);

    public IReadOnlyList<MovimientoDeLibro> Movimientos => _movimientos;

    /// <summary>Las filas de un día valor.</summary>
    public IEnumerable<MovimientoDeLibro> DelDia(DateOnly dia) => _movimientos.Where(m => m.Dia == dia);

    /// <summary>La suma de un concepto (y una cuota, o todas si es nula) hasta un día incluido.</summary>
    public decimal SumaHasta(LoanConcept concepto, DateOnly hasta, int? cuota = null) =>
        _movimientos.Where(m => m.Concepto == concepto && m.Dia <= hasta && (cuota is null || m.Cuota == cuota)).Sum(m => m.Monto);

    /// <summary>
    /// Las diferencias de <paramref name="libro"/> contra éste por (día valor, concepto, cuota), sin importar la clase de la fila: cada
    /// una con lo que tiene el libro, lo que da la reconstrucción y la diferencia. Sólo hasta <paramref name="hasta"/> si se da.
    /// </summary>
    public IReadOnlyList<DiferenciaDeLibro> Diferencias(IEnumerable<MovimientoDeLibro> libro, DateOnly? hasta = null)
    {
        ArgumentNullException.ThrowIfNull(libro);
        var porClave = new Dictionary<(DateOnly, LoanConcept, int?), (decimal Libro, decimal Teorico)>();
        foreach (var m in libro.Where(m => hasta is null || m.Dia <= hasta))
        {
            var k = (m.Dia, m.Concepto, m.Cuota);
            porClave[k] = porClave.TryGetValue(k, out var v) ? (v.Libro + m.Monto, v.Teorico) : (m.Monto, 0m);
        }
        foreach (var m in _movimientos.Where(m => hasta is null || m.Dia <= hasta))
        {
            var k = (m.Dia, m.Concepto, m.Cuota);
            porClave[k] = porClave.TryGetValue(k, out var v) ? (v.Libro, v.Teorico + m.Monto) : (0m, m.Monto);
        }
        return porClave
            .Where(kv => kv.Value.Libro != kv.Value.Teorico)
            .Select(kv => new DiferenciaDeLibro(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Value.Libro, kv.Value.Teorico))
            .OrderBy(d => d.Dia).ThenBy(d => (int)d.Concepto).ThenBy(d => d.Cuota ?? 0)
            .ToList();
    }

    /// <summary>Las clases de fila que son causación (lo demás son hechos de las operaciones).</summary>
    public static bool EsDeCausacion(LoanTransactionKind clase) =>
        clase is LoanTransactionKind.Accrual or LoanTransactionKind.AccrualAdjustment or LoanTransactionKind.SuspenseReclassification;

    /// <summary>
    /// Como <see cref="Diferencias"/>, pero por familia (US5, T33): la causación por un lado y los hechos de las operaciones (pagos,
    /// anticipos, saldos a favor, desembolsos) por otro, así un reverso con pagos posteriores registra cada diferencia con su clase
    /// (<see cref="DiferenciaDeLibro.DeHechos"/>): <c>PaymentReallocation</c> la de los hechos, <c>AccrualAdjustment</c> o
    /// <c>SuspenseReclassification</c> la de la causación.
    /// </summary>
    public IReadOnlyList<DiferenciaDeLibro> DiferenciasPorFamilia(IEnumerable<MovimientoDeLibro> libro, DateOnly? hasta = null)
    {
        ArgumentNullException.ThrowIfNull(libro);
        var lista = libro.ToList();
        var causacion = new LibroTeorico(_movimientos.Where(m => EsDeCausacion(m.Clase)))
            .Diferencias(lista.Where(m => EsDeCausacion(m.Clase)), hasta);
        var hechos = new LibroTeorico(_movimientos.Where(m => !EsDeCausacion(m.Clase)))
            .Diferencias(lista.Where(m => !EsDeCausacion(m.Clase)), hasta).Select(d => d with { DeHechos = true });
        return causacion.Concat(hechos).OrderBy(d => d.Dia).ThenBy(d => d.DeHechos).ThenBy(d => (int)d.Concepto).ThenBy(d => d.Cuota ?? 0).ToList();
    }
}
