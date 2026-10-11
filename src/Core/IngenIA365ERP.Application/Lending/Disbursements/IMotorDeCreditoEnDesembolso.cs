using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>Lo que cuesta cancelar un crédito recogido a la fecha valor, y sus días de mora. (nuevo)</summary>
public sealed record LiquidacionParaRecogida(decimal ParaCancelar, int DiasDeMora, bool PerdioCapacidad);

/// <summary>
/// Lo que el desembolso necesita del motor del crédito (feature 013, US3; T0443, T0444 pasos 3, T0447) y que construye la fase 8 (US4,
/// <c>ReconstruccionDeCredito</c>, <c>PuestaAlDiaDeCredito</c>, <c>RegistroDeCartera.Conciliar</c>): <b>punto de extensión</b> con firma estable.
/// Hasta la fase 8 la registrada es <see cref="MotorDeCreditoEnDesembolsoProvisional"/>; la fase 8 la reemplaza en <c>AddApplication</c>
/// sin tocar a quien la usa. (nuevo)
/// </summary>
public interface IMotorDeCreditoEnDesembolso
{
    /// <summary>La liquidación de un crédito recogido a <paramref name="fecha"/> (lo que muestra la vista previa como <c>toPayOff</c>).</summary>
    Task<LiquidacionParaRecogida> LiquidarAsync(int loanId, DateOnly fecha, CancellationToken ct);

    /// <summary>
    /// Pone al día la causación de los créditos hasta <paramref name="fecha"/> dentro de la operación (T6, C04: sus partes <c>LoanCatchUp</c>).
    /// Corre bajo el cerrojo de los créditos y sin guardar.
    /// </summary>
    Task PonerAlDiaAsync(IReadOnlyCollection<int> loanIds, DateOnly fecha, CancellationToken ct);

    /// <summary>
    /// La diferencia de causación por (día valor, concepto, cuota) de reversar un tramo cuando el crédito tiene otros (C49): filas
    /// <c>AccrualAdjustment</c> de la operación <paramref name="reverso"/> desde <paramref name="desde"/>, sin agregarlas (las registra quien
    /// reversa). Vacía si no hay diferencia.
    /// </summary>
    Task<IReadOnlyList<LoanTransaction>> ConciliarCausacionAsync(int loanId, DateOnly desde, LoanOperation reverso, DateOnly fechaContable, CancellationToken ct);
}

/// <summary>
/// La implementación provisional hasta la fase 8 (feature 013, US3): liquida con la última posición diaria del crédito (o, sin posición, con la
/// suma de su libro sin anticipos ni saldos a favor y sin mora), no pone al día nada —no hay motor de causación todavía— y no concilia
/// causación (sin causación no hay diferencia). Sólo lee. (nuevo)
/// </summary>
public sealed class MotorDeCreditoEnDesembolsoProvisional(IApplicationDbContext db) : IMotorDeCreditoEnDesembolso
{
    public async Task<LiquidacionParaRecogida> LiquidarAsync(int loanId, DateOnly fecha, CancellationToken ct)
    {
        var posicion = await db.LoanDailyPositions.AsNoTracking()
            .Where(p => p.LoanId == loanId && p.PositionDate <= fecha)
            .OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version)
            .Select(p => new { p.PayoffAmount, p.DaysPastDue })
            .FirstOrDefaultAsync(ct);
        if (posicion is not null) return new LiquidacionParaRecogida(posicion.PayoffAmount, posicion.DaysPastDue, false);

        var saldo = await db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == loanId && t.ValueDate <= fecha && !t.InControl
                        && t.Concept != LoanConcept.Advance && t.Concept != LoanConcept.CreditBalance)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        return new LiquidacionParaRecogida(Math.Max(0m, saldo), 0, false);
    }

    public Task PonerAlDiaAsync(IReadOnlyCollection<int> loanIds, DateOnly fecha, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<LoanTransaction>> ConciliarCausacionAsync(int loanId, DateOnly desde, LoanOperation reverso, DateOnly fechaContable,
        CancellationToken ct) => Task.FromResult<IReadOnlyList<LoanTransaction>>([]);
}
