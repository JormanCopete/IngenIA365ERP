using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// La guarda de los aportes en el riesgo (feature 013, US9, T0693, T0694; D19; FR-100, FR-103): la excepción de aportes en el
/// arrastre y el descuento de aportes en la base del deterioro individual sólo aplican si la norma los admite, la entidad tiene
/// indicadores vigentes (sin pérdidas, con solvencia: <c>LectorDeIndicadoresDeLaEntidad</c>, C33) <b>y no rige D19</b>. Mientras
/// Aportes y Ahorros no ofrezcan su consulta rige D19: la excepción no se aplica, el descuento es cero y la explicación lo dice.
///
/// <para>Nota de alcance (T0694): la excepción, el descuento a prorrata y el ahorro permanente de los fondos de empleados quedan para
/// la feature de Aportes; aquí sólo la guarda que los deja en cero. Si alguien la abre antes, el motor se niega en vez de inventar el
/// cálculo. (nuevo)</para>
/// </summary>
/// <param name="RigeD19">¿Rige D19 (no hay consulta de aportes)? En K1 siempre.</param>
/// <param name="IndicadoresDisponibles">¿La entidad tiene indicadores vigentes (C33)?</param>
public sealed record ContextoDeAportes(bool RigeD19, bool IndicadoresDisponibles)
{
    /// <summary>K1: rige D19 y no hay indicadores.</summary>
    public static ContextoDeAportes D19SinIndicadores { get; } = new(true, false);

    /// <summary>¿Los aportes cuentan con esta norma? Si la respuesta fuera sí, el motor se niega (<see cref="ExigirQueNoApliquen"/>).</summary>
    public bool Aplican(Anexo1 norma) => !RigeD19 && IndicadoresDisponibles && norma.Aportes.Deducibles;

    /// <summary>Por qué no cuentan, para la explicación.</summary>
    public string MotivoDeQueNoApliquen(Anexo1 norma) =>
        RigeD19 ? "rige D19: Aportes y Ahorros no ofrecen su consulta"
        : !IndicadoresDisponibles ? "la entidad no tiene indicadores vigentes (pérdidas, solvencia)"
        : !norma.Aportes.Deducibles ? "la norma vigente no los admite"
        : string.Empty;

    /// <summary>Se niega si los aportes aplicarían: su cálculo es de la feature de Aportes.</summary>
    public void ExigirQueNoApliquen(Anexo1 norma)
    {
        if (Aplican(norma))
            throw new NotSupportedException("La excepción y el descuento de aportes son de la feature de Aportes (D19); hasta entonces no se calculan.");
    }
}
