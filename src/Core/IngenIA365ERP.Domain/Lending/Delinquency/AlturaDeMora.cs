namespace IngenIA365ERP.Domain.Lending.Delinquency;

/// <summary>
/// Lo que la altura de mora mira de una cuota: su número, su fecha exigible (art. 829), su saldo vencido —capital pendiente más su
/// corriente y su seguro; la mora no cuenta— y si está cubierta por anticipo o descontada por la pagaduría y pendiente de giro
/// (FR-114; la entrada existe desde K1, la usa K2).
/// </summary>
public sealed record CuotaParaMora(int Numero, DateOnly FechaExigible, decimal SaldoVencido)
{
    public bool CubiertaPorAnticipo { get; init; }

    public bool DescontadaPendienteDeGiro { get; init; }
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
            cuentan++;
            if (masAntigua is null || c.Numero < masAntigua.Numero) masAntigua = c;
        }
        return masAntigua is null
            ? ResultadoDeAlturaDeMora.AlDia
            : new ResultadoDeAlturaDeMora(dia.DayNumber - masAntigua.FechaExigible.DayNumber, masAntigua.Numero, cuentan);
    }
}
