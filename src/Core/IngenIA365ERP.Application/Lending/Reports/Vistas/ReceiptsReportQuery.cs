using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>receipts</c> de <c>/api/reports/lending</c> (feature 013, US15, K1, T0797; FR-133; contracts/api.md §17; F-15 la llamó
/// <c>collections</c>, CH10): el recaudo del rango —los pagos con fecha valor entre <c>from</c> y <c>to</c>— abierto por
/// <c>groupBy=line|collector|modality|meansClass</c> (línea por defecto), con los pagos, lo aplicado a capital y a intereses (corrientes y de mora,
/// también los de suspenso) según la distribución del libro (<c>LND_LoanTransactions</c> de la operación), los otros conceptos (seguros, cargos,
/// anticipos, saldos a favor) y el total recibido; los reversos van <b>aparte</b>, en su columna, por la fecha valor del reverso: el pago reversado
/// sigue contando en las demás. Con <c>collector</c> sólo los de ese recaudador. Agregada: sin datos del deudor. El alcance va en la consulta de
/// créditos (<see cref="AmbitoDeInformeDeCartera"/>, <see cref="IAlcanceDeCartera"/>). (nuevo)
/// </summary>
public sealed record ReceiptsReportQuery(FiltrosDeInformeDeCartera Filtros, string? GroupBy = null) : IRequest<Result<TablaExportable>>;

public sealed class ReceiptsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ReceiptsReportQuery, Result<TablaExportable>>
{
    public const string PorLinea = "line";
    public const string PorRecaudador = "collector";
    public const string PorModalidad = "modality";
    public const string PorClaseDeMedio = "meansClass";

    public static readonly IReadOnlyList<string> Dimensiones = [PorLinea, PorRecaudador, PorModalidad, PorClaseDeMedio];

    public const string SinRecaudador = "Sin recaudador";
    public const string SinClaseDeMedio = "Sin clase de medio";

    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = new(
        "receipts", "Recaudo", "Pagos del rango por línea, recaudador, modalidad o clase de medio: capital, intereses, otros y total, con los reversos aparte.",
        "recaudo", ["from", "to", "branch", "line", "modality", "subsegment", "advisor", "collector"], ["groupBy"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Dimensión"),
        new("Pagos", TipoDeColumna.Entero),
        new("Capital", TipoDeColumna.Moneda),
        new("Intereses", TipoDeColumna.Moneda),
        new("Otros", TipoDeColumna.Moneda),
        new("Total", TipoDeColumna.Moneda),
        new("Reversos", TipoDeColumna.Moneda),
    ];

    private static readonly LoanConcept[] DeIntereses =
        [LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense];

    private sealed record Movimiento(long OperacionId, int LoanId, int CreditLineId, LoanOperationKind Clase, decimal Monto, string? Recaudador,
        PaymentModality? Modalidad, string? ClaseDeMedio, long? Original);

    public async Task<Result<TablaExportable>> Handle(ReceiptsReportQuery request, CancellationToken ct)
    {
        var dimension = FiltrosDeInformeDeCartera.Elegir("groupBy", request.GroupBy, Dimensiones, PorLinea);
        if (dimension.IsFailure) return Result.Failure<TablaExportable>(dimension.Error);

        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditos = ambito.Creditos(db);
        var recaudador = string.IsNullOrWhiteSpace(f.Collector) ? null : f.Collector.Trim();
        var movimientos = await (from o in db.LoanOperations.AsNoTracking()
                                 join l in creditos on o.LoanId equals l.Id
                                 where (o.Kind == LoanOperationKind.Payment || o.Kind == LoanOperationKind.PaymentReversal)
                                       && o.ValueDate >= desde && o.ValueDate <= hasta
                                 select new Movimiento(o.Id, o.LoanId, l.CreditLineId, o.Kind, o.Amount, o.CollectorCode, o.PaymentModality,
                                     o.PaymentMeansClass, o.ReversesOperationId))
            .ToListAsync(ct);

        // El reverso no trae el recaudador ni la modalidad: los toma de su pago (que puede ser de antes del rango).
        var originales = movimientos.Where(m => m.Clase == LoanOperationKind.PaymentReversal && m.Original is not null).Select(m => m.Original!.Value).ToList();
        var deOriginales = originales.Count == 0
            ? new Dictionary<long, (string? Recaudador, PaymentModality? Modalidad, string? Medio, decimal Monto)>()
            : (await db.LoanOperations.AsNoTracking().Where(o => originales.Contains(o.Id))
                    .Select(o => new { o.Id, o.CollectorCode, o.PaymentModality, o.PaymentMeansClass, o.Amount }).ToListAsync(ct))
                .ToDictionary(o => o.Id, o => (Recaudador: o.CollectorCode, Modalidad: o.PaymentModality, Medio: o.PaymentMeansClass, Monto: o.Amount));
        movimientos = movimientos.Select(m => m.Clase == LoanOperationKind.PaymentReversal && m.Original is { } id && deOriginales.TryGetValue(id, out var o)
                ? m with { Recaudador = m.Recaudador ?? o.Recaudador, Modalidad = m.Modalidad ?? o.Modalidad, ClaseDeMedio = m.ClaseDeMedio ?? o.Medio,
                    Monto = m.Monto != 0m ? Math.Abs(m.Monto) : o.Monto }
                : m)
            .Where(m => recaudador is null || string.Equals(m.Recaudador, recaudador, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (ambito.Modalidad is not null || ambito.Subsegmento is not null)
        {
            var condiciones = await AmbitoDeInformeDeCartera.CondicionesVigentesAsync(db, movimientos.Select(m => m.LoanId).Distinct().ToList(), ct);
            movimientos = movimientos.Where(m => condiciones.TryGetValue(m.LoanId, out var c) && ambito.DeLaModalidad(c.ModalityCode, c.SubsegmentCode)).ToList();
        }

        var pagos = movimientos.Where(m => m.Clase == LoanOperationKind.Payment).Select(m => m.OperacionId).ToList();
        var distribucion = pagos.Count == 0
            ? new Dictionary<long, (decimal Capital, decimal Intereses)>()
            : (await db.LoanTransactions.AsNoTracking()
                    .Where(t => t.OperationId != null && pagos.Contains(t.OperationId.Value) && t.ReversesTransactionId == null
                                && (t.Kind == LoanTransactionKind.PaymentAllocation || t.Kind == LoanTransactionKind.Recovery))
                    .Select(t => new { OperationId = t.OperationId!.Value, t.Concept, t.Amount }).ToListAsync(ct))
                .GroupBy(t => t.OperationId)
                .ToDictionary(g => g.Key, g => (-g.Where(t => t.Concept == LoanConcept.Principal).Sum(t => t.Amount),
                    -g.Where(t => DeIntereses.Contains(t.Concept)).Sum(t => t.Amount)));

        var nombre = await NombresAsync(dimension.Value, movimientos, ct);
        var filas = movimientos.GroupBy(nombre)
            .Select(g => Fila(g.Key, g.ToList(), distribucion))
            .OrderBy(r => (string)r.Valores[0]!, StringComparer.CurrentCulture)
            .ToList();
        var totales = Fila("Total", movimientos, distribucion);
        return Result.Success(new TablaExportable("Recaudo", $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd} · por {Nombre(dimension.Value)} · {ambito.Descripcion()}",
            Columnas, filas, totales,
        [
            "Pagos con fecha valor en el rango. Capital e intereses (corrientes y de mora, también los de suspenso) según la distribución del libro; otros = total − capital − intereses (seguros, cargos, anticipos, saldos a favor).",
            "Reversos: lo reversado con fecha valor del reverso en el rango; el pago reversado sigue contando en las demás columnas.",
        ]));
    }

    private static FilaExportable Fila(string dimension, IReadOnlyList<Movimiento> movimientos, IReadOnlyDictionary<long, (decimal Capital, decimal Intereses)> distribucion)
    {
        var pagos = movimientos.Where(m => m.Clase == LoanOperationKind.Payment).ToList();
        var capital = pagos.Sum(p => distribucion.TryGetValue(p.OperacionId, out var d) ? d.Capital : 0m);
        var intereses = pagos.Sum(p => distribucion.TryGetValue(p.OperacionId, out var d) ? d.Intereses : 0m);
        var total = pagos.Sum(p => p.Monto);
        var reversos = movimientos.Where(m => m.Clase == LoanOperationKind.PaymentReversal).Sum(m => Math.Abs(m.Monto));
        return new FilaExportable([dimension, pagos.Count, capital, intereses, total - capital - intereses, total, reversos]);
    }

    private static string Nombre(string dimension) => dimension switch
    {
        PorRecaudador => "recaudador",
        PorModalidad => "modalidad de pago",
        PorClaseDeMedio => "clase de medio",
        _ => "línea",
    };

    private async Task<Func<Movimiento, string>> NombresAsync(string dimension, IReadOnlyList<Movimiento> movimientos, CancellationToken ct)
    {
        switch (dimension)
        {
            case PorRecaudador:
                return m => string.IsNullOrWhiteSpace(m.Recaudador) ? SinRecaudador : m.Recaudador!;
            case PorModalidad:
                return m => TextosDeInformesDeCartera.Modalidad(m.Modalidad);
            case PorClaseDeMedio:
                return m => string.IsNullOrWhiteSpace(m.ClaseDeMedio) ? SinClaseDeMedio : m.ClaseDeMedio!;
            default:
            {
                var ids = movimientos.Select(m => m.CreditLineId).Distinct().ToList();
                var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id))
                    .ToDictionaryAsync(l => l.Id, l => $"{l.Code} · {l.Name}", ct);
                return m => lineas.GetValueOrDefault(m.CreditLineId) ?? string.Empty;
            }
        }
    }
}
