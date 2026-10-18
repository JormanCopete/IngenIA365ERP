using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>restructured</c> de <c>/api/reports/lending</c> (feature 013, US11, K3, T0887; FR-090, FR-093; contracts/api.md §17): las
/// <b>reestructuraciones aplicadas</b> hasta el corte (<c>asOf</c>, hoy por defecto) —novedades con causa <c>Restructuring</c>, de cualquier mecanismo;
/// filtro propio <c>mechanism</c>—, una fila por novedad sobre el crédito que quedó (el enlazado en una novación o refinanciación): el conteo de
/// reestructuraciones que dejó, la fecha de la operación, la calificación al pedirla, las <b>cuotas consecutivas pagadas</b> del plan nuevo (de la primera
/// que venció después de la novedad, hasta la primera con saldo, por la reconstrucción al corte) y la calificación del último cierre al corte. Trae datos
/// del deudor (PD); el alcance va en la consulta de créditos (<see cref="AmbitoDeInformeDeCartera"/>). Columna oculta <c>_credito</c>. (nuevo)
/// </summary>
public sealed record RestructuredReportQuery(FiltrosDeInformeDeCartera Filtros, string? Mechanism = null) : IRequest<Result<TablaExportable>>;

public sealed class RestructuredReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    CargadorDeReconstruccion cargador,
    IDateTimeService reloj) : IRequestHandler<RestructuredReportQuery, Result<TablaExportable>>
{
    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "restructured", "Reestructurados", "Las reestructuraciones aplicadas al corte: mecanismo, conteo, calificación al reestructurar y cuotas consecutivas pagadas.",
        "reestructurados", ["asOf", "branch", "line", "modality", "subsegment"], ["mechanism"], EntregaDeCartera.K3);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Crédito"),
        new("Deudor"),
        new("Mecanismo"),
        new("Conteo", TipoDeColumna.Entero),
        new("Fecha", TipoDeColumna.Fecha),
        new("Calificación al reestructurar"),
        new("Cuotas consecutivas pagadas", TipoDeColumna.Entero),
        new("Calificación actual"),
        new("_credito", Clave: "_credito"),
    ];

    private static readonly LoanConcept[] Exigibles =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.Insurance, LoanConcept.CollectionFee,
        LoanConcept.OtherCharge,
    ];

    public async Task<Result<TablaExportable>> Handle(RestructuredReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        NoveltyMechanism? mecanismo = null;
        if (!string.IsNullOrWhiteSpace(request.Mechanism))
        {
            if (!Enum.TryParse<NoveltyMechanism>(request.Mechanism.Trim(), ignoreCase: true, out var m) || !Enum.IsDefined(m))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("mechanism", request.Mechanism,
                    Enum.GetNames<NoveltyMechanism>()));
            mecanismo = m;
        }

        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditosDelAlcance = ambito.Creditos(db).Select(l => l.Id);
        var consulta = from n in db.LoanNovelties.AsNoTracking()
                       join o in db.LoanOperations.AsNoTracking() on n.AppliedOperationId equals o.Id
                       where !n.IsDeleted && n.Status == LoanNoveltyStatus.Applied && n.Cause == NoveltyCause.Restructuring && o.ValueDate <= corte
                             && creditosDelAlcance.Contains(n.ResultingLoanId ?? n.LoanId)
                       select new { n.Number, n.Mechanism, n.RatingAtRequest, n.RestructuringCountAfter, LoanId = n.ResultingLoanId ?? n.LoanId, o.ValueDate };
        if (mecanismo is { } filtro) consulta = consulta.Where(x => x.Mechanism == filtro);
        var novedades = await consulta.ToListAsync(ct);

        var ids = novedades.Select(n => n.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        var posiciones = await AmbitoDeInformeDeCartera.PosicionesAlCorteAsync(db, ids, corte, ct);
        var deLaModalidad = creditos.Where(c => ambito.Modalidad is null && ambito.Subsegmento is null
                                                || (posiciones.TryGetValue(c.Id, out var p) && ambito.DeLaModalidad(p.ModalityCode, p.SubsegmentCode)))
            .ToDictionary(c => c.Id);
        var (personas, _, _) = await ListLoansQueryHandler.ReferenciasAsync(db, deLaModalidad.Values.ToList(), ct);
        var categorias = await new CategoriaContableDeCartera(db).AlCorteAsync(deLaModalidad.Keys.ToList(), corte, ct);

        var filas = new List<FilaExportable>();
        foreach (var n in novedades.Where(n => deLaModalidad.ContainsKey(n.LoanId)).OrderBy(n => deLaModalidad[n.LoanId].Number, StringComparer.Ordinal)
                     .ThenBy(n => n.ValueDate))
        {
            var credito = deLaModalidad[n.LoanId];
            var categoria = categorias.GetValueOrDefault(credito.Id);
            filas.Add(new FilaExportable(
            [
                credito.Number, personas[credito.DebtorPersonId].Name, TextosDeMecanismo(n.Mechanism), n.RestructuringCountAfter, n.ValueDate,
                n.RatingAtRequest.ToString(), await CuotasConsecutivasPagadasAsync(credito.Id, n.ValueDate, corte, ct),
                categoria is null || categoria.MonthlyClosePublicId is null ? "Sin cierre" : categoria.Category, credito.PublicId.ToString(),
            ]));
        }
        var notas = new List<string>
        {
            "Reestructuraciones aplicadas (novedades con causa «reestructuración», de cualquier mecanismo) hasta el corte, sobre el crédito que quedó.",
            "Cuotas consecutivas pagadas: del plan nuevo, desde la primera que venció después de la novedad hasta la primera con saldo al corte. Calificación actual: la del último cierre al corte.",
        };
        return Result.Success(new TablaExportable("Reestructurados", $"Al {corte:yyyy-MM-dd} · {ambito.Descripcion()}", Columnas, filas, null, notas));
    }

    /// <summary>Las cuotas que vencieron después de la novedad y hasta el corte, pagadas sin interrupción desde la primera.</summary>
    private async Task<int?> CuotasConsecutivasPagadasAsync(int loanId, DateOnly desde, DateOnly corte, CancellationToken ct)
    {
        var cargado = await cargador.CargarAsync(loanId, null, corte, ct);
        if (cargado is not { Listo: true }) return null;
        EstadoDelCredito estado;
        try
        {
            estado = MotorDeCartera.Reconstruir(cargado.Entrada!, corte);
        }
        catch (ReglaDeCarteraException)
        {
            return null;
        }
        var pagadas = 0;
        foreach (var cuota in estado.Cuotas.Where(c => c.Vencimiento > desde && c.Vencimiento <= corte).OrderBy(c => c.Numero))
        {
            var pendiente = estado.Saldos.Where(s => s.Cuota == cuota.Numero && Exigibles.Contains(s.Concepto)).Sum(s => s.Valor);
            if (pendiente > 0m) break;
            pagadas++;
        }
        return pagadas;
    }

    private static string TextosDeMecanismo(NoveltyMechanism m) => m switch
    {
        NoveltyMechanism.Extension => "Prórroga",
        NoveltyMechanism.PaymentDateChange => "Cambio de fecha de pago",
        NoveltyMechanism.RateChange => "Cambio de tasa",
        NoveltyMechanism.Novation => "Novación",
        NoveltyMechanism.Refinancing => "Refinanciación",
        NoveltyMechanism.BranchChange => "Cambio de oficina",
        _ => m.ToString(),
    };
}
