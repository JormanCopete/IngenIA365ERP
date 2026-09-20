using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Budgets;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

/// <summary>
/// Ejecución presupuestal de un mes y su acumulado enero..mes (feature 009 E2, US9, FR-063,
/// FR-064; vista <c>budget-execution</c> de <c>/api/reports/accounting</c>). Presupuestado de la
/// versión vigente; ejecutado = movimiento neto por naturaleza de la cuenta sobre el libro
/// (débito-positivo en activos, costos y gastos; crédito-positivo en ingresos), sumado desde las
/// cuentas de movimiento hacia arriba con la misma jerarquía del balance de prueba. La columna
/// «Ppto. inicial acum.» trae la versión 1, para ver cuánto se movió el presupuesto desde que se
/// aprobó. Cada fila lleva <c>_nodo = account:&lt;código&gt;</c> para abrir el libro auxiliar del mes.
///
/// <para>
/// Alcance de las líneas presupuestadas (decisión 15): una línea sin sucursal ni centro se compara
/// con TODO el movimiento de la cuenta (con el alcance de sucursal de quien consulta); una con
/// sucursal o centro, sólo con el que coincide. Con el filtro de sucursal o centro se comparan las
/// líneas presupuestadas para esa sucursal o centro contra su propio movimiento.
/// </para>
/// </summary>
public sealed record BudgetExecutionQuery(int Year, int Month, FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class BudgetExecutionQueryValidator : AbstractValidator<BudgetExecutionQuery>
{
    private static readonly HashSet<string> Formatos = new(StringComparer.OrdinalIgnoreCase) { "json", "xlsx", "pdf", "docx" };

    public BudgetExecutionQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(DateTime.UnixEpoch.Year, DateTime.MaxValue.Year);
        RuleFor(x => x.Month).InclusiveBetween(1, ArmadoDePresupuesto.Meses);
        RuleFor(x => x.Filtros).NotNull();
        RuleFor(x => x.Filtros.Level).InclusiveBetween(1, 6).When(x => x.Filtros?.Level is not null);
        RuleFor(x => x.Filtros.Format).Must(f => Formatos.Contains(f!)).When(x => !string.IsNullOrWhiteSpace(x.Filtros?.Format))
            .WithMessage("El formato es json, xlsx, pdf o docx.");
    }
}

