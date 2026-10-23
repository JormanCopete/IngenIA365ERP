namespace IngenIA365ERP.Domain.Receipts;

/// <summary>
/// Las entregas de Recaudo (feature 015, T0042; plan.md «Entregas y fases»), en el orden en que salen. Se convierte sola a
/// <c>EntregaDeModulo("RCD", n)</c>, que es lo que declaran las claves de <see cref="Parameters.ParametrosDeRecaudoYTesoreria"/>. R0 sale junto
/// con T0 de Tesorería (el recorte mínimo del 01/12/2026).
/// </summary>
public enum EntregaDeRecaudo
{
    R0 = 1,
    R1 = 2,
    R2 = 3,
}

/// <summary>
/// Lo que la versión desplegada sabe de Recaudo (feature 015, T0042): <see cref="EntregaVigente"/> es la entrega en producción; una clave, una
/// plantilla o un tipo de alerta de una entrega posterior no se ofrece ni se acepta todavía. La sube la entrega que habilita lo suyo.
/// </summary>
public static class CatalogoDeRecaudo
{
    /// <summary>El módulo de Recaudo en los catálogos de plataforma (<c>COR_ParameterVersions.Module</c>) y en la numeración.</summary>
    public const string Modulo = "RCD";

    /// <summary>El código de Recaudo como participante y en <c>ModuloContable</c>.</summary>
    public const string Codigo = "REC";

    public const EntregaDeRecaudo EntregaVigente = EntregaDeRecaudo.R0;
}
