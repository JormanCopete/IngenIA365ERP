namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>Los depósitos de un ahorrador a la fecha (capital e intereses causados de todas sus cuentas de captación; nunca aportes). (nuevo)</summary>
public sealed record DepositosDeUnAhorrador(int PersonaId, int Cuentas, decimal Depositos);

/// <summary>Lo asegurado de un ahorrador: hasta el tope por persona y lo que no cubre. (nuevo)</summary>
public sealed record AseguradoDeUnAhorrador(int PersonaId, int Cuentas, decimal Depositos, decimal Asegurado, decimal NoAsegurado);

/// <summary>
/// Motor puro de los depósitos asegurados (feature 014, US15, T214; FR-110; research §24): el seguro de depósitos de Fogacoop cubre a cada ahorrador hasta el
/// tope por persona y por entidad, sumando todos sus productos de ahorro (no los aportes, que son patrimonio). El tope es el valor nacional
/// <c>TOPE_SEGURO_DEPOSITOS</c> que rige a la fecha; sin tope, nada se puede decir asegurado. (nuevo)
/// </summary>
public static class DepositosAsegurados
{
    public static IReadOnlyList<AseguradoDeUnAhorrador> Calcular(decimal tope, IReadOnlyList<DepositosDeUnAhorrador> ahorradores)
    {
        ArgumentNullException.ThrowIfNull(ahorradores);
        if (tope < 0m) throw new ArgumentOutOfRangeException(nameof(tope), "El tope no puede ser negativo.");
        return ahorradores.Where(a => a.Depositos > 0m)
            .Select(a =>
            {
                var asegurado = Math.Min(a.Depositos, tope);
                return new AseguradoDeUnAhorrador(a.PersonaId, a.Cuentas, a.Depositos, asegurado, a.Depositos - asegurado);
            })
            .ToList();
    }
}
