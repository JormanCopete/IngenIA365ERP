namespace IngenIA365ERP.Domain.Payroll.Calculation.Bases;

/// <summary>
/// Arma los tramos de salario del empleado dentro del período (FR-008): recorta por
/// ingreso y retiro, parte en cada cambio de salario con efecto dentro del período y
/// resta a cada tramo los días de ausencia que le caen encima. Los días son
/// comerciales (<see cref="CalendarConventions"/>).
/// </summary>
public static class SalaryTranches
{
    public static IReadOnlyList<SalaryTranche> Build(
        PeriodInput period,
        EmployeeInput employee,
        IReadOnlyList<(DateTime From, DateTime To)> absences)
    {
        var start = period.StartDate.Date;
        var end = period.EndDate.Date;
        if (employee.JoinDate.Date > start) start = employee.JoinDate.Date;
        if (employee.TerminationDate is { } fin && fin.Date < end) end = fin.Date;
        if (end < start) return [];

        var history = employee.SalaryHistory.OrderBy(h => h.EffectiveDate).ToList();
        if (history.Count == 0)
            throw new CalculationRefusedException(
                $"El empleado {employee.DisplayName} ({employee.PublicId}) no tiene salario registrado.", []);

        // Salario vigente al inicio del tramo: el último cambio con efecto <= fecha.
        var enVigor = history.LastOrDefault(h => h.EffectiveDate.Date <= start) ?? history[0];

        var cortes = history
            .Where(h => h.EffectiveDate.Date > start && h.EffectiveDate.Date <= end)
            .Select(h => h.EffectiveDate.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var tramos = new List<SalaryTranche>();
        var desde = start;
        var salario = enVigor.MonthlySalary;
        foreach (var corte in cortes)
        {
            tramos.Add(Tramo(desde, corte.AddDays(-1), salario, absences));
            desde = corte;
            salario = history.Last(h => h.EffectiveDate.Date <= corte).MonthlySalary;
        }
        tramos.Add(Tramo(desde, end, salario, absences));

        return tramos.Where(t => t.Days > 0).ToList();
    }

    private static SalaryTranche Tramo(DateTime from, DateTime to, decimal salary,
        IReadOnlyList<(DateTime From, DateTime To)> absences)
    {
        var dias = CalendarConventions.Days(from, to);
        var ausencia = 0;
        foreach (var (aFrom, aTo) in absences)
        {
            var solape = CalendarConventions.Overlap(from, to, aFrom, aTo);
            if (solape is { } s) ausencia += CalendarConventions.Days(s.From, s.To);
        }
        return new SalaryTranche(from, to, dias, Math.Min(ausencia, dias), salary);
    }

    /// <summary>Salario mensual vigente en una fecha del período (el del tramo que la contiene; si ninguno, el último).</summary>
    public static decimal SalaryAt(IReadOnlyList<SalaryTranche> tranches, DateTime date)
    {
        if (tranches.Count == 0) return 0m;
        var t = tranches.FirstOrDefault(x => x.From <= date.Date && date.Date <= x.To);
        return (t ?? tranches[^1]).MonthlySalary;
    }

    /// <summary>Salario del cierre del período: el del último tramo.</summary>
    public static decimal SalaryAtEnd(IReadOnlyList<SalaryTranche> tranches) =>
        tranches.Count == 0 ? 0m : tranches[^1].MonthlySalary;

    /// <summary>Días comerciales de un rango de fechas de novedad que caen dentro del período.</summary>
    public static int DaysWithinPeriod(PeriodInput period, DateTime? from, DateTime? to)
    {
        if (from is null || to is null) return 0;
        var solape = CalendarConventions.Overlap(period.StartDate, period.EndDate, from.Value, to.Value);
        return solape is { } s ? CalendarConventions.Days(s.From, s.To) : 0;
    }
}
