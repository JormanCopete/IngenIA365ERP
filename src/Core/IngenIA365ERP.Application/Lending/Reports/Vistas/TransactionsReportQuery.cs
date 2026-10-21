using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>transactions</c> de <c>/api/reports/lending</c> (feature 013, US15, K1, T0797; FR-133; contracts/api.md §17): las filas del libro
/// de Cartera (<c>LND_LoanTransactions</c>) del rango por la fecha que pida <c>dateKind=value|accounting|registered</c> —fecha valor (por defecto),
/// contable o de registro (hora de Colombia)—, de un crédito (<c>loan</c>, su PublicId) o de todos los del alcance, y de una clase
/// (<c>kind</c>, por nombre o número de <see cref="LoanTransactionKind"/>). Cada fila con su operación y el mensaje que la llevó a otro módulo.
/// Trae datos del deudor (PD). El alcance va en la consulta de créditos (<see cref="AmbitoDeInformeDeCartera"/>, <see cref="IAlcanceDeCartera"/>).
/// Columnas ocultas <c>_credito</c>, <c>_operacion</c> y <c>_mensaje</c>. (nuevo)
/// </summary>
public sealed record TransactionsReportQuery(FiltrosDeInformeDeCartera Filtros, Guid? Loan = null, string? Kind = null, string? DateKind = null)
    : IRequest<Result<TablaExportable>>;

public sealed class TransactionsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<TransactionsReportQuery, Result<TablaExportable>>
{
    public const string PorFechaValor = "value";
    public const string PorFechaContable = "accounting";
    public const string PorRegistro = "registered";

    public static readonly IReadOnlyList<string> ClasesDeFecha = [PorFechaValor, PorFechaContable, PorRegistro];

    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "transactions", "Movimientos de un crédito",
        "Las filas del libro de Cartera del rango, por fecha valor, contable o de registro, con su operación y su mensaje.",
        "movimientos-de-cartera", ["from", "to", "branch", "line", "modality", "subsegment", "advisor"], ["loan", "kind", "dateKind"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha valor", TipoDeColumna.Fecha),
        new("Fecha contable", TipoDeColumna.Fecha),
        new("Registro"),
        new("Crédito"),
        new("Tipo"),
        new("Concepto"),
        new("Cuota", TipoDeColumna.Entero),
        new("Valor", TipoDeColumna.Moneda),
        new("Operación"),
        new("Mensaje"),
        new("_credito", Clave: "_credito"),
        new("_operacion", Clave: "_operacion"),
        new("_mensaje", Clave: "_mensaje"),
    ];

    public async Task<Result<TablaExportable>> Handle(TransactionsReportQuery request, CancellationToken ct)
    {
        var clase = FiltrosDeInformeDeCartera.Elegir("dateKind", request.DateKind, ClasesDeFecha, PorFechaValor);
        if (clase.IsFailure) return Result.Failure<TablaExportable>(clase.Error);
        LoanTransactionKind? tipo = null;
        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            if (!Enum.TryParse<LoanTransactionKind>(request.Kind.Trim(), ignoreCase: true, out var t) || !Enum.IsDefined(t))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("kind", request.Kind, Enum.GetNames<LoanTransactionKind>()));
            tipo = t;
        }

        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditos = ambito.Creditos(db);
        if (request.Loan is { } credito) creditos = creditos.Where(l => l.PublicId == credito);

        var filasDelLibro = from t in db.LoanTransactions.AsNoTracking()
                            join l in creditos on t.LoanId equals l.Id
                            select new { Fila = t, l.Number, LoanPublicId = l.PublicId };
        if (tipo is { } k) filasDelLibro = filasDelLibro.Where(x => x.Fila.Kind == k);
        switch (clase.Value)
        {
            case PorFechaContable:
                filasDelLibro = filasDelLibro.Where(x => x.Fila.AccountingDate >= desde && x.Fila.AccountingDate <= hasta);
                break;
            case PorRegistro:
            {
                var desdeUtc = DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue) - Colombia, DateTimeKind.Utc);
                var hastaUtc = DateTime.SpecifyKind(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue) - Colombia, DateTimeKind.Utc);
                filasDelLibro = filasDelLibro.Where(x => x.Fila.RegisteredAt >= desdeUtc && x.Fila.RegisteredAt < hastaUtc);
                break;
            }
            default:
                filasDelLibro = filasDelLibro.Where(x => x.Fila.ValueDate >= desde && x.Fila.ValueDate <= hasta);
                break;
        }
        var leidas = await filasDelLibro.OrderBy(x => x.Fila.ValueDate).ThenBy(x => x.Fila.Id).ToListAsync(ct);

        if (ambito.Modalidad is not null || ambito.Subsegmento is not null)
        {
            var condiciones = await AmbitoDeInformeDeCartera.CondicionesVigentesAsync(db, leidas.Select(x => x.Fila.LoanId).Distinct().ToList(), ct);
            leidas = leidas.Where(x => condiciones.TryGetValue(x.Fila.LoanId, out var c) && ambito.DeLaModalidad(c.ModalityCode, c.SubsegmentCode)).ToList();
        }

        var operaciones = leidas.Where(x => x.Fila.OperationId is not null).Select(x => x.Fila.OperationId!.Value).Distinct().ToList();
        var deOperaciones = operaciones.Count == 0
            ? new Dictionary<long, (string Numero, Guid PublicId)>()
            : (await db.LoanOperations.AsNoTracking().Where(o => operaciones.Contains(o.Id)).Select(o => new { o.Id, o.Number, o.PublicId }).ToListAsync(ct))
                .ToDictionary(o => o.Id, o => (o.Number, o.PublicId));

        var filas = leidas.Select(x =>
        {
            var t = x.Fila;
            var operacion = t.OperationId is { } id && deOperaciones.TryGetValue(id, out var o) ? o : ((string Numero, Guid PublicId)?)null;
            var registro = new DateTimeOffset(DateTime.SpecifyKind(t.RegisteredAt, DateTimeKind.Utc)).ToOffset(Colombia);
            return new FilaExportable(
            [
                t.ValueDate, t.AccountingDate, registro.ToString("yyyy-MM-dd HH:mm"), x.Number, TextosDeInformesDeCartera.Transaccion(t.Kind),
                TextosDeInformesDeCartera.Concepto(t.Concept), t.InstallmentNumber, t.Amount, operacion?.Numero ?? string.Empty,
                t.MessagePublicId?.ToString() ?? string.Empty, x.LoanPublicId.ToString(), operacion?.PublicId.ToString(), t.MessagePublicId?.ToString(),
            ]);
        }).ToList();
        var porFecha = clase.Value switch { PorFechaContable => "fecha contable", PorRegistro => "fecha de registro", _ => "fecha valor" };
        return Result.Success(new TablaExportable("Movimientos de un crédito",
            $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd} por {porFecha} · {filas.Count} filas · {ambito.Descripcion()}", Columnas, filas, null,
        [
            "Las filas del libro de Cartera tal como se registraron: un reverso es una fila nueva con el signo contrario, nunca una corrección.",
            "Valor con el signo del libro: positivo aumenta el saldo del concepto, negativo lo disminuye. El registro va en hora de Colombia.",
        ]));
    }
}
