using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Payroll;

/// <summary>
/// Lo exigible de una obligación del convenio en el período (feature 013, K2, T0825): lo arma <c>PayrollDeductionLines</c> en bloque con la
/// reconstrucción (<c>CargadorDeReconstruccion</c>) para cada crédito vivo con vínculo vigente al convenio. <see cref="Exigible"/> es lo del
/// período (la cuota que vence en él); <see cref="Vencido"/>, lo que quedó impago de antes, que sólo entra si el convenio lo admite.
/// </summary>
public sealed record ExigibleDeLibranza(int LoanId, int LoanPayrollLinkId, int PersonId, DateOnly LibranzaDate, decimal Exigible, decimal Vencido)
{
    public string? EmployeeIdAtPaymaster { get; init; }

    public long? RepaymentInstallmentId { get; init; }

    public int? InstallmentNumber { get; init; }

    public DateOnly? DueDate { get; init; }
}

/// <summary>
/// Una línea del archivo base (el último <c>Sent</c> del convenio) tal como se envió: lo que pidió y su novedad. Una línea que en el base ya era
/// <c>Cancellation</c> no cuenta como vigente para la pagaduría.
/// </summary>
public sealed record LineaDelArchivoBase(int LoanId, int LoanPayrollLinkId, int PersonId, DateOnly LibranzaDate, decimal Total, PayrollNoveltyKind Novedad)
{
    public string? EmployeeIdAtPaymaster { get; init; }
}

/// <summary>Una línea del archivo nuevo, ya numerada en el orden de la prelación (FR-111).</summary>
public sealed record LineaDeNovedad(
    int LineNumber,
    int LoanId,
    int LoanPayrollLinkId,
    int PersonId,
    DateOnly LibranzaDate,
    PayrollNoveltyKind Novedad,
    decimal Corriente,
    decimal Vencido,
    decimal Total,
    decimal? Anterior)
{
    public string? EmployeeIdAtPaymaster { get; init; }

    public long? RepaymentInstallmentId { get; init; }

    public int? InstallmentNumber { get; init; }

    public DateOnly? DueDate { get; init; }
}

/// <summary>Los conteos y montos por novedad del archivo (columnas de <c>LND_PayrollDeductionFiles</c>).</summary>
public sealed record TotalesDeNovedades(int Nuevas, int Cambios, int Cancelaciones, int SinCambio, decimal MontoNuevas, decimal MontoCambios,
    decimal MontoSinCambio)
{
    public int Lineas => Nuevas + Cambios + Cancelaciones + SinCambio;

    /// <summary>Lo que se pide descontar (las cancelaciones piden cero).</summary>
    public decimal Total => MontoNuevas + MontoCambios + MontoSinCambio;
}

/// <summary>El archivo de novedades: las líneas en el orden de la prelación y sus totales.</summary>
public sealed record ArchivoDeNovedades(IReadOnlyList<LineaDeNovedad> Lineas, TotalesDeNovedades Totales);

