using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Accrual;

/// <summary>Por qué está suspendida la causación (FR-065, T30), del disparador más fuerte al más débil.</summary>
public enum MotivoDeSuspension
{
    /// <summary>La altura de mora llegó al umbral vigente de la modalidad.</summary>
    AlturaDeMora = 1,

    /// <summary>Reestructurado más de una vez: reconoce sus intereses sólo cuando los cobra.</summary>
    Reestructuraciones = 2,

    /// <summary>En proceso de insolvencia.</summary>
    Insolvencia = 3,

    /// <summary>Castigado.</summary>
    Castigo = 4,

    /// <summary>
    /// K3 (FR-097, T102): el deudor falleció y la cooperativa decidió que la causación no sigue tras el fallecimiento
    /// (<c>Causacion.ContinuaTrasFallecimiento = false</c>, por línea). (nuevo)
    /// </summary>
    Fallecimiento = 5,
}

/// <summary>El cambio de estado de la causación de un día: Application emite <c>CausacionSuspendida</c> o <c>CausacionReanudada</c>.</summary>
public enum TransicionDeCausacion
{
    Suspendida = 1,
    Reanudada = 2,
}

/// <summary>
/// Lo que la regla mira de un crédito un día: su altura de mora, el umbral vigente de su modalidad (la primera altura que suspende;
/// nulo si la norma no fija uno), cuántas veces se reestructuró, si ese día está en insolvencia o castigado y si ya pasó el fallecimiento del
/// deudor con la causación que no sigue tras él (K3, <see cref="TrasFallecimiento"/>).
/// </summary>
public sealed record EntradaDeSuspension(int AlturaDeMora, int? Umbral, int Reestructuraciones, bool EnInsolvencia, bool Castigado,
    bool TrasFallecimiento = false);

/// <summary>
/// La suspensión y la reanudación de la causación como <b>transiciones del estado</b> (feature 013, US4, T0487; T30, R38, M9;
/// FR-065): no son una marca que ponga el proceso —una fecha valor anterior no la reconstruiría—, sino lo que sale cada día de la
/// reconstrucción.
/// <list type="bullet">
/// <item>Se suspende si está castigado, en insolvencia, tras el fallecimiento del deudor si la causación no sigue (K3), reestructurado <b>más de una vez</b> o con altura <b>mayor o igual</b> al umbral
/// vigente de su modalidad (el umbral es la primera altura que suspende: con «más de 60 días», 61).</item>
/// <item>Se reanuda por la regla por defecto: cuando ya no hay ningún disparador —la altura quedó debajo del umbral vigente—.</item>
/// <item>Suspendida, lo nuevo se causa en <see cref="LoanConcept.CurrentInterestInSuspense"/> y
/// <see cref="LoanConcept.DefaultInterestInSuspense"/> (el castigo usa los mismos con su marca); lo causado antes no se reclasifica
/// (lo deteriora SARC al 100 %).</item>
/// </list>
/// Pura. (nuevo)
/// </summary>
public static class ReglaDeSuspension
{
    /// <summary>Desde cuántas reestructuraciones se suspende: «más de una vez».</summary>
    public const int ReestructuracionesQueSuspenden = 2;

    /// <summary>El motivo más fuerte por el que está suspendida, o nulo si causa normal.</summary>
    public static MotivoDeSuspension? Evaluar(EntradaDeSuspension e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Castigado) return MotivoDeSuspension.Castigo;
        if (e.EnInsolvencia) return MotivoDeSuspension.Insolvencia;
        if (e.TrasFallecimiento) return MotivoDeSuspension.Fallecimiento;
        if (e.Reestructuraciones >= ReestructuracionesQueSuspenden) return MotivoDeSuspension.Reestructuraciones;
        if (e.Umbral is { } umbral && e.AlturaDeMora >= umbral) return MotivoDeSuspension.AlturaDeMora;
        return null;
    }

    /// <summary>La transición de un día a otro, o nula si sigue igual.</summary>
    public static TransicionDeCausacion? Transicion(bool suspendidaAntes, bool suspendidaAhora) =>
        suspendidaAntes == suspendidaAhora ? null
        : suspendidaAhora ? TransicionDeCausacion.Suspendida
        : TransicionDeCausacion.Reanudada;

    /// <summary>A qué concepto va la corriente.</summary>
    public static LoanConcept ConceptoCorriente(bool suspendida) =>
        suspendida ? LoanConcept.CurrentInterestInSuspense : LoanConcept.CurrentInterest;

    /// <summary>A qué concepto va la mora.</summary>
    public static LoanConcept ConceptoDeMora(bool suspendida) =>
        suspendida ? LoanConcept.DefaultInterestInSuspense : LoanConcept.DefaultInterest;
}
