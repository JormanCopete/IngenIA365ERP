using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>payoffs</c> («Recogidas») de <c>/api/reports/lending</c> (feature 028, HU8, T053; FR-028, R17; contracts/api.md §5): una fila por producto
/// que recogió un desembolso con fecha valor en el rango (<c>LND_DisbursementPayoffs</c>), las vivas, las reversadas (el crédito nuevo se anuló antes del
/// giro) y las omitidas (el producto ya estaba cancelado), marcadas en «Estado». Por cada una: el crédito nuevo, su solicitud, el deudor, la oficina y la
/// línea del crédito nuevo, el producto recogido, su clase y modo, lo proyectado en la solicitud, lo aplicado, la diferencia y su causa —éstas sólo si la
/// solicitud guardó proyección (E-07: una fila copiada de <c>PayoffRequest</c> lleva proyectado 0 sin huella y no tiene con qué compararse)—, la cuota
/// causada, el interés devuelto y cobrado por proporcionales (de lo de ahora: lo proyectado más la diferencia por concepto), quién radicó la solicitud
/// (el asesor), quién la decidió y quién desembolsó. Filtros: el rango (fecha valor del desembolso), la sucursal, la línea y el asesor del crédito nuevo
/// (<see cref="AmbitoDeInformeDeCartera"/>, con el alcance en la consulta) y el propio <c>productKind</c> (<c>Loan</c>, <c>Account</c>,
/// <c>CommercialReceivable</c>). Trae datos del deudor (PD). Columna oculta <c>_credito</c>, el crédito nuevo. (nuevo)
/// </summary>
public sealed record PayoffsReportQuery(FiltrosDeInformeDeCartera Filtros, string? ProductKind = null) : IRequest<Result<TablaExportable>>;

public sealed class PayoffsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<PayoffsReportQuery, Result<TablaExportable>>
{
    public const string FiltroDeClase = "productKind";

    /// <summary>Los valores de <c>productKind</c>: los nombres de <see cref="PayoffProductKind"/>.</summary>
    public static readonly IReadOnlyList<string> Clases = Enum.GetNames<PayoffProductKind>();

    public const string Aplicada = "Aplicada";
    public const string Reversada = "Reversada al anular el crédito nuevo";
    public const string Omitida = "Omitida: ya estaba cancelado";