public sealed class BudgetExecutionQueryHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock,
    ICurrentUserService user, AccountingAuditEmitter audit) : IRequestHandler<BudgetExecutionQuery, Result<TablaExportable>>
{
    public const string Vista = "budget-execution";

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Código"),
        new("Nombre"),
        new("Ppto. mes", TipoDeColumna.Moneda),
        new("Ejec. mes", TipoDeColumna.Moneda),
        new("Var. mes", TipoDeColumna.Moneda),
        new("% mes", TipoDeColumna.Porcentaje),
        new("Ppto. acum.", TipoDeColumna.Moneda),
        new("Ejec. acum.", TipoDeColumna.Moneda),
        new("Var. acum.", TipoDeColumna.Moneda),
        new("% acum.", TipoDeColumna.Porcentaje),
        new("Ppto. inicial acum.", TipoDeColumna.Moneda),
        new("Nodo", Clave: "_nodo"),
        new("Cuenta", Clave: "_cuenta"),
    ];

    /// <summary>Presupuestado y ejecutado de una cuenta, del mes y acumulados; se suman hacia arriba tal cual.</summary>
    private sealed record Valores(decimal PptoMes, decimal EjecMes, decimal PptoAcum, decimal EjecAcum, decimal PptoInicialAcum)
    {
        public static readonly Valores Cero = new(0m, 0m, 0m, 0m, 0m);
        public Valores Mas(Valores o) => new(PptoMes + o.PptoMes, EjecMes + o.EjecMes, PptoAcum + o.PptoAcum, EjecAcum + o.EjecAcum, PptoInicialAcum + o.PptoInicialAcum);
        public bool EsCero => PptoMes == 0m && EjecMes == 0m && PptoAcum == 0m && EjecAcum == 0m && PptoInicialAcum == 0m;
    }

    private sealed record LineaPresupuestada(int AccountId, int? BranchId, int? CostCenterId, byte Month, decimal Amount);
    private sealed record Movimiento(int AccountId, int BranchId, int? CostCenterId, int Month, decimal Debitos, decimal Creditos);

    public async Task<Result<TablaExportable>> Handle(BudgetExecutionQuery request, CancellationToken ct)
    {
        var desde = new DateOnly(request.Year, 1, 1);
        var hasta = new DateOnly(request.Year, request.Month, DateTime.DaysInMonth(request.Year, request.Month));
        var filtros = request.Filtros with { From = desde, To = hasta };

        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;

        // El libro: enero..mes, agrupado por cuenta, sucursal, centro y mes para poder respetar el alcance de cada línea presupuestada.
        var movimientos = await MovimientosContables.DelRango(MovimientosContables.Base(db, c), c)
            .GroupBy(e => new { e.AccountId, e.BranchId, e.CostCenterId, e.Date.Month })
            .Select(g => new Movimiento(g.Key.AccountId, g.Key.BranchId, g.Key.CostCenterId, g.Key.Month, g.Sum(e => e.Debit), g.Sum(e => e.Credit)))
            .ToListAsync(ct);

        // El presupuesto: la vigente y la inicial (versión 1) del año.
        var ejercicioId = await db.FiscalYears.AsNoTracking().Where(f => f.Year == request.Year && !f.IsDeleted).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct);
        var versiones = await db.Budgets.AsNoTracking().Where(b => ejercicioId != null && b.FiscalYearId == ejercicioId && !b.IsDeleted)
            .Select(b => new { b.Id, b.Version, b.Status, b.ApprovedAt, b.ApprovedBy }).ToListAsync(ct);
        var vigente = versiones.Where(v => v.Status != BudgetStatus.Superseded).OrderByDescending(v => v.Version).FirstOrDefault();
        var inicial = versiones.FirstOrDefault(v => v.Version == 1);

        var lineasVigente = vigente is null ? new List<LineaPresupuestada>() : await LineasAsync(vigente.Id, c, ct);
        var lineasInicial = inicial is null ? new List<LineaPresupuestada>() : inicial.Id == vigente?.Id ? lineasVigente : await LineasAsync(inicial.Id, c, ct);

        var plan = await JerarquiaDelPlan.CargarAsync(db, ct);
        var porCuenta = new Dictionary<int, Valores>();

        foreach (var g in lineasVigente.GroupBy(l => l.AccountId))
        {
            var pptoMes = g.Where(l => l.Month == request.Month).Sum(l => l.Amount);
            var pptoAcum = g.Where(l => l.Month <= request.Month).Sum(l => l.Amount);
            porCuenta[g.Key] = porCuenta.GetValueOrDefault(g.Key, Valores.Cero).Mas(new Valores(pptoMes, 0m, pptoAcum, 0m, 0m));
        }
        foreach (var g in lineasInicial.GroupBy(l => l.AccountId))
        {
            var pptoInicialAcum = g.Where(l => l.Month <= request.Month).Sum(l => l.Amount);
            porCuenta[g.Key] = porCuenta.GetValueOrDefault(g.Key, Valores.Cero).Mas(new Valores(0m, 0m, 0m, 0m, pptoInicialAcum));
        }

        // Alcance de cada cuenta presupuestada: sin sucursal ni centro en alguna línea, todo su movimiento; si no, sólo lo que coincide.
        var alcances = lineasVigente.GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => g.Select(l => (l.BranchId, l.CostCenterId)).Distinct().ToList());

        foreach (var g in movimientos.GroupBy(m => m.AccountId))
        {
            var cuenta = plan.PorId(g.Key);
            var naturaleza = cuenta?.Nature ?? AccountNature.Debit;
            var ejecMes = 0m;
            var ejecAcum = 0m;
            foreach (var m in g)
            {
                if (!EntraEnElEjecutado(alcances, m)) continue;
                var neto = naturaleza == AccountNature.Debit ? m.Debitos - m.Creditos : m.Creditos - m.Debitos;
                ejecAcum += neto;
                if (m.Month == request.Month) ejecMes += neto;
            }
            porCuenta[g.Key] = porCuenta.GetValueOrDefault(g.Key, Valores.Cero).Mas(new Valores(0m, ejecMes, 0m, ejecAcum, 0m));
        }

        // Hacia arriba por la cadena de padres, como el balance de prueba.
        var total = new Dictionary<int, Valores>();
        foreach (var (id, valores) in porCuenta)
        {
            total[id] = total.GetValueOrDefault(id, Valores.Cero).Mas(valores);
            if (plan.PorId(id) is not { } cuenta) continue;
            foreach (var padre in plan.Ancestros(cuenta))
                total[padre.Id] = total.GetValueOrDefault(padre.Id, Valores.Cero).Mas(valores);
        }

        var nivelMaximo = request.Filtros.Level
            ?? await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted).Select(s => (int?)s.MovementLevel).FirstOrDefaultAsync(ct)
            ?? 6;

        var filas = new List<FilaExportable>();
        foreach (var cuenta in plan.Cuentas.Where(a => a.Level <= nivelMaximo).OrderBy(a => a.Code, StringComparer.Ordinal))
        {
            if (!total.TryGetValue(cuenta.Id, out var v) || v.EsCero) continue;
            var clase = plan.AlNivel(cuenta, 1);
            filas.Add(new FilaExportable(
            [
                cuenta.Code, cuenta.Name,
                v.PptoMes, v.EjecMes, v.EjecMes - v.PptoMes, Porcentaje(v.EjecMes, v.PptoMes),
                v.PptoAcum, v.EjecAcum, v.EjecAcum - v.PptoAcum, Porcentaje(v.EjecAcum, v.PptoAcum),
                v.PptoInicialAcum,
                $"account:{cuenta.Code}", cuenta.PublicId,
            ], Seccion: $"{clase.Code} {clase.Name}", Resaltada: cuenta.Level == 1));
        }

        var cultura = CultureInfo.GetCultureInfo("es-CO");
        var nombreMes = cultura.DateTimeFormat.GetMonthName(request.Month);
        var notas = new List<string>(await EncabezadoDeInforme.NotasAsync(db, user, clock, filtros, c.PeriodoTexto, c, ct))
        {
            vigente is null
                ? $"El año {request.Year} no tiene presupuesto: las columnas presupuestadas van en cero."
                : $"Presupuesto versión {vigente.Version} ({Estado(vigente.Status)}{(vigente.ApprovedAt is { } a ? $", aprobado el {a:dd/MM/yyyy} por {vigente.ApprovedBy}" : string.Empty)}); «Ppto. inicial acum.» es la versión 1.",
            "Ejecutado = movimiento neto según la naturaleza de la cuenta; variación = ejecutado − presupuestado; % = ejecutado / presupuestado × 100 (vacío sin presupuesto). Una línea presupuestada sin sucursal ni centro se compara con todo el movimiento de la cuenta; con sucursal o centro, sólo con el que coincide.",
        };

        var tabla = new TablaExportable("Ejecución presupuestal",
            $"{cultura.TextInfo.ToTitleCase(nombreMes)} de {request.Year} · acumulado enero–{nombreMes}", Columnas, filas, null, notas);

        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Vista, new { request.Year, request.Month, filtros = request.Filtros }, request.Filtros.Format!, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }

    /// <summary>Las líneas vivas de una versión, restringidas a la sucursal y al centro del filtro cuando vienen.</summary>
    private async Task<List<LineaPresupuestada>> LineasAsync(int budgetId, MovimientosContables.Contexto c, CancellationToken ct)
    {
        var q = db.BudgetLines.AsNoTracking().Where(l => l.BudgetId == budgetId && !l.IsDeleted);
        if (c.SucursalId is { } sucursal) q = q.Where(l => l.BranchId == sucursal);
        if (c.CentroDeCostoId is { } centro) q = q.Where(l => l.CostCenterId == centro);
        return await q.Select(l => new LineaPresupuestada(l.AccountId, l.BranchId, l.CostCenterId, l.Month, l.Amount)).ToListAsync(ct);
    }

    /// <summary>Si el movimiento entra en el ejecutado de su cuenta según los alcances de las líneas presupuestadas (todo, si no hay presupuesto).</summary>
    private static bool EntraEnElEjecutado(Dictionary<int, List<(int? BranchId, int? CostCenterId)>> alcances, Movimiento m)
    {
        if (!alcances.TryGetValue(m.AccountId, out var ambitos)) return true;
        return ambitos.Any(a => (a.BranchId is null || a.BranchId == m.BranchId) && (a.CostCenterId is null || a.CostCenterId == m.CostCenterId));
    }

    private static decimal? Porcentaje(decimal ejecutado, decimal presupuestado) =>
        presupuestado == 0m ? null : Math.Round(ejecutado / presupuestado * 100m, 2, MidpointRounding.AwayFromZero);

    private static string Estado(BudgetStatus estado) => estado switch
    {
        BudgetStatus.Draft => "borrador",
        BudgetStatus.Approved => "aprobado",
        BudgetStatus.Superseded => "reemplazado",
        _ => estado.ToString(),
    };
}
