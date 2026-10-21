using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>Lo que cuesta cancelar un crédito recogido a la fecha valor, y sus días de mora. (nuevo)</summary>
public sealed record LiquidacionParaRecogida(decimal ParaCancelar, int DiasDeMora, bool PerdioCapacidad);

/// <summary>
/// Lo que el desembolso necesita del motor del crédito (feature 013, US3; T0443, T0444 pasos 3, T0447): <b>punto de extensión</b> con firma
/// estable que la fase 8 (US4) llena con <see cref="MotorDeCreditoEnDesembolso"/> sin tocar a quien la usa. US4 le suma
/// <see cref="RegistrarPuestaAlDiaAsync"/>: la parte <c>LoanCatchUp</c> apunta a la operación, así que lo calculado se escribe cuando la
/// operación ya tiene su <c>Id</c>. (nuevo)
/// </summary>
public interface IMotorDeCreditoEnDesembolso
{
    /// <summary>La liquidación de un crédito recogido a <paramref name="fecha"/> (lo que muestra la vista previa como <c>toPayOff</c>).</summary>
    Task<LiquidacionParaRecogida> LiquidarAsync(int loanId, DateOnly fecha, CancellationToken ct);

    /// <summary>
    /// Pone al día la causación de los créditos hasta <paramref name="fecha"/> dentro de la operación (T6, C04: sus partes <c>LoanCatchUp</c>).
    /// Corre bajo el cerrojo de los créditos y sin guardar; lo calculado se escribe con <see cref="RegistrarPuestaAlDiaAsync"/>.
    /// </summary>
    Task PonerAlDiaAsync(IReadOnlyCollection<int> loanIds, DateOnly fecha, CancellationToken ct);

    /// <summary>
    /// Escribe la puesta al día calculada por <see cref="PonerAlDiaAsync"/> con la operación que la pidió, ya guardada (US4, T0496): partes,
    /// filas, marcas, posiciones y mensajes. Sin guardar. Sin nada calculado, no hace nada.
    /// </summary>
    Task<Result<IReadOnlyList<IntegrationMessage>>> RegistrarPuestaAlDiaAsync(LoanOperation disparadora, CancellationToken ct);

    /// <summary>
    /// La diferencia de causación por (día valor, concepto, cuota) de reversar un tramo cuando el crédito tiene otros (C49): filas
    /// <c>AccrualAdjustment</c> de la operación <paramref name="reverso"/> desde <paramref name="desde"/>, sin agregarlas (las registra quien
    /// reversa). Vacía si no hay diferencia.
    /// </summary>
    Task<IReadOnlyList<LoanTransaction>> ConciliarCausacionAsync(int loanId, DateOnly desde, LoanOperation reverso, DateOnly fechaContable, CancellationToken ct);
}

/// <summary>
/// El motor del crédito en el desembolso (feature 013, US4, T0496; reemplaza la implementación provisional de la fase 7):
/// <list type="bullet">
/// <item><see cref="LiquidarAsync"/>: la reconstrucción a la fecha (<c>ValorParaCancelar</c> y la altura de mora); un crédito sin condiciones
/// selladas (anterior a la activación) se liquida con su última posición o, sin ella, con su libro;</item>
/// <item><see cref="PonerAlDiaAsync"/>/<see cref="RegistrarPuestaAlDiaAsync"/>: <see cref="PuestaAlDiaDeCredito"/>, causar en bucle desde la marca;</item>
/// <item><see cref="ConciliarCausacionAsync"/>: <see cref="MotorDeCartera.Verificar"/> del libro contra la reconstrucción con el reverso ya
/// incluido, desde <c>desde</c> y sin el capital (que espeja el reverso): las diferencias son las filas de ajuste.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class MotorDeCreditoEnDesembolso(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    PuestaAlDiaDeCredito puestaAlDia) : IMotorDeCreditoEnDesembolso
{
    public async Task<LiquidacionParaRecogida> LiquidarAsync(int loanId, DateOnly fecha, CancellationToken ct)
    {
        var cargado = await cargador.CargarAsync(loanId, null, fecha, ct);
        if (cargado is { Listo: true })
        {
            var estado = MotorDeCartera.Reconstruir(cargado.Entrada!, fecha);
            if (estado.Origen is not null) return new LiquidacionParaRecogida(estado.ValorParaCancelar, estado.AlturaDeMora.Dias, false);
        }

        // Sin condiciones selladas (o sin poder armar la reconstrucción): la última posición diaria o, sin ella, lo que dice el libro, sin
        // anticipos ni saldos a favor y sin mora.
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

    public Task PonerAlDiaAsync(IReadOnlyCollection<int> loanIds, DateOnly fecha, CancellationToken ct) => puestaAlDia.PrepararAsync(loanIds, fecha, ct);

    public Task<Result<IReadOnlyList<IntegrationMessage>>> RegistrarPuestaAlDiaAsync(LoanOperation disparadora, CancellationToken ct) =>
        puestaAlDia.RegistrarAsync(disparadora, ct);

    public async Task<IReadOnlyList<LoanTransaction>> ConciliarCausacionAsync(int loanId, DateOnly desde, LoanOperation reverso, DateOnly fechaContable,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reverso);
        var marca = await db.LoanControls.AsNoTracking().Where(c => c.LoanId == loanId).Select(c => (DateOnly?)c.LastAccruedDate).FirstOrDefaultAsync(ct);
        if (marca is not { } hasta || hasta < desde) return [];
        var cargado = await cargador.CargarAsync(loanId, null, hasta, ct);
        if (cargado is not { Listo: true }) return [];

        var entrada = CargadorDeReconstruccion.ConOperacion(cargado.Entrada!, reverso);
        var libro = (await cargador.LibroAsync([loanId], hasta, ct))[loanId];
        var diferencias = MotorDeCartera.Verificar(entrada, hasta, libro)
            .Where(d => d.Dia >= desde && d.Concepto != LoanConcept.Principal)
            .ToList();
        return RegistroDeCartera.Diferencias(loanId, diferencias, reverso, null, fechaContable, reverso.RegisteredAt, null);
    }
}
