using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Severance;

/// <summary>
/// Arma la relación de consignación por fondo de una corrida <c>Severance</c> (FR-012;
/// contracts/archivos.md §3.1): un bloque por fondo de cesantías con sus empleados —documento,
/// ingreso, base, días y valor de la línea <c>CESANTIAS</c>, más los intereses como dato
/// informativo—, el total por fondo y el general, y la marca de consignación si ya existe. Lo
/// comparten la consulta, la lista, el reporte y el comando que marca consignado, para que los
/// cuatro digan el mismo número. Un empleado sin fondo en la ficha va en un bloque propio sin
/// <c>fundPublicId</c>: el total general tiene que igualar la cuenta por pagar del comprobante.
/// Las fechas límite salen de los parámetros <c>DateInYear</c> vigentes al corte (D-07); si no
/// hay vigencia, van nulas y la pantalla no avisa. Nada se calcula: es una lectura de líneas.
/// </summary>
public sealed class DepositScheduleBuilder(IApplicationDbContext db)
{
    public const string SinFondo = "Sin fondo de cesantías en la ficha";

    public async Task<DepositScheduleDto> BuildAsync(PayrollRun run, CancellationToken ct)
    {
        var filas = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id
            select new
            {
                RunEmployeeId = re.Id, e.PublicId, e.SeveranceFundId, e.JoinDate, p.IdType, p.TaxId,
                p.FirstName, p.OtherNames, p.LastName, p.SecondLastName,
            }).ToListAsync(ct);
        var idsFilas = filas.Select(f => f.RunEmployeeId).ToList();

        var lineas = await db.PayrollRunLines.AsNoTracking()
            .Where(l => idsFilas.Contains(l.PayrollRunEmployeeId)
                        && (l.ConceptCode == WellKnownConceptCodes.Severance || l.ConceptCode == WellKnownConceptCodes.SeveranceInterest))
            .Select(l => new { l.PayrollRunEmployeeId, l.ConceptCode, l.Amount, l.Quantity, l.BaseAmount })
            .ToListAsync(ct);
        var lineasPorFila = lineas.ToLookup(l => l.PayrollRunEmployeeId);

        var idsFondos = filas.Select(f => f.SeveranceFundId).Where(id => id > 0).Distinct().ToList();
        var fondos = idsFondos.Count == 0
            ? []
            : await db.SeveranceProviders.AsNoTracking().IgnoreQueryFilters()
                .Where(f => idsFondos.Contains(f.Id))
                .Select(f => new { f.Id, f.PublicId, f.Code, f.Name, f.TaxId, PersonaNit = f.Person != null ? f.Person.TaxId : null })
                .ToListAsync(ct);
        var fondoPorId = fondos.ToDictionary(f => f.Id);

        var consignaciones = await db.SeveranceFundDeposits.AsNoTracking()
            .Where(d => d.PayrollRunId == run.Id)
            .ToListAsync(ct);

        var bloques = new List<DepositScheduleFundDto>();
        foreach (var grupo in filas.GroupBy(f => f.SeveranceFundId))
        {
            fondoPorId.TryGetValue(grupo.Key, out var fondo);
            var renglones = grupo
                .Select(f =>
                {
                    var cesantias = lineasPorFila[f.RunEmployeeId].FirstOrDefault(l => l.ConceptCode == WellKnownConceptCodes.Severance);
                    var intereses = lineasPorFila[f.RunEmployeeId].Where(l => l.ConceptCode == WellKnownConceptCodes.SeveranceInterest).Sum(l => l.Amount);
                    return new DepositScheduleLineDto(f.PublicId, f.IdType, f.TaxId, NombreDePersona.Completo(f.FirstName, f.OtherNames, f.LastName, f.SecondLastName), DateOnly.FromDateTime(f.JoinDate),
                        cesantias?.BaseAmount ?? 0m, cesantias?.Quantity ?? 0m, cesantias?.Amount ?? 0m, intereses);
                })
                .OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            // Un empleado sin línea de cesantías (todo excluido) no consigna nada y no aparece.
            renglones = renglones.Where(l => l.Amount != 0m || l.Interest != 0m).ToList();
            if (renglones.Count == 0) continue;

            var consignacion = fondo is null ? null : consignaciones.FirstOrDefault(c => c.SeveranceFundId == fondo.Id);
            bloques.Add(new DepositScheduleFundDto(
                fondo?.PublicId, fondo?.Code, fondo?.Name ?? SinFondo,
                fondo is null ? null : (string.IsNullOrWhiteSpace(fondo.PersonaNit) ? fondo.TaxId : fondo.PersonaNit),
                PilaCode: null,
                renglones, renglones.Count, renglones.Sum(l => l.Amount), renglones.Sum(l => l.Interest),
                consignacion?.DepositedAt, consignacion?.DepositedBy, consignacion?.Reference, consignacion?.Amount));
        }
        bloques = bloques.OrderBy(b => b.FundPublicId is null ? 1 : 0).ThenBy(b => b.FundName, StringComparer.CurrentCultureIgnoreCase).ToList();

        var (limite, limiteIntereses) = await FechasLimiteAsync(run, ct);
        return new DepositScheduleDto(run.PublicId, run.Year ?? run.CutoffDate!.Value.Year, run.Version, run.Status.ToString(), run.CutoffDate!.Value,
            bloques, bloques.Sum(b => b.Employees), bloques.Sum(b => b.Total), bloques.Sum(b => b.InterestTotal), limite, limiteIntereses);
    }

    /// <summary>La fecha límite de consignación y la de pago de los intereses, en el año siguiente al liquidado, desde los parámetros <c>DateInYear</c> vigentes al corte.</summary>
    public async Task<(DateOnly? Consignacion, DateOnly? Intereses)> FechasLimiteAsync(PayrollRun run, CancellationToken ct)
    {
        var corte = run.CutoffDate!.Value.ToDateTime(TimeOnly.MinValue);
        var anioSiguiente = (run.Year ?? run.CutoffDate.Value.Year) + 1;
        var codigos = new[] { SettlementParameterCodes.SeveranceDepositDeadline, SettlementParameterCodes.SeveranceInterestDeadline };
        var parametros = await db.PayrollLegalParameters.AsNoTracking()
            .Where(p => codigos.Contains(p.Code) && p.Kind == LegalParameterKind.DateInYear && p.ValidFrom <= corte && (p.ValidTo == null || p.ValidTo >= corte))
            .Select(p => new { p.Code, p.Value, p.ValidFrom })
            .ToListAsync(ct);

        DateOnly? Leer(string codigo)
        {
            var p = parametros.Where(x => x.Code == codigo).OrderByDescending(x => x.ValidFrom).FirstOrDefault();
            return p is not null && DateInYear.TryLeer(p.Value, anioSiguiente, out var fecha) ? fecha : null;
        }

        return (Leer(SettlementParameterCodes.SeveranceDepositDeadline), Leer(SettlementParameterCodes.SeveranceInterestDeadline));
    }
}