/// <summary>
/// Las novedades del archivo de descuentos de una pagaduría (feature 013, K2, T0825; T78, FR-111, FR-113; adaptadores-y-archivos.md §6.3):
/// compara lo exigible del período de cada obligación del convenio con el último archivo enviado y da, por obligación, una línea con su
/// novedad:
/// <list type="bullet">
/// <item>sin archivo base, todas son <c>New</c>;</item>
/// <item>en el base y ahora con el mismo total, <c>Unchanged</c>; con otro, <c>Change</c> con <c>Anterior</c> = lo del base;</item>
/// <item>en el base y ya no entre las obligaciones del convenio (cancelada o trasladada), <c>Cancellation</c> con total cero;</item>
/// <item>ahora y no en el base (o en el base como cancelación), <c>New</c>.</item>
/// </list>
/// El total es lo exigible más lo vencido si el convenio lo admite (<c>IncludesPastDue</c>); si no, lo vencido no viaja. Una línea por
/// obligación. Las líneas van en orden de la fecha de la libranza (prelación cronológica, FR-111) y, a igual fecha, por crédito. Pura: no
/// sabe de formatos ni de la base. (nuevo)
/// </summary>
public static class NovedadesDeLibranza
{
    /// <param name="obligaciones">Lo exigible de cada obligación vigente del convenio en el período.</param>
    /// <param name="archivoBase">Las líneas del último <c>Sent</c> del convenio; nulo si no hay.</param>
    /// <param name="incluyeVencido">El convenio lleva lo vencido (<c>IncludesPastDue</c>).</param>
    public static ArchivoDeNovedades Calcular(IEnumerable<ExigibleDeLibranza> obligaciones, IEnumerable<LineaDelArchivoBase>? archivoBase,
        bool incluyeVencido)
    {
        ArgumentNullException.ThrowIfNull(obligaciones);
        var actuales = new Dictionary<int, ExigibleDeLibranza>();
        foreach (var o in obligaciones)
        {
            if (o.Exigible < 0m || o.Vencido < 0m)
                throw new ArgumentException($"El crédito {o.LoanId} trae un exigible o un vencido negativo.", nameof(obligaciones));
            if (!actuales.TryAdd(o.LoanId, o))
                throw new ArgumentException($"El crédito {o.LoanId} viene dos veces: el archivo lleva una línea por obligación.", nameof(obligaciones));
        }

        var enElBase = new Dictionary<int, LineaDelArchivoBase>();
        foreach (var b in archivoBase ?? [])
            if (!enElBase.TryAdd(b.LoanId, b))
                throw new ArgumentException($"El archivo base trae dos veces el crédito {b.LoanId}.", nameof(archivoBase));

        var lineas = new List<LineaDeNovedad>(actuales.Count + enElBase.Count);
        foreach (var o in actuales.Values)
        {
            var vencido = incluyeVencido ? o.Vencido : 0m;
            var total = o.Exigible + vencido;
            PayrollNoveltyKind novedad;
            decimal? anterior = null;
            if (enElBase.TryGetValue(o.LoanId, out var b) && b.Novedad != PayrollNoveltyKind.Cancellation)
            {
                anterior = b.Total;
                novedad = b.Total == total ? PayrollNoveltyKind.Unchanged : PayrollNoveltyKind.Change;
            }
            else
            {
                novedad = PayrollNoveltyKind.New;
            }
            lineas.Add(new LineaDeNovedad(0, o.LoanId, o.LoanPayrollLinkId, o.PersonId, o.LibranzaDate, novedad, o.Exigible, vencido, total, anterior)
            {
                EmployeeIdAtPaymaster = o.EmployeeIdAtPaymaster,
                RepaymentInstallmentId = o.RepaymentInstallmentId,
                InstallmentNumber = o.InstallmentNumber,
                DueDate = o.DueDate,
            });
        }

        foreach (var b in enElBase.Values.Where(b => b.Novedad != PayrollNoveltyKind.Cancellation && !actuales.ContainsKey(b.LoanId)))
            lineas.Add(new LineaDeNovedad(0, b.LoanId, b.LoanPayrollLinkId, b.PersonId, b.LibranzaDate, PayrollNoveltyKind.Cancellation, 0m, 0m, 0m, b.Total)
            {
                EmployeeIdAtPaymaster = b.EmployeeIdAtPaymaster,
            });

        var ordenadas = lineas.OrderBy(l => l.LibranzaDate).ThenBy(l => l.LoanId)
            .Select((l, i) => l with { LineNumber = i + 1 })
            .ToList();
        return new ArchivoDeNovedades(ordenadas, Totalizar(ordenadas));
    }

    private static TotalesDeNovedades Totalizar(IReadOnlyList<LineaDeNovedad> lineas)
    {
        int Contar(PayrollNoveltyKind k) => lineas.Count(l => l.Novedad == k);
        decimal Sumar(PayrollNoveltyKind k) => lineas.Where(l => l.Novedad == k).Sum(l => l.Total);
        return new TotalesDeNovedades(Contar(PayrollNoveltyKind.New), Contar(PayrollNoveltyKind.Change), Contar(PayrollNoveltyKind.Cancellation),
            Contar(PayrollNoveltyKind.Unchanged), Sumar(PayrollNoveltyKind.New), Sumar(PayrollNoveltyKind.Change), Sumar(PayrollNoveltyKind.Unchanged));
    }
}
