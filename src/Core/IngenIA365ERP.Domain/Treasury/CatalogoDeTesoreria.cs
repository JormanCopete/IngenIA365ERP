namespace IngenIA365ERP.Domain.Treasury;

/// <summary>
/// Las entregas de la Tesorería nueva (feature 015, T0042), en el orden en que salen. Se convierte sola a <c>EntregaDeModulo("TES", n)</c>. T0
/// sale junto con R0 de Recaudo; lo heredado de Tesorería (<c>TRS_</c>) no tiene entregas: se retira después de T2.
/// </summary>
public enum EntregaDeTesoreria
{
    T0 = 1,
    T1 = 2,
    T2 = 3,
}

/// <summary>Lo que la versión desplegada sabe de la Tesorería nueva (feature 015, T0042). La sube la entrega que habilita lo suyo.</summary>
public static class CatalogoDeTesoreria
{
    /// <summary>El módulo de Tesorería en los catálogos de plataforma, en la numeración y como participante.</summary>
    public const string Modulo = "TES";

    public const EntregaDeTesoreria EntregaVigente = EntregaDeTesoreria.T0;
}
