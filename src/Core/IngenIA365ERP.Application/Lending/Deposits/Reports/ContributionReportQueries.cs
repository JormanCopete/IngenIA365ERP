using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Reports;

// Feature 014, US3 (T088; api.md §18.1; research R24; FR-107): las vistas de aportes del centro de informes de la Cartera —los aportes por asociado y las
// cuotas en mora—, con datos del titular: consultarlas exige Lending.Accounts.Read y exportarlas Lending.Reports.ExportPersonalData. Las cuentas se
// filtran por el alcance de oficina y línea de quien pregunta. (nuevo)

/// <summary>Lo común de las vistas de captaciones y aportes. (nuevo)</summary>
public static class VistasDeCaptaciones
{
    /// <summary>El permiso que exige toda vista de captaciones con datos del titular (api.md §18.1, «PD»).</summary>
    public const string PermisoDeDatosDelTitular = "Lending.Accounts.Read";

    /// <summary>Una vista con datos del titular, desde la entrega <paramref name="desde"/>.</summary>
    public static VistaDeInformeDeCartera ConDatosDelTitular(string key, string name, string description, string fileName, IReadOnlyList<string> filters,
        IReadOnlyList<string>? ownFilters, EntregaDeCartera desde) =>
        new(key, name, description, fileName, filters, ownFilters, PersonalData: true, desde, PermisoDeDatosDelTitular);

    /// <summary>Las cuentas de la naturaleza en el alcance de quien pregunta, con los filtros comunes de oficina y línea.</summary>
    public static async Task<IQueryable<MemberAccount>> CuentasAsync(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion,
        FiltrosDeInformeDeCartera f, LineNature naturaleza, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var cuentas = db.MemberAccounts.AsNoTracking().Where(a => a.Nature == naturaleza && !a.IsDeleted)
            .PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId);
        if (f.Branch is { } oficina) cuentas = cuentas.Where(a => db.Branches.Any(b => b.Id == a.BranchId && b.PublicId == oficina));
        if (f.Line is { } linea) cuentas = cuentas.Where(a => db.CreditLines.Any(l => l.Id == a.CreditLineId && l.PublicId == linea));
        return cuentas;
    }
}

/// <summary>
/// La vista <c>contributions-by-member</c> (feature 014, US3, T088; api.md §18.1): el saldo de aportes de cada asociado al corte <c>asOf</c> por clase
/// —mínimos, ordinarios, extraordinarios, revalorizados— con su total y sus cuotas en mora; con <c>class</c>, sólo los asociados con saldo en esa clase.
/// La suma del libro con fecha valor ≤ corte: el mismo saldo de <c>IConsultaDeAportes</c>. (nuevo)
/// </summary>
public sealed record ContributionsByMemberReportQuery(FiltrosDeInformeDeCartera Filtros, string? Class = null) : IRequest<Result<TablaExportable>>;

