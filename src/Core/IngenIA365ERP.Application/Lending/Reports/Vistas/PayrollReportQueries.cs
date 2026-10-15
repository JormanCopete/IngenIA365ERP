using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>payroll-agreements</c> de <c>/api/reports/lending</c> (feature 013, K2, T0846; FR-114, FR-133; api.md §17), al corte (<c>asOf</c>, hoy
/// por defecto): por convenio, los créditos vivos con vínculo vigente, lo exigible del período (las líneas del archivo vigente del mes), lo descontado
/// pendiente de giro, los días de mora de la pagaduría y si superó el umbral sellado. Sin datos del deudor (agregada); el alcance va en la consulta
/// de créditos (<see cref="AmbitoDeInformeDeCartera"/>). (nuevo)
/// </summary>
public sealed record PayrollAgreementsReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class PayrollAgreementsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<PayrollAgreementsReportQuery, Result<TablaExportable>>
{
    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = new("payroll-agreements", "Pagadurías",
        "Por convenio: créditos, exigible del período, lo descontado pendiente de giro y la mora de la pagaduría.", "pagadurias", ["asOf", "branch", "line"],
        AvailableFrom: EntregaDeCartera.K2);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Pagaduría"),
        new("Créditos", TipoDeColumna.Entero),
        new("Exigible del período", TipoDeColumna.Moneda),
        new("Descontado pendiente de giro", TipoDeColumna.Moneda),
        new("Días de mora de la pagaduría", TipoDeColumna.Entero),
        new("Umbral superado"),
    ];

    public async Task<Result<TablaExportable>> Handle(PayrollAgreementsReportQuery request, CancellationToken ct)
    {
        var corte = request.Filtros.ALaFecha(reloj.HoyLocal);
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, request.Filtros, ct);
        var creditos = ambito.Creditos(db).Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff).Select(l => l.Id);
        var convenios = await db.PayrollAgreements.AsNoTracking().Where(a => !a.IsDeleted && !a.IsOwnPayroll).OrderBy(a => a.Code).ToListAsync(ct);
        var ids = convenios.Select(a => a.Id).ToList();
        var pagadurias = await ReferenciasDeGobierno.PersonasAsync(db, convenios.Select(a => a.PaymasterPersonId), ct);

        var vinculados = (await db.LoanPayrollLinks.AsNoTracking()
                .Where(v => v.PayrollAgreementId != null && ids.Contains(v.PayrollAgreementId.Value) && !v.IsDeleted && v.ValidFrom <= corte
                            && (v.ValidTo == null || v.ValidTo >= corte) && creditos.Contains(v.LoanId))
                .Select(v => new { Convenio = v.PayrollAgreementId!.Value, v.LoanId }).ToListAsync(ct))
            .GroupBy(v => v.Convenio).ToDictionary(g => g.Key, g => g.Select(v => v.LoanId).Distinct().Count());
        var anio = (short)corte.Year;
        var mes = (byte)corte.Month;
        var exigible = (await (from l in db.PayrollDeductionFileLines.AsNoTracking()
                               join f in db.PayrollDeductionFiles.AsNoTracking() on l.PayrollDeductionFileId equals f.Id
                               where ids.Contains(f.PayrollAgreementId) && f.PeriodYear == anio && f.PeriodMonth == mes && !f.IsDeleted
                                     && (f.Status == PayrollFileStatus.Generated || f.Status == PayrollFileStatus.Sent) && creditos.Contains(l.LoanId)
                               select new { f.PayrollAgreementId, l.TotalAmount }).ToListAsync(ct))
            .GroupBy(x => x.PayrollAgreementId).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));
        var pendientes = (await (from r in PendientesDeGiro.Consulta(db).AsNoTracking()
                                 join f in db.PayrollDeductionFiles.AsNoTracking() on r.PayrollDeductionFileId equals f.Id
                                 where ids.Contains(f.PayrollAgreementId) && creditos.Contains(r.LoanId)
                                 select new { f.PayrollAgreementId, r.DeductedAmount, r.TransferDueDate, r.PaymasterArrearsThresholdDays }).ToListAsync(ct))
            .ToLookup(x => x.PayrollAgreementId);

        var filas = convenios.Select(a =>
        {
            var suyos = pendientes[a.Id].ToList();
            var dias = suyos.Count == 0 ? 0 : suyos.Max(x => PendientesDeGiro.DiasDeMora(x.TransferDueDate, corte));
            var supera = suyos.Any(x => PendientesDeGiro.SuperaElUmbral(x.TransferDueDate, x.PaymasterArrearsThresholdDays, corte));
            return new FilaExportable(
            [
                $"{a.Code} · {pagadurias.GetValueOrDefault(a.PaymasterPersonId)?.FullName ?? a.Description}", vinculados.GetValueOrDefault(a.Id),
                exigible.GetValueOrDefault(a.Id), suyos.Sum(x => x.DeductedAmount), dias, supera ? "Sí" : "No",
            ]);
        }).ToList();
        var notas = new List<string>
        {
            "Exigible del período: las líneas del archivo vigente (generado o enviado) del mes del corte. Los días de mora de la pagaduría cuentan desde el vencimiento de su ventana de giro.",
        };
        return Result.Success(new TablaExportable("Pagadurías", $"Al {corte:yyyy-MM-dd} · {ambito.Descripcion()}", Columnas, filas, null, notas));
    }
}

