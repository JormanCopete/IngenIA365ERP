using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Una fila del libro de cuentas tal como la ve la reconstrucción (feature 014, T033; data-model §4.2): su Id, su clase, su concepto, su importe
/// <b>con signo</b> (§0.2: en los conceptos del titular, <c>+</c> = la Cartera le debe), su fecha valor, cuándo se registró y, si es el espejo de un
/// reverso, la fila que espeja. Puro: lo arma la aplicación desde <c>LND_MemberAccountTransactions</c>.
/// </summary>
public sealed record MovimientoDeCuenta(
    long Id,
    MemberAccountTransactionKind Clase,
    MemberAccountConcept Concepto,
    decimal Monto,
    DateOnly FechaValor,
    DateTime RegistradoEn,
    long? Reversa = null);

/// <summary>
/// Lo que una cuenta debe por concepto a la fecha <see cref="Fecha"/> con la información registrada hasta <see cref="InformacionHasta"/> (R04): los
/// saldos de los diez conceptos (los que no tienen filas, en cero), las sumas que usa la aplicación y la huella de las filas que entraron.
/// </summary>
public sealed record SaldosDeCuenta(DateOnly Fecha, DateTime InformacionHasta, IReadOnlyDictionary<MemberAccountConcept, decimal> Saldos, int Filas,
    string Huella)
{
    /// <summary>El saldo de un concepto (Σ de sus filas visibles).</summary>
    public decimal Saldo(MemberAccountConcept concepto) => Saldos.GetValueOrDefault(concepto);

    /// <summary>Saldo del titular de una cuenta de captación (§0.2): capital + interés por pagar + bonificación − cargo por cobrar.</summary>
    public decimal SaldoDeCaptacion => Saldo(MemberAccountConcept.Principal) + Saldo(MemberAccountConcept.InterestPayable)
        + Saldo(MemberAccountConcept.Bonus) - Saldo(MemberAccountConcept.ChargeReceivable);

    /// <summary>Saldo de una cuenta de aportes (§0.2): Σ de las cuatro clases de aporte.</summary>
    public decimal SaldoDeAportes => Saldo(MemberAccountConcept.ContributionMinimum) + Saldo(MemberAccountConcept.ContributionOrdinary)
        + Saldo(MemberAccountConcept.ContributionExtraordinary) + Saldo(MemberAccountConcept.ContributionRevaluation);

    /// <summary>Lo retenido al titular para pagar al Estado: memoria, no saldo del titular (§0.2).</summary>
    public decimal Retenido => Saldo(MemberAccountConcept.Withholding) + Saldo(MemberAccountConcept.Gmf);
}

/// <summary>Los saldos por concepto al cierre de un día (la serie que recibe el interés por saldo diario, R05).</summary>
public sealed record SaldoDelDia(DateOnly Dia, IReadOnlyDictionary<MemberAccountConcept, decimal> Saldos)
{
    public decimal Saldo(MemberAccountConcept concepto) => Saldos.GetValueOrDefault(concepto);
}

/// <summary>Una diferencia del libro contra la reconstrucción en un concepto (<see cref="ReconstruccionDeCuenta.Verificar"/>).</summary>
public sealed record DiferenciaDeCuenta(MemberAccountConcept Concepto, decimal EnElLibro, decimal Reconstruido)
{
    public decimal Diferencia => EnElLibro - Reconstruido;
}

/// <summary>
/// La reconstrucción de una cuenta de captación o de aportes (feature 014, T033; research R04; data-model §4.2; FR-001, FR-011, FR-062): pura y
/// determinista, sin IO ni reloj. Recibe las filas del libro y devuelve los saldos por concepto <b>a la fecha D con la información registrada hasta
/// R</b>: entra una fila si su fecha valor es ≤ D y se registró hasta R. Así:
/// <list type="bullet">
/// <item>una consignación con fecha valor anterior registrada después de R no cambia lo que se sabía en R, y sí lo que se sabe después (FR-062);</item>
/// <item>el espejo de un reverso lleva la fecha valor de su original y signo contrario: anula al original desde que se registra, y antes de eso el
/// original cuenta (lo que se informó a esa fecha sigue siendo reproducible, SC-001);</item>
/// <item>un espejo de otro espejo, o un espejo que no es el opuesto exacto de su original (mismo concepto, importe contrario y misma fecha valor), es un
/// libro inconsistente y se rechaza: nadie más que <c>RegistroDeCuentas</c> escribe el libro y él no los produce.</item>
/// </list>
/// Molde: <c>ReconstruccionDeCredito</c> de la 013. En K7 la cuenta no causa intereses (los aportes no tienen tasa, R02) y la causación de K8 llega
/// como filas <c>InterestAccrued</c> del libro; el motor del interés diario (<c>InteresDeCaptacion</c>) recibe la serie de <see cref="SaldosDiarios"/>.
/// </summary>
public static class ReconstruccionDeCuenta
{
    /// <summary>El libro de la cuenta no es consistente (un espejo sin su original, de otro espejo o que no lo anula exactamente).</summary>
    public const string CodigoLibroInconsistente = "Lending.Savings.LedgerInconsistent";