    /// <summary>Lo que la vista declara al publicarse.</summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "payoffs", "Recogidas",
        "Los productos que recogieron los desembolsos del rango: lo proyectado, lo aplicado, la diferencia y su causa, la cuota causada, los intereses "
        + "devueltos o cobrados, y quién vendió, decidió y desembolsó.",
        "recogidas", ["from", "to", "branch", "line", "advisor"], [FiltroDeClase]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha),
        new("Crédito nuevo"),
        new("Solicitud"),
        new("Deudor"),
        new("Oficina"),
        new("Línea"),
        new("Producto"),
        new("Tipo"),
        new("Modo"),
        new("Proyectado", TipoDeColumna.Moneda),
        new("Aplicado", TipoDeColumna.Moneda),
        new("Diferencia", TipoDeColumna.Moneda),
        new("Causa"),
        new("Cuota causada"),
        new("Interés devuelto", TipoDeColumna.Moneda),
        new("Interés cobrado", TipoDeColumna.Moneda),
        new("Asesor"),
        new("Decisor"),
        new("Desembolsó"),
        new("Estado"),
        new("_credito", Clave: "_credito"),
    ];

    public async Task<Result<TablaExportable>> Handle(PayoffsReportQuery request, CancellationToken ct)
    {
        PayoffProductKind? clase = null;
        if (!string.IsNullOrWhiteSpace(request.ProductKind))
        {
            var elegida = FiltrosDeInformeDeCartera.Elegir(FiltroDeClase, request.ProductKind, Clases, Clases[0]);
            if (elegida.IsFailure) return Result.Failure<TablaExportable>(elegida.Error);
            clase = Enum.Parse<PayoffProductKind>(elegida.Value);
        }

        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditos = ambito.Creditos(db);
        var consulta = from r in db.DisbursementPayoffs.AsNoTracking()
                       join o in db.LoanOperations.AsNoTracking() on r.DisbursementOperationId equals o.Id
                       join l in creditos on o.LoanId equals l.Id
                       where !r.IsDeleted && o.ValueDate >= desde && o.ValueDate <= hasta
                       select new { Fila = r, o.LoanId, o.ValueDate, o.ActorUserId };
        if (clase is { } c) consulta = consulta.Where(x => x.Fila.ProductKind == c);
        var leidas = await consulta.OrderBy(x => x.ValueDate).ThenBy(x => x.Fila.DisbursementOperationId).ThenBy(x => x.Fila.Id).ToListAsync(ct);

        var nuevoIds = leidas.Select(x => x.LoanId).Distinct().ToList();
        var nuevos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => nuevoIds.Contains(l.Id)).ToListAsync(ct);
        var porCredito = nuevos.ToDictionary(l => l.Id);
        var (personas, lineas, oficinas) = await ListLoansQueryHandler.ReferenciasAsync(db, nuevos, ct);

        // La solicitud del crédito nuevo: su número, quién la radicó (el asesor) y quién la decidió.
        var solicitudIds = nuevos.Where(l => l.ApplicationId is not null).Select(l => l.ApplicationId!.Value).Distinct().ToList();
        var solicitudes = await db.CreditApplications.AsNoTracking().IgnoreQueryFilters().Where(a => solicitudIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Number, Asesor = a.SubmittedByUserId ?? a.CreatedByUserId, a.ApprovalDecisionId })
            .ToDictionaryAsync(a => a.Id, ct);
        var decisionIds = solicitudes.Values.Where(a => a.ApprovalDecisionId is not null).Select(a => a.ApprovalDecisionId!.Value).Distinct().ToList();
        var decisiones = await db.CreditDecisions.AsNoTracking().IgnoreQueryFilters().Where(d => decisionIds.Contains(d.Id))
            .Select(d => new { d.Id, d.DecidedByUserId, d.DecidedByPersonId }).ToDictionaryAsync(d => d.Id, ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db,
            solicitudes.Values.Select(a => a.Asesor).Concat(decisiones.Values.Select(d => d.DecidedByUserId)).Concat(leidas.Select(x => x.ActorUserId)), ct);
        var decisoresPersona = decisiones.Values.Where(d => d.DecidedByUserId is null && d.DecidedByPersonId is not null)
            .Select(d => d.DecidedByPersonId!.Value).Distinct().ToList();
        var personasQueDecidieron = (await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => decisoresPersona.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id, ReferenciasDeGobierno.Nombre);

        // Lo proyectado por concepto (para el interés de lo de ahora) y si la solicitud guardó proyección.
        var proyectadaIds = leidas.Where(x => x.Fila.CreditApplicationPayoffId is not null).Select(x => x.Fila.CreditApplicationPayoffId!.Value).Distinct().ToList();
        var proyectadas = await db.CreditApplicationPayoffs.AsNoTracking().IgnoreQueryFilters().Where(p => proyectadaIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ProjectedBreakdown, p.ProjectionSha256 }).ToDictionaryAsync(p => p.Id, ct);

        string Usuario(int? id) => ReferenciasDeGobierno.Usuario(usuarios, id)?.Name ?? string.Empty;
        string Decisor(int? decision)
        {
            if (decision is not { } d || !decisiones.TryGetValue(d, out var leida)) return string.Empty;
            if (leida.DecidedByUserId is not null) return Usuario(leida.DecidedByUserId);
            return leida.DecidedByPersonId is { } p && personasQueDecidieron.TryGetValue(p, out var nombre) ? nombre : string.Empty;
        }

        var filas = new List<FilaExportable>();
        decimal proyectado = 0m, aplicado = 0m, diferencia = 0m;
        foreach (var x in leidas)
        {
            var r = x.Fila;
            var nuevo = porCredito[x.LoanId];
            var solicitud = nuevo.ApplicationId is { } s ? solicitudes.GetValueOrDefault(s) : null;
            var proyectada = r.CreditApplicationPayoffId is { } p ? proyectadas.GetValueOrDefault(p) : null;
            // E-07: sin la fila proyectada, «con proyección» es tener algo proyectado o una causa.
            var conProyeccion = proyectada is not null
                ? !string.IsNullOrEmpty(proyectada.ProjectionSha256)
                : r.ProjectedAmount > 0m || r.DifferenceCause != PayoffDifferenceCause.None;
            // Lo de ahora por concepto = lo proyectado más la diferencia; sin proyección no se sabe (sólo el total).
            var ahora = conProyeccion && proyectada is not null && !r.Omitted
                ? Sumar(DesgloseDeRecogida.Leer(proyectada.ProjectedBreakdown), DesgloseDeRecogida.Leer(r.DifferenceDetail))
                : null;
            var estado = r.Omitted ? Omitida : r.EstaReversada ? Reversada : Aplicada;
            if (!r.EstaReversada)
            {
                aplicado += r.AppliedAmount;
                if (conProyeccion)
                {
                    proyectado += r.ProjectedAmount;
                    diferencia += r.Difference;
                }
            }
            filas.Add(new FilaExportable(
            [
                x.ValueDate, nuevo.Number, solicitud?.Number ?? string.Empty, personas.TryGetValue(nuevo.DebtorPersonId, out var deudor) ? deudor.Name : string.Empty,
                oficinas.TryGetValue(nuevo.BranchId, out var oficina) ? oficina.Name : string.Empty,
                lineas.TryGetValue(nuevo.CreditLineId, out var linea) ? $"{linea.Code} · {linea.Name}" : string.Empty,
                r.ProductNumber, ClaseEnTexto(r.ProductKind), ModoEnTexto(r.Mode),
                conProyeccion ? r.ProjectedAmount : null, r.AppliedAmount, conProyeccion ? r.Difference : null,
                conProyeccion && r.DifferenceCause != PayoffDifferenceCause.None ? Mayuscula(RequisitosDelDesembolso.TextoDeCausas(r.DifferenceCause)) : string.Empty,
                CuotaEnTexto(r.InstallmentTreatment),
                ahora?.InteresDevuelto, ahora?.InteresCobradoPorProporcionales,
                Usuario(solicitud?.Asesor), Decisor(solicitud?.ApprovalDecisionId), Usuario(x.ActorUserId), estado, nuevo.PublicId.ToString(),
            ], Resaltada: r.EstaReversada || r.Omitted));
        }
        var totales = new FilaExportable(["Total", null, null, null, null, null, null, null, null, proyectado, aplicado, diferencia, null, null, null, null, null,
            null, null, null, null]);
        var notas = new List<string>
        {
            "Una fila por producto que recogió un desembolso con fecha valor en el rango; oficina, línea y asesor son los del crédito nuevo. El asesor es quien "
            + "radicó la solicitud.",
            "Proyectado: lo que la solicitud proyectó a la fecha esperada de desembolso. Diferencia: lo de ahora (a la fecha valor) menos lo proyectado; sólo "
            + "si la solicitud guardó la proyección (las solicitudes de antes no tienen con qué compararse).",
            "Interés devuelto y cobrado: los de los intereses proporcionales de lo que se recogió (vacíos sin proyección o en un abono a capital).",
            "Reversada: el crédito nuevo se anuló antes del giro y la recogida se deshizo. Omitida: el producto ya estaba cancelado y no se recogió. Los totales "
            + "no cuentan las reversadas.",
        };
        return Result.Success(new TablaExportable("Recogidas", $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd} · {filas.Count} productos · {ambito.Descripcion()}",
            Columnas, filas, filas.Count == 0 ? null : totales, notas));
    }

    private static DesgloseDeRecogida Sumar(DesgloseDeRecogida a, DesgloseDeRecogida b) => new(
        a.Capital + b.Capital, a.InteresCorriente + b.InteresCorriente, a.InteresDevuelto + b.InteresDevuelto,
        a.InteresCobradoPorProporcionales + b.InteresCobradoPorProporcionales, a.Mora + b.Mora, a.Seguro + b.Seguro, a.Administracion + b.Administracion,
        a.OtrosCargos + b.OtrosCargos, a.Anticipo + b.Anticipo, a.SaldoAFavor + b.SaldoAFavor);

    public static string ClaseEnTexto(PayoffProductKind clase) => clase switch
    {
        PayoffProductKind.Loan => "Crédito",
        PayoffProductKind.Account => "Cuenta",
        PayoffProductKind.CommercialReceivable => "Factura a crédito",
        _ => clase.ToString(),
    };

    public static string ModoEnTexto(PayoffMode modo) => modo switch
    {
        PayoffMode.Total => "Total",
        PayoffMode.Partial => "Parcial",
        PayoffMode.Installments => "Cuotas",
        PayoffMode.Deposit => "Abono",
        _ => modo.ToString(),
    };

    public static string CuotaEnTexto(InstallmentTreatment? tratamiento) => tratamiento switch
    {
        InstallmentTreatment.Include => "Incluida",
        InstallmentTreatment.Exclude => "Dejada a su canal",
        _ => string.Empty,
    };

    private static string Mayuscula(string texto) => texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
