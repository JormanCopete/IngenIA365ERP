using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero.Contabilidad;

// 2026-11-17 (pedido del dueño): las cifras de Nómina en el tablero de inicio. Salen de la liquidación del período
// (/nomina/liquidacion, Payroll.Runs.*): el período abierto más próximo y la corrida ordinaria vigente de ese período.

/// <summary>
/// La próxima nómina: el período abierto o calculado (sin aprobar) que empieza primero. La cifra es el devengado del último
/// cálculo (la corrida ordinaria en borrador o desactualizada, la versión más alta); sin cálculo, la cifra va vacía y la nota lo dice.
/// La nota lleva también los empleados activos (fichas sin retiro, <c>Status != -1</c>, la regla de los saldos iniciales).
/// </summary>
public sealed class ProximaNomina(IApplicationDbContext db) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "nomina.proxima", "Próxima nómina", "Nómina", "Payroll.Runs.View",
        AdmiteSoloLoMio: false, TipoDeIndicador.Cifra, FormatoDeIndicador.Moneda, "/nomina/liquidacion",
        "El período de nómina abierto más próximo: el devengado de su último cálculo y los empleados activos.",
        PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var periodo = await db.PayPeriods.AsNoTracking()
            .Where(p => !p.IsDeleted && (p.Status == PayPeriodStatus.Open || p.Status == PayPeriodStatus.Calculated))
            .OrderBy(p => p.StartDate).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.StartDate, p.EndDate })
            .FirstOrDefaultAsync(ct);
        if (periodo is null) return Result.Success(new ResultadoDeIndicador(null, Nota: "No hay un período de nómina abierto."));

        var calculo = await db.PayrollRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && r.PayPeriodId == periodo.Id && r.Kind == PayrollRunKind.Ordinary
                        && (r.Status == PayrollRunStatus.Draft || r.Status == PayrollRunStatus.Stale))
            .OrderByDescending(r => r.Version)
            .Select(r => new { r.TotalEarnings, r.EmployeeCount })
            .FirstOrDefaultAsync(ct);
        var activos = await db.Employees.AsNoTracking().CountAsync(e => !e.IsDeleted && e.Status != -1, ct);

        var rango = $"{periodo.StartDate:dd/MM} a {periodo.EndDate:dd/MM/yyyy}";
        var empleados = activos == 1 ? "1 empleado activo" : $"{activos} empleados activos";
        return Result.Success(calculo is null
            ? new ResultadoDeIndicador(null, Nota: $"Período del {rango}, sin calcular · {empleados}")
            : new ResultadoDeIndicador(calculo.TotalEarnings, Nota: $"Devengado del período del {rango} ({calculo.EmployeeCount} liquidados) · {empleados}"));
    }
}

/// <summary>Nóminas ordinarias calculadas que esperan aprobación (corrida en borrador; una desactualizada se recalcula primero).</summary>
public sealed class NominasPorAprobar(IApplicationDbContext db) : IPendienteDelTablero
{
    public DefinicionDePendiente Definicion { get; } = new(
        "nomina.por-aprobar", "Nóminas calculadas por aprobar", "Nómina", "Payroll.Runs.Approve", "/nomina/liquidacion");

    public Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct) =>
        db.PayrollRuns.AsNoTracking()
            .CountAsync(r => !r.IsDeleted && r.Kind == PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Draft, ct);
}