    /// <summary>Todos los conceptos, en el orden del enum: la reconstrucción los devuelve siempre (en cero si no hay filas).</summary>
    public static IReadOnlyList<MemberAccountConcept> Conceptos { get; } = Enum.GetValues<MemberAccountConcept>();

    /// <summary>Los saldos por concepto a <paramref name="fecha"/> con la información hasta <paramref name="informacionHasta"/>.</summary>
    public static SaldosDeCuenta Reconstruir(IEnumerable<MovimientoDeCuenta> libro, DateOnly fecha, DateTime informacionHasta)
    {
        ArgumentNullException.ThrowIfNull(libro);
        var filas = libro.ToList();
        ValidarEspejos(filas);
        var vistas = Visibles(filas, fecha, informacionHasta);
        return new SaldosDeCuenta(fecha, informacionHasta, Sumar(vistas), vistas.Count, Huella(vistas, fecha, informacionHasta));
    }

    /// <summary>
    /// Los saldos por concepto al cierre de cada día de (<paramref name="desde"/>, <paramref name="hasta"/>] —el primero excluido, el último
    /// incluido, como la causación de la 013 (T6)— con la información hasta <paramref name="informacionHasta"/>. Una sola pasada por el libro.
    /// </summary>
    public static IReadOnlyList<SaldoDelDia> SaldosDiarios(IEnumerable<MovimientoDeCuenta> libro, DateOnly desde, DateOnly hasta, DateTime informacionHasta)
    {
        ArgumentNullException.ThrowIfNull(libro);
        if (hasta < desde) throw new ArgumentOutOfRangeException(nameof(hasta), hasta, "El fin de la serie no va antes de su inicio.");
        var filas = libro.ToList();
        ValidarEspejos(filas);
        var conocidas = filas.Where(f => f.RegistradoEn <= informacionHasta).ToList();
        var acumulado = Conceptos.ToDictionary(c => c, _ => 0m);
        foreach (var f in conocidas.Where(f => f.FechaValor <= desde)) acumulado[f.Concepto] += f.Monto;

        var porDia = conocidas.Where(f => f.FechaValor > desde && f.FechaValor <= hasta).ToLookup(f => f.FechaValor);
        var serie = new List<SaldoDelDia>(hasta.DayNumber - desde.DayNumber);
        for (var dia = desde.AddDays(1); dia <= hasta; dia = dia.AddDays(1))
        {
            foreach (var f in porDia[dia]) acumulado[f.Concepto] += f.Monto;
            serie.Add(new SaldoDelDia(dia, new Dictionary<MemberAccountConcept, decimal>(acumulado)));
        }
        return serie;
    }

    /// <summary>
    /// Verifica el libro (data-model §4.2): Σ de las filas con fecha valor ≤ <paramref name="fecha"/> por concepto contra lo que dice
    /// <paramref name="esperado"/> (la reconstrucción guardada o la del motor con más información). Devuelve sólo los conceptos que difieren.
    /// </summary>
    public static IReadOnlyList<DiferenciaDeCuenta> Verificar(IEnumerable<MovimientoDeCuenta> libro, DateOnly fecha, IReadOnlyDictionary<MemberAccountConcept, decimal> esperado)
    {
        ArgumentNullException.ThrowIfNull(esperado);
        var real = Reconstruir(libro, fecha, DateTime.MaxValue);
        return Conceptos
            .Select(c => new DiferenciaDeCuenta(c, real.Saldo(c), esperado.GetValueOrDefault(c)))
            .Where(d => d.Diferencia != 0m)
            .ToList();
    }