public sealed class ContributionsByMemberReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ContributionsByMemberReportQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<string> Clases =
        [nameof(ContributionClass.Minimum), nameof(ContributionClass.Ordinary), nameof(ContributionClass.Extraordinary), nameof(ContributionClass.Revalued)];

    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "contributions-by-member", "Aportes por asociado",
        "El saldo de aportes de cada asociado al corte por clase, con su total y sus cuotas en mora.", "aportes-por-asociado",
        ["asOf", "branch", "line"], ["class"], EntregaDeCartera.K7);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Asociado"),
        new("Documento"),
        new("Mínimo", TipoDeColumna.Moneda),
        new("Ordinarios", TipoDeColumna.Moneda),
        new("Extraordinarios", TipoDeColumna.Moneda),
        new("Revalorizados", TipoDeColumna.Moneda),
        new("Total", TipoDeColumna.Moneda),
        new("Cuotas en mora", TipoDeColumna.Entero),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(ContributionsByMemberReportQuery request, CancellationToken ct)
    {
        string? clase = null;
        if (request.Class is not null)
        {
            var elegida = FiltrosDeInformeDeCartera.Elegir("class", request.Class, Clases, Clases[0]);
            if (elegida.IsFailure) return Result.Failure<TablaExportable>(elegida.Error);
            clase = elegida.Value;
        }
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var cuentas = await (await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Contribution, ct))
            .Select(a => new { a.Id, a.HolderPersonId }).ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var conceptos = MemberAccountTransaction.ConceptosDeAportes.ToArray();
        var saldos = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= corte && conceptos.Contains(t.Concept))
            .GroupBy(t => new { t.MemberAccountId, t.Concept }).Select(g => new { g.Key.MemberAccountId, g.Key.Concept, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        var enMora = (await LectorDeCuotas.DeCuentasAsync(db, ids, ct)).Where(q => q.EnMoraEl(corte)).GroupBy(q => q.Cuota.MemberAccountId)
            .ToDictionary(g => g.Key, g => g.Count());
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => cuentas.Select(c => c.HolderPersonId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var filas = cuentas.GroupBy(c => c.HolderPersonId).Select(g =>
        {
            var deEllas = g.Select(c => c.Id).ToHashSet();
            decimal De(MemberAccountConcept concepto) => saldos.Where(s => deEllas.Contains(s.MemberAccountId) && s.Concept == concepto).Sum(s => s.Total);
            var (minimo, ordinario, extra, revalorizado) = (De(MemberAccountConcept.ContributionMinimum), De(MemberAccountConcept.ContributionOrdinary),
                De(MemberAccountConcept.ContributionExtraordinary), De(MemberAccountConcept.ContributionRevaluation));
            var persona = personas[g.Key];
            return (Clase: new Dictionary<string, decimal>
                {
                    [nameof(ContributionClass.Minimum)] = minimo, [nameof(ContributionClass.Ordinary)] = ordinario,
                    [nameof(ContributionClass.Extraordinary)] = extra, [nameof(ContributionClass.Revalued)] = revalorizado,
                },
                Fila: new FilaExportable([ReferenciasDeGobierno.Nombre(persona), persona.TaxId, minimo, ordinario, extra, revalorizado,
                    minimo + ordinario + extra + revalorizado, g.Sum(c => enMora.GetValueOrDefault(c.Id)), persona.PublicId.ToString()]));
        })
            .Where(x => clase is null || x.Clase[clase] != 0m)
            .Select(x => x.Fila)
            .OrderBy(r => (string)r.Valores[0]!, StringComparer.CurrentCulture)
            .ToList();
        var totales = new FilaExportable(["Total", null, Suma(filas, 2), Suma(filas, 3), Suma(filas, 4), Suma(filas, 5), Suma(filas, 6),
            filas.Sum(r => (int)r.Valores[7]!), null]);
        return Result.Success(new TablaExportable("Aportes por asociado", $"Al {corte:yyyy-MM-dd}" + (clase is null ? string.Empty : $" · con {clase}"),
            Columnas, filas, totales,
            ["Suma del libro de cuentas con fecha valor hasta el corte; el mismo saldo de la consulta de aportes y de la constancia.",
             "Cuotas en mora: cuotas de aportes vencidas con saldo pendiente al corte (una cuota de aportes no causa intereses de mora)."]));
    }

    private static decimal Suma(IReadOnlyList<FilaExportable> filas, int columna) => filas.Sum(r => (decimal)r.Valores[columna]!);
}

/// <summary>
/// La vista <c>installments-in-arrears</c> (feature 014, US3, T088; api.md §18.1): las cuotas de aportes, ahorro permanente y contractual vencidas y con
/// saldo pendiente al corte <c>asOf</c>, con el motivo del último no descontado que informó un recaudador. Con <c>line</c>, sólo esa línea. (nuevo)
/// </summary>
public sealed record InstallmentsInArrearsReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class InstallmentsInArrearsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<InstallmentsInArrearsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "installments-in-arrears", "Cuotas de aportes en mora",
        "Las cuotas de aportes y de ahorro permanente vencidas con saldo pendiente al corte, con el motivo del no descuento.", "cuotas-en-mora",
        ["asOf", "branch", "line"], null, EntregaDeCartera.K7);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Cuenta"),
        new("Titular"),
        new("Período"),
        new("Vencimiento", TipoDeColumna.Fecha),
        new("Pendiente", TipoDeColumna.Moneda),
        new("Motivo"),
        new("_cuenta", Clave: "_cuenta"),
    ];

    public async Task<Result<TablaExportable>> Handle(InstallmentsInArrearsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var deAportes = await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Contribution, ct);
        var deAhorro = await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Deposit, ct);
        var cuentas = (await deAportes.ToListAsync(ct))
            .Concat(await deAhorro.Where(a => a.DepositClass == DepositClass.Permanent || a.DepositClass == DepositClass.Contractual).ToListAsync(ct))
            .ToDictionary(a => a.Id);
        var cuotas = (await LectorDeCuotas.DeCuentasAsync(db, cuentas.Keys.ToList(), ct)).Where(q => q.EnMoraEl(corte)).ToList();
        var personas = await LectorDeCuentas.PersonasAsync(db, cuotas.Select(q => cuentas[q.Cuota.MemberAccountId].HolderPersonId), ct);
        var filas = cuotas
            .OrderBy(q => cuentas[q.Cuota.MemberAccountId].Number, StringComparer.Ordinal).ThenBy(q => q.Cuota.DueDate)
            .Select(q =>
            {
                var cuenta = cuentas[q.Cuota.MemberAccountId];
                return new FilaExportable([cuenta.Number, personas[cuenta.HolderPersonId].FullName, q.Cuota.Periodo, q.Cuota.DueDate, q.Pendiente,
                    q.MotivoDeNoDescuento is { } m ? Motivo(m) : string.Empty, cuenta.PublicId.ToString()]);
            })
            .ToList();
        var totales = new FilaExportable(["Total", null, null, null, filas.Sum(r => (decimal)r.Valores[4]!), null, null]);
        return Result.Success(new TablaExportable("Cuotas de aportes en mora", $"Al {corte:yyyy-MM-dd}", Columnas, filas, totales,
            ["Cuotas exigibles de aportes, ahorro permanente y contractual vencidas antes del corte y con saldo pendiente.",
             "Motivo: el último no descontado que informó Nómina o la pagaduría; vacío si nadie informó."]));
    }

    /// <summary>El motivo de no descuento en palabras.</summary>
    public static string Motivo(NonDeductionReason motivo) => motivo switch
    {
        NonDeductionReason.DeductionCap => "Tope de descuentos",
        NonDeductionReason.InsufficientNet => "Neto insuficiente",
        NonDeductionReason.Termination => "Retiro del empleado",
        NonDeductionReason.Leave => "Licencia",
        NonDeductionReason.Disability => "Incapacidad",
        NonDeductionReason.DeductionPriority => "Prelación de descuentos",
        _ => "Otro",
    };
}
