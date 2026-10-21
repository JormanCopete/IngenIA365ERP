using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Processes;

/// <summary>Cómo quedó una corrida después de una tanda. (nuevo)</summary>
public enum AvanceDeCorrida
{
    /// <summary>Quedan tandas: la corrida sigue <c>Running</c> y la retoma la pasada siguiente (o la misma, si hay presupuesto).</summary>
    Pendiente = 1,

    /// <summary>La corrida terminó; con créditos fallidos queda <c>CompletedWithErrors</c>.</summary>
    Terminada = 2,
}

/// <summary>
/// El que conduce las tandas de una clase de corrida (feature 013, T5, PM-13, C59, T0225): <c>DailyCycle</c> y
/// <c>NationalValueCorrection</c> los agrega US4; <c>MonthlyClose</c> y <c>CloseReopen</c>, US9. Una clase sin ejecutor registrado
/// deja sus corridas en <c>Requested</c> con una advertencia (<see cref="ConductorDeCorridasDeCartera"/>). Cada tanda escribe sus
/// transacciones, sus partes y sus mensajes en un solo guardado, avanza <c>CursorLoanId</c> y los contadores de la corrida, y no
/// pasa del presupuesto. (nuevo)
/// </summary>
public interface IEjecutorDeCorridaDeCartera
{
    /// <summary>La clase de corrida que conduce.</summary>
    LendingProcessKind Clase { get; }

    /// <summary>Una tanda de la corrida, dentro de <paramref name="presupuesto"/>.</summary>
    Task<AvanceDeCorrida> EjecutarTandaAsync(LendingProcessRun corrida, TimeSpan presupuesto, CancellationToken ct);
}
