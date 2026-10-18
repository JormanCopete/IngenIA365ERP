using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// El disponible de una cuenta desglosado (FR-057): el saldo del titular, lo retenido por bloqueos, lo embargado, lo que está en canje, el mínimo de la
/// línea y lo que queda disponible. (nuevo)
/// </summary>
public sealed record DisponibleCalculado(decimal Saldo, decimal Retenido, decimal Embargado, decimal EnCanje, decimal Minimo, decimal Disponible)
{
    public static DisponibleCalculado Cero { get; } = new(0m, 0m, 0m, 0m, 0m, 0m);
}

/// <summary>
/// El disponible de una cuenta (feature 014, US4, T111; contrato-de-cuentas §3.5; FR-056, FR-057, SC-007): <b>saldo − bloqueos − embargos − canje −
/// mínimo</b>, nunca negativo. Es el mismo que muestra <c>ConsultarAsync</c>, el que valida un retiro y el que usan la simulación y los cargos: los tres
/// lo piden aquí. En aportes no hay disponible (los aportes no se retiran por el contrato). Puro. (nuevo)
/// </summary>
public static class DisponibleDeCuenta
{
    public static DisponibleCalculado Calcular(LineNature naturaleza, decimal saldo, decimal retenido, decimal embargado, decimal enCanje, decimal minimo)
    {
        if (naturaleza == LineNature.Contribution) return new DisponibleCalculado(saldo, retenido, embargado, enCanje, minimo, 0m);
        var disponible = Math.Max(0m, saldo - retenido - embargado - enCanje - minimo);
        return new DisponibleCalculado(saldo, retenido, embargado, enCanje, minimo, disponible);
    }

    /// <summary>
    /// ¿Deja el débito de <paramref name="monto"/> (con GMF y cargos ya sumados) la cuenta bajo el mínimo o negativa? (FR-056: ninguna operación de
    /// usuario lo hace).
    /// </summary>
    public static bool Alcanza(DisponibleCalculado disponible, decimal monto)
    {
        ArgumentNullException.ThrowIfNull(disponible);
        return monto <= disponible.Disponible;
    }
}
