namespace IngenIA365ERP.Domain.Lending.Delinquency;

/// <summary>
/// Lo que la altura de mora mira de una cuota: su número, su fecha exigible (art. 829), su saldo vencido —capital pendiente más su
/// corriente y su seguro; la mora no cuenta— y si está cubierta por anticipo o descontada por la pagaduría y pendiente de giro
/// (FR-114; la entrada existe desde K1, la usa K2).
/// </summary>
public sealed record CuotaParaMora(int Numero, DateOnly FechaExigible, decimal SaldoVencido)
{
    public bool CubiertaPorAnticipo { get; init; }

    /// <summary>La cuota entera, descontada y pendiente de giro, medida con el umbral único del llamado (forma de K1; ver <see cref="Giros"/>).</summary>
    public bool DescontadaPendienteDeGiro { get; init; }

    /// <summary>
    /// K2 (T0826): lo que la pagaduría descontó de esta cuota y no ha girado, cada uno con su vencimiento de la ventana y su umbral
    /// <b>sellados</b> en el resultado (<c>LND_PayrollDeductionResults</c>). Mientras la mora de la pagaduría no supere el umbral, lo descontado
    /// no cuenta en el saldo vencido de la cuota.
    /// </summary>
    public IReadOnlyList<GiroPendiente> Giros { get; init; } = [];
}

/// <summary>
/// Lo descontado de una cuota y pendiente de giro (K2, T0826; FR-114, data-model §24.8): el monto, el vencimiento de la ventana de giro
/// (<c>TransferDueDate</c>) y el umbral de mora de la pagaduría sellado (<c>PaymasterArrearsThresholdDays</c>). Umbral nulo = el parámetro no
/// tenía valor al recibir el resultado: la mora de la pagaduría nunca se imputa al deudor (C71).
/// </summary>
public sealed record GiroPendiente(decimal Monto, DateOnly VencimientoDelGiro, int? UmbralDias)
{
    /// <summary>Los días que la pagaduría lleva en mora el <paramref name="dia"/>: desde el vencimiento de la ventana, nunca negativos.</summary>
    public int MoraDeLaPagaduria(DateOnly dia) => Math.Max(dia.DayNumber - VencimientoDelGiro.DayNumber, 0);

    /// <summary>
    /// K2 (T0839): la fecha valor del pago que giró lo descontado (la del descuento), si ya se giró. Desde ese día la cuota la cubre el pago y lo
    /// descontado deja de cubrirla; antes sigue cubriéndola, para que girar no cambie la historia que el libro ya causó.
    /// </summary>
    public DateOnly? GiradoDesde { get; init; }

    /// <summary>
    /// ¿El <paramref name="dia"/> lo descontado todavía cubre la cuota? No, desde el día en que se giró; si no, mientras la mora de la pagaduría no
    /// supere el umbral (sin umbral, siempre).
    /// </summary>
    public bool DentroDelUmbral(DateOnly dia) =>
        (GiradoDesde is not { } girado || dia < girado) && (UmbralDias is not { } u || MoraDeLaPagaduria(dia) <= u);
}

/// <summary>Los días de mora, la cuota impaga más antigua que los cuenta y cuántas cuotas vencidas cuentan.</summary>
public sealed record ResultadoDeAlturaDeMora(int Dias, int? CuotaMasAntigua, int CuotasVencidas)
{
    public static ResultadoDeAlturaDeMora AlDia { get; } = new(0, null, 0);
}

/// <summary>
/// La altura de mora de un crédito un día (feature 013, US4, T0487; T30, R38, M9; FR-069, FR-114):
/// <list type="bullet">
/// <item>El capital de una cuota vence el día <b>siguiente</b> a su fecha exigible (<see cref="EstaVencida"/>): el día exigible se paga
/// sin mora.</item>
/// <item>Cuenta desde la cuota vencida más antigua cuyo saldo vencido <b>supere</b> la tolerancia (<c>Mora.ToleranciaDeSaldoVencido</c>):
/// <c>día − fecha exigible</c>.</item>
/// <item>No cuentan las cubiertas por anticipo ni, dentro del umbral de la pagaduría, las descontadas pendientes de giro (K2).</item>
/// <item>K2 (T0826, T30): de una cuota con <see cref="CuotaParaMora.Giros"/>, lo descontado pendiente de giro se resta de su saldo vencido
/// mientras la mora de la pagaduría (días desde el vencimiento de la ventana) no supere el umbral sellado; si lo que queda no supera la
/// tolerancia, la cuota no cuenta. Superado el umbral, cuenta entera y la altura va <b>desde su fecha exigible</b>, como cualquier otra.</item>
/// </list>
/// Pura. (nuevo)
/// </summary>
public static class AlturaDeMora
{
    /// <summary>¿El capital de una cuota con esa fecha exigible está vencido el día <paramref name="dia"/>?</summary>
    public static bool EstaVencida(DateOnly fechaExigible, DateOnly dia) => dia > fechaExigible;

    /// <param name="dia">El día que se mide.</param>
    /// <param name="cuotas">Las cuotas del plan con su saldo vencido.</param>
    /// <param name="tolerancia">El saldo vencido que no alcanza para contar días.</param>
    /// <param name="umbralDePagaduria">Los días que una cuota descontada y pendiente de giro no cuenta (K2); nulo, no aplica.</param>
    public static ResultadoDeAlturaDeMora Calcular(DateOnly dia, IEnumerable<CuotaParaMora> cuotas, decimal tolerancia, int? umbralDePagaduria = null)
    {
        ArgumentNullException.ThrowIfNull(cuotas);
        if (tolerancia < 0m) throw new ArgumentOutOfRangeException(nameof(tolerancia), tolerancia, "La tolerancia no puede ser negativa.");
        CuotaParaMora? masAntigua = null;
        var cuentan = 0;
        foreach (var c in cuotas)
        {
            if (!EstaVencida(c.FechaExigible, dia) || c.SaldoVencido <= tolerancia || c.CubiertaPorAnticipo) continue;
            if (c.DescontadaPendienteDeGiro && umbralDePagaduria is { } u && dia.DayNumber - c.FechaExigible.DayNumber <= u) continue;
            if (c.Giros.Count > 0 && c.SaldoVencido - CubiertoPorGiro(dia, c.Giros) <= tolerancia) continue;
            cuentan++;
            if (masAntigua is null || c.Numero < masAntigua.Numero) masAntigua = c;
        }
        return masAntigua is null
            ? ResultadoDeAlturaDeMora.AlDia
            : new ResultadoDeAlturaDeMora(dia.DayNumber - masAntigua.FechaExigible.DayNumber, masAntigua.Numero, cuentan);
    }

    /// <summary>
    /// Lo descontado y pendiente de giro de una cuota que el <paramref name="dia"/> todavía no cuenta para el deudor: la suma de los giros cuya
    /// pagaduría no superó su umbral (K2, T0826). Lo usa también la reconstrucción para no causar mora sobre lo que la pagaduría ya descontó.
    /// </summary>
    public static decimal CubiertoPorGiro(DateOnly dia, IEnumerable<GiroPendiente> giros)
    {
        ArgumentNullException.ThrowIfNull(giros);
        var cubierto = 0m;
        foreach (var g in giros)
            if (g.DentroDelUmbral(dia)) cubierto += g.Monto;
        return cubierto;
    }
}
