using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Settlement;

/// <summary>Lo que dejó calcular la definitiva: la corrida en borrador, el empleado cargado, el resultado del motor y los avisos.</summary>
public sealed record CalculoDeDefinitiva(
    PayrollRun Run,
    LoadedSettlementEmployee Cargado,
    SettlementResult Resultado,
    IReadOnlyList<WarningDto> Avisos,
    IReadOnlyList<SettlementDeduction> Deducciones);

/// <summary>
/// El camino común de registrar y recalcular la definitiva (feature 010, US3, R7): cargar al
/// empleado con <see cref="SettlementInputLoader"/> —que ya propone las deudas desde Cartera y las
/// libranzas y conserva los ajustes cuyo propuesto no cambió—, calcular con el motor puro, volver
/// persistentes las deudas propuestas (<c>PAY_SettlementDeductions</c>: crea las nuevas, actualiza
/// las que siguen y retira las que Cartera ya no trae), crear la corrida <c>Settlement</c> en
/// borrador con el molde común y enlazar cada línea <c>DESC_CARTERA</c>/<c>LIBRANZA</c> con su
/// descuento por <c>SettlementDeductionId</c>. Guarda dos veces (los descuentos necesitan Id antes
/// de enlazar): quien lo llama lo envuelve en <see cref="TransaccionDeLiquidacion"/>.
/// </summary>
public sealed class DefinitivaCalculator(
    IApplicationDbContext db,
    SettlementInputLoader loader,
    SettlementRunPersister persister,
    IDateTimeService clock,
    ICurrentUserService user)
{
    public async Task<Result<CalculoDeDefinitiva>> CalcularAsync(EmploymentTermination terminacion, Employee empleado, bool recalculo, CancellationToken ct)
    {
        var batch = await loader.LoadAsync(SettlementLoadRequest.Definitiva(empleado.Id, terminacion.Id, terminacion.TerminationDate), ct);
        var faltantes = batch.MissingRequiredParameters;
        if (faltantes.Count > 0)
            return Result.Failure<CalculoDeDefinitiva>(SettlementErrors.ParametersMissing(faltantes, terminacion.TerminationDate.ToDateTime(TimeOnly.MinValue)));

        var cargado = batch.Employees.FirstOrDefault(e => e.Employee.Id == empleado.Id);
        if (cargado is null) return Result.Failure<CalculoDeDefinitiva>(SettlementErrors.EmployeeNotFound);

        SettlementResult resultado;
        try
        {
            resultado = new SettlementCalculationEngine().Calculate(cargado.Input);
        }
        catch (CalculationRefusedException ex)
        {
            return Result.Failure<CalculoDeDefinitiva>(SettlementErrors.CalculationRefused(cargado.FullName, ex.Message));
        }

        var key = SettlementRunKey.Definitiva(empleado.Id, terminacion.TerminationDate);
        var anteriores = await persister.CorridasDeAsync(key, ct);
        if (SettlementRunPersister.Duplicado(anteriores, key, recalculo) is { } duplicado)
            return Result.Failure<CalculoDeDefinitiva>(duplicado);

        // --- los descuentos propuestos se vuelven filas (necesitan Id para enlazar las líneas) ---
        var deducciones = await SincronizarDeduccionesAsync(terminacion, cargado.Deudas, ct);
        await db.SaveChangesAsync(ct);

        var calculados = new List<SettlementCalculatedEmployee> { new(cargado, resultado) };
        var run = persister.CrearBorrador(batch, key, calculados, anteriores, terminationId: terminacion.Id);
        EnlazarLineas(run, cargado.Deudas, deducciones);
        await db.SaveChangesAsync(ct);

        var avisos = batch.Warnings.Concat(resultado.Warnings.Select(w => new WarningDto("Payroll.Settlement.Warning", w, null))).ToList();
        return Result.Success(new CalculoDeDefinitiva(run, cargado, resultado, avisos, deducciones));
    }

    /// <summary>
    /// Deja <c>PAY_SettlementDeductions</c> igual a la propuesta: la fila que ya existía (misma
    /// obligación) se actualiza —propuesto, desglose, aplicado, motivo—, la nueva se crea y la que
    /// Cartera ya no trae (saldo en cero, crédito cerrado) se retira en blando. Un ajuste conservado
    /// por el cargador llega con <c>Applied &lt; Proposed</c> y su motivo: se respeta.
    /// </summary>
    private async Task<List<SettlementDeduction>> SincronizarDeduccionesAsync(EmploymentTermination terminacion, IReadOnlyList<DeudaPropuesta> deudas, CancellationToken ct)
    {
        var ahora = clock.UtcNow;
        var quien = user.UserName;
        var existentes = terminacion.Deductions.Count > 0
            ? terminacion.Deductions.Where(d => !d.IsDeleted).ToList()
            : await db.SettlementDeductions.Where(d => d.TerminationId == terminacion.Id).ToListAsync(ct);
        var vivas = new List<SettlementDeduction>();

        foreach (var deuda in deudas)
        {
            var fila = existentes.FirstOrDefault(d => Coincide(d, deuda));
            if (fila is null)
            {
                fila = new SettlementDeduction
                {
                    TerminationId = terminacion.Id,
                    Kind = deuda.Kind,
                    LoanPortfolioId = deuda.LoanPortfolioId,
                    RecurringNoveltyId = deuda.RecurringNoveltyId,
                    CreatedAt = ahora,
                    CreatedBy = quien,
                };
                // Sólo por la navegación: la terminación está rastreada y EF descubre la fila como nueva al guardar.
                // Agregarla también al DbSet la duplicaba en la colección por el fixup del contexto.
                terminacion.Deductions.Add(fila);
            }
            else
            {
                fila.UpdatedAt = ahora;
                fila.UpdatedBy = quien;
            }

            var conservaAjuste = deuda.Applied < deuda.Proposed;
            fila.Description = deuda.Description;
            fila.ProposedAmount = deuda.Proposed;
            fila.ProposedBreakdownJson = deuda.BreakdownJson;
            fila.AppliedAmount = deuda.Applied;
            fila.AdjustmentReason = conservaAjuste ? deuda.AdjustmentReason : null;
            if (!conservaAjuste)
            {
                fila.AdjustedBy = null;
                fila.AdjustedAt = null;
            }
            if (fila.Status is not (SettlementDeductionStatus.Applied or SettlementDeductionStatus.Reverted))
                fila.Status = conservaAjuste ? SettlementDeductionStatus.Adjusted : SettlementDeductionStatus.Proposed;
            vivas.Add(fila);
        }

        foreach (var sobrante in existentes.Where(d => !vivas.Contains(d) && d.Status is not (SettlementDeductionStatus.Applied or SettlementDeductionStatus.Reverted)))
        {
            sobrante.IsDeleted = true;
            sobrante.DeletedAt = ahora;
            sobrante.DeletedBy = quien;
        }

        return vivas;
    }

    private static bool Coincide(SettlementDeduction fila, DeudaPropuesta deuda) =>
        fila.Kind == deuda.Kind
        && ((deuda.LoanPortfolioId is { } prestamo && fila.LoanPortfolioId == prestamo)
            || (deuda.RecurringNoveltyId is { } libranza && fila.RecurringNoveltyId == libranza)
            || (deuda.DeductionPublicId is { } pid && fila.PublicId == pid));

    /// <summary>
    /// El motor vuelve línea cada deuda con valor aplicado mayor que cero, en el orden en que llegaron
    /// (<c>ProposedDeductionsRule</c>): las líneas <c>DESC_CARTERA</c>/<c>LIBRANZA</c> de la corrida, por
    /// <c>Order</c>, se emparejan con esas deudas en el mismo orden.
    /// </summary>
    public static void EnlazarLineas(PayrollRun run, IReadOnlyList<DeudaPropuesta> deudas, IReadOnlyList<SettlementDeduction> deducciones)
    {
        var fila = run.Employees.FirstOrDefault();
        if (fila is null) return;
        var lineas = fila.Lines
            .Where(l => l.ConceptCode.Equals(WellKnownConceptCodes.LoanDeduction, StringComparison.OrdinalIgnoreCase)
                        || l.ConceptCode.Equals(SettlementInputLoader.LibranzaCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(l => l.Order).ToList();
        var conValor = deudas.Where(d => d.Applied > 0m).ToList();

        for (var i = 0; i < lineas.Count && i < conValor.Count; i++)
        {
            var deuda = conValor[i];
            var linea = lineas[i];
            var descuento = deducciones.FirstOrDefault(d => Coincide(d, deuda));
            if (descuento is not null) linea.SettlementDeductionId = descuento.Id;
        }
    }
}
