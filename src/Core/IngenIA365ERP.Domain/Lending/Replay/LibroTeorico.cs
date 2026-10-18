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

    /// <summary>
    /// K6b (fase 24): la diferencia es de la <b>corrección monetaria</b> de un crédito en UVR (filas <c>MonetaryCorrection</c>) y no de la causación;
    /// sólo la marca <see cref="LibroTeorico.DiferenciasSeparandoLaCorreccionMonetaria"/>.
    /// </summary>
    public bool DeCorreccionMonetaria { get; init; }

    /// <summary>
    /// K6b (decisión del dueño, 2026-10-17): la diferencia es del <b>reparto de un pago</b> (filas de <see cref="LibroTeorico.EsDePago"/>) de un
    /// crédito en UVR cuyo día valor tuvo la UVR corregida: el pago es el mismo en pesos, pero a la UVR nueva cubre en pesos otra mezcla de capital,
    /// intereses, cargos, anticipo y saldo a favor. Desde la decisión del 2026-10-18, también el de un día con la <c>USURA</c> corregida (cubre otro
    /// interés y otro capital). Sólo la marca <see cref="LibroTeorico.DiferenciasSeparandoLaCorreccionMonetaria"/> con
    /// <c>separarPagos</c>; viaja en el hecho <c>PagoRedistribuidoPorValorNacional</c>.
    /// </summary>
    public bool DePago { get; init; }
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

    /// <summary>
    /// K6b (decisión del dueño, 2026-10-17): las clases de fila del <b>reparto de un pago</b> —lo aplicado, lo recuperado de un castigado, el anticipo
    /// recibido o usado, el saldo a favor que deja y las reaplicaciones—: lo que cambia en pesos, sin cambiar lo recibido, cuando se corrige la UVR
    /// del día valor del pago. Las demás operaciones (desembolso, cargo, condonación, novedades, resoluciones) no son un reparto.
    /// </summary>
    public static bool EsDePago(LoanTransactionKind clase) =>
        clase is LoanTransactionKind.PaymentAllocation or LoanTransactionKind.Recovery or LoanTransactionKind.AdvanceReceived
            or LoanTransactionKind.AdvanceApplied or LoanTransactionKind.CreditBalanceRecognized or LoanTransactionKind.PaymentReallocation;

    /// <summary>Los conceptos que son un pasivo con el deudor (el anticipo y el saldo a favor): en el libro, positivo es que el pasivo crece.</summary>
    public static bool EsPasivoConElDeudor(LoanConcept concepto) => concepto is LoanConcept.Advance or LoanConcept.CreditBalance;

    /// <summary>
    /// Lo que no cuadra de un cambio de reparto (K6b, decisión del dueño del 2026-10-17): Σ de las diferencias de lo que se debe − Σ de las de los
    /// pasivos con el deudor. Lo recibido no cambia, así que un reparto que sólo se redistribuye da cero: cubrir más de un concepto (libro más
    /// negativo) es cubrir menos de otro o dejar menos anticipo o saldo a favor.
    /// </summary>
    public static decimal DescuadreDelReparto(IEnumerable<DiferenciaDeLibro> diferencias)
    {
        ArgumentNullException.ThrowIfNull(diferencias);
        return diferencias.Sum(d => EsPasivoConElDeudor(d.Concepto) ? -d.Diferencia : d.Diferencia);
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

    /// <summary>
    /// Como <see cref="Diferencias"/>, pero con la corrección monetaria de un crédito en UVR aparte (K6b, fase 24): las filas
    /// <c>MonetaryCorrection</c> se comparan sólo entre sí y sus diferencias van marcadas con <see cref="DiferenciaDeLibro.DeCorreccionMonetaria"/>,
    /// porque viajan en otro hecho (<c>CorreccionMonetariaUvrCausada</c>) y se registran con su clase. En un crédito en pesos no hay filas
    /// <c>MonetaryCorrection</c> y el resultado es el de <see cref="Diferencias"/>.
    /// <para>
    /// Con <paramref name="separarPagos"/> (K6b, decisión del dueño del 2026-10-17; la corrección de una <c>UVR</c>), también el <b>reparto de los
    /// pagos</b> (<see cref="EsDePago"/>) se compara aparte y sus diferencias van marcadas con <see cref="DiferenciaDeLibro.DePago"/>: si no, el interés
    /// que un pago cubre de más o de menos se anula con el de la causación del mismo día y el capital quedaría como un ajuste de causación, que no
    /// tiene con qué contabilizarse.
    /// </para>
    /// </summary>
    public IReadOnlyList<DiferenciaDeLibro> DiferenciasSeparandoLaCorreccionMonetaria(IEnumerable<MovimientoDeLibro> libro, DateOnly? hasta = null,
        bool separarPagos = false)
    {
        ArgumentNullException.ThrowIfNull(libro);
        var lista = libro.ToList();
        static bool Cmu(MovimientoDeLibro m) => m.Clase == LoanTransactionKind.MonetaryCorrection;
        bool Pago(MovimientoDeLibro m) => separarPagos && EsDePago(m.Clase);
        var resto = new LibroTeorico(_movimientos.Where(m => !Cmu(m) && !Pago(m))).Diferencias(lista.Where(m => !Cmu(m) && !Pago(m)), hasta);
        var correccion = new LibroTeorico(_movimientos.Where(Cmu)).Diferencias(lista.Where(Cmu), hasta)
            .Select(d => d with { DeCorreccionMonetaria = true });
        IEnumerable<DiferenciaDeLibro> pagos = separarPagos
            ? new LibroTeorico(_movimientos.Where(Pago)).Diferencias(lista.Where(Pago), hasta).Select(d => d with { DePago = true })
            : [];
        return resto.Concat(pagos).Concat(correccion)
            .OrderBy(d => d.Dia).ThenBy(d => d.DeCorreccionMonetaria).ThenBy(d => d.DePago).ThenBy(d => (int)d.Concepto).ThenBy(d => d.Cuota ?? 0)
            .ToList();
    }
}