    /// <summary>
    /// Lo que cambió en cada día de (<paramref name="desde"/>, <paramref name="hasta"/>] entre lo que se sabía en <paramref name="antes"/> y lo que
    /// se sabe en <paramref name="despues"/>: el efecto de una operación con fecha valor anterior (FR-062) sobre la serie de saldos, día por día y por
    /// concepto. La usa la puesta al día para el ajuste de la causación ya escrita.
    /// </summary>
    public static IReadOnlyList<(DateOnly Dia, MemberAccountConcept Concepto, decimal Cambio)> CambiosPorFechaValor(IEnumerable<MovimientoDeCuenta> libro,
        DateOnly desde, DateOnly hasta, DateTime antes, DateTime despues)
    {
        ArgumentNullException.ThrowIfNull(libro);
        var filas = libro.ToList();
        var a = SaldosDiarios(filas, desde, hasta, antes);
        var b = SaldosDiarios(filas, desde, hasta, despues);
        var cambios = new List<(DateOnly, MemberAccountConcept, decimal)>();
        for (var i = 0; i < a.Count; i++)
            foreach (var c in Conceptos)
                if (b[i].Saldo(c) - a[i].Saldo(c) is var cambio && cambio != 0m)
                    cambios.Add((a[i].Dia, c, cambio));
        return cambios;
    }

    /// <summary>Las filas que entran a (D, R): fecha valor ≤ D y registradas hasta R. En el orden de su Id.</summary>
    public static IReadOnlyList<MovimientoDeCuenta> Visibles(IEnumerable<MovimientoDeCuenta> libro, DateOnly fecha, DateTime informacionHasta)
    {
        ArgumentNullException.ThrowIfNull(libro);
        return libro.Where(f => f.FechaValor <= fecha && f.RegistradoEn <= informacionHasta).OrderBy(f => f.Id).ToList();
    }

    private static Dictionary<MemberAccountConcept, decimal> Sumar(IEnumerable<MovimientoDeCuenta> filas)
    {
        var saldos = Conceptos.ToDictionary(c => c, _ => 0m);
        foreach (var f in filas) saldos[f.Concepto] += f.Monto;
        return saldos;
    }

    /// <summary>Cada espejo nombra una fila que existe, que no es otro espejo, y la anula exactamente (mismo concepto y fecha valor, importe opuesto).</summary>
    private static void ValidarEspejos(IReadOnlyList<MovimientoDeCuenta> filas)
    {
        var porId = new Dictionary<long, MovimientoDeCuenta>();
        foreach (var f in filas)
            if (!porId.TryAdd(f.Id, f))
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoLibroInconsistente, $"La fila {f.Id} del libro está repetida.", "id", f.Id));
        foreach (var f in filas)
        {
            if (f.Monto == 0m)
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoLibroInconsistente, $"La fila {f.Id} del libro tiene importe cero.", "id", f.Id));
            if (f.Reversa is not { } original) continue;
            if (!porId.TryGetValue(original, out var o))
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoLibroInconsistente,
                    $"La fila {f.Id} espeja la {original}, que no está en el libro de la cuenta.", "id", f.Id));
            if (o.Reversa is not null)
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con("Lending.Reversal.OfReversal",
                    $"La fila {f.Id} espeja otro espejo ({original}): un reverso no se reversa.", "id", f.Id));
            if (o.Concepto != f.Concepto || o.Monto != -f.Monto || o.FechaValor != f.FechaValor)
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoLibroInconsistente,
                    $"La fila {f.Id} no anula exactamente la {original}: un espejo lleva el mismo concepto y fecha valor y el importe opuesto.", "id", f.Id));
        }
    }

    private static string Huella(IReadOnlyList<MovimientoDeCuenta> vistas, DateOnly fecha, DateTime informacionHasta) => new HuellaDeCartera()
        .Fecha("fecha", fecha)
        .Instante("informacionHasta", informacionHasta)
        .Texto("reglas", VersionesDeReglas.Vigente)
        .Lista("filas", vistas, (h, f) => h
            .Entero("id", f.Id)
            .Enumeracion<MemberAccountTransactionKind>("clase", f.Clase)
            .Enumeracion<MemberAccountConcept>("concepto", f.Concepto)
            .Decimal("monto", f.Monto)
            .Fecha("fechaValor", f.FechaValor)
            .Entero("reversa", f.Reversa))
        .Calcular();
}