/// <summary>
/// La vista <c>payroll-deduction-reconciliation</c> (feature 013, K2, T0846; FR-115; api.md §17): el cuadre de un convenio y período —<c>agreement</c>
/// (su PublicId), <c>year</c> y <c>month</c>, obligatorios—, línea por línea del archivo enviado (o del vigente si todavía no se envió): enviado,
/// descontado, pendiente de giro, aplicado por el contrato, no aplicado y su motivo. Trae datos del deudor (PD, C84); sólo créditos del alcance.
/// Columna oculta <c>_credito</c>. (nuevo)
/// </summary>
public sealed record PayrollDeductionReconciliationReportQuery(FiltrosDeInformeDeCartera Filtros, Guid? Agreement = null, int? Year = null, int? Month = null)
    : IRequest<Result<TablaExportable>>;

public sealed class PayrollDeductionReconciliationReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<PayrollDeductionReconciliationReportQuery, Result<TablaExportable>>
{
    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor("payroll-deduction-reconciliation",
        "Cuadre de descuentos de pagaduría", "Lo enviado a la pagaduría contra lo descontado, girado y no aplicado, línea por línea.",
        "cuadre-de-descuentos", ["branch", "line"], ["agreement", "year", "month"], EntregaDeCartera.K2);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Empleado"),
        new("Crédito"),
        new("Enviado", TipoDeColumna.Moneda),
        new("Descontado", TipoDeColumna.Moneda),
        new("Pendiente de giro", TipoDeColumna.Moneda),
        new("Aplicado", TipoDeColumna.Moneda),
        new("No aplicado", TipoDeColumna.Moneda),
        new("Motivo"),
        new("_credito", Clave: "_credito"),
    ];

    public async Task<Result<TablaExportable>> Handle(PayrollDeductionReconciliationReportQuery request, CancellationToken ct)
    {
        if (request.Agreement is not { } publico)
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroObligatorio("agreement", "el convenio de la pagaduría (su PublicId)."));
        if (request.Year is not { } anio || request.Month is not { } mes || mes is < 1 or > 12)
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroObligatorio("year", "el año y el mes (1 a 12) del período."));
        var convenio = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == publico && !a.IsDeleted, ct);
        if (convenio is null) return Result.Failure<TablaExportable>(ErroresDeLibranza.ConvenioInexistente());
        var archivo = (await db.PayrollDeductionFiles.AsNoTracking()
                .Where(f => f.PayrollAgreementId == convenio.Id && f.PeriodYear == anio && f.PeriodMonth == mes && !f.IsDeleted
                            && (f.Status == PayrollFileStatus.Sent || f.Status == PayrollFileStatus.Generated))
                .ToListAsync(ct))
            .OrderByDescending(f => f.Status == PayrollFileStatus.Sent).ThenByDescending(f => f.Version).FirstOrDefault();
        var titulo = $"Cuadre de descuentos · {convenio.Code} {anio}-{mes:00}";
        if (archivo is null) return Result.Success(new TablaExportable(titulo, "Sin archivo generado ni enviado del período.", Columnas, [], null, []));

        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, request.Filtros, ct);
        var creditos = ambito.Creditos(db).Select(l => l.Id);
        var lineas = await db.PayrollDeductionFileLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == archivo.Id && creditos.Contains(l.LoanId))
            .OrderBy(l => l.LineNumber).ToListAsync(ct);
        var idsDeLinea = lineas.Select(l => l.Id).ToList();
        var resultados = await db.PayrollDeductionResults.AsNoTracking().Where(r => idsDeLinea.Contains(r.PayrollDeductionFileLineId) && !r.IsDeleted).ToListAsync(ct);
        var reemplazados = resultados.Where(r => r.SupersedesResultId is not null).Select(r => r.SupersedesResultId!.Value).ToHashSet();
        var vigentes = resultados.Where(r => !reemplazados.Contains(r.Id)).GroupBy(r => r.PayrollDeductionFileLineId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());
        var idsDeResultado = resultados.Select(r => r.Id).ToList();
        var aplicados = (await db.LoanOperations.AsNoTracking()
                .Where(o => o.PayrollDeductionResultId != null && idsDeResultado.Contains(o.PayrollDeductionResultId.Value) && o.ReversedByOperationId == null)
                .Select(o => new { Resultado = o.PayrollDeductionResultId!.Value, o.Amount, o.ExcessAmount }).ToListAsync(ct))
            .GroupBy(o => o.Resultado).ToDictionary(g => g.Key, g => g.Sum(o => o.Amount - (o.ExcessAmount ?? 0m)));
        var deCredito = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => lineas.Select(x => x.LoanId).Contains(l.Id)).ToListAsync(ct);
        var (personas, _, _) = await ListLoansQueryHandler.ReferenciasAsync(db, deCredito, ct);
        var porId = deCredito.ToDictionary(l => l.Id);

        var filas = lineas.Select(l =>
        {
            var credito = porId[l.LoanId];
            vigentes.TryGetValue(l.Id, out var r);
            var aplicado = r is null ? 0m : aplicados.GetValueOrDefault(r.Id);
            var pendiente = r is { DejaPendienteDeGiro: true } && aplicado == 0m ? r.DeductedAmount : 0m;
            var motivo = r?.NonDeductionReason is { } m ? Payroll.Results.SeguimientoDeNoAplicados.Motivo(m) : null;
            return new FilaExportable(
            [
                $"{l.EmployeeIdAtPaymaster ?? "—"} · {personas[credito.DebtorPersonId].Name}", credito.Number, l.TotalAmount, r?.DeductedAmount ?? 0m, pendiente, aplicado,
                r?.NotDeductedAmount ?? 0m, motivo, credito.PublicId.ToString(),
            ]);
        }).ToList();
        var totales = new FilaExportable(["Total", null, filas.Sum(f => (decimal)f.Valores[2]!), filas.Sum(f => (decimal)f.Valores[3]!),
            filas.Sum(f => (decimal)f.Valores[4]!), filas.Sum(f => (decimal)f.Valores[5]!), filas.Sum(f => (decimal)f.Valores[6]!), null, null]);
        var notas = new List<string>
        {
            $"Archivo {archivo.Number} (versión {archivo.Version}, {(archivo.Status == PayrollFileStatus.Sent ? "enviado" : "generado sin enviar")}). Aplicado: lo que Recaudo giró por el contrato citando la línea.",
        };
        return Result.Success(new TablaExportable(titulo, $"{ambito.Descripcion()}", Columnas, filas, totales, notas));
    }
}
