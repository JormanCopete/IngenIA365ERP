using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Reports;

// Feature 014, US6 (T176; api.md §18.1; research R24; FR-107, FR-127): las vistas del CDAT —vencimientos y tasas negociadas, K9— y la concentración de
// depositantes (K8). Las tres traen datos del titular (Lending.Accounts.Read para consultar, ExportPersonalData para exportar) y filtran por el alcance de
// oficina y línea de quien pregunta. (nuevo)

/// <summary>
/// La vista <c>term-deposit-maturities</c> (api.md §18.1): los CDAT cuya versión vigente vence en el rango (<c>from</c>, <c>to</c>), con el titular, el capital,
/// la tasa, el plazo de emisión (<c>issueTermBand</c> opcional) y la instrucción al vencimiento que rige. (nuevo)
/// </summary>
public sealed record TermDepositMaturitiesReportQuery(FiltrosDeInformeDeCartera Filtros, string? IssueTermBand = null) : IRequest<Result<TablaExportable>>;

public sealed class TermDepositMaturitiesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<TermDepositMaturitiesReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "term-deposit-maturities", "Vencimientos de CDAT",
        "Los CDAT que vencen en un rango, con su capital, su tasa, su plazo de emisión y la instrucción al vencimiento.", "vencimientos-de-cdat",
        ["from", "to", "branch", "line"], ["issueTermBand"], EntregaDeCartera.K9);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Vencimiento", TipoDeColumna.Fecha),
        new("CDAT"),
        new("Título"),
        new("Titular"),
        new("Capital", TipoDeColumna.Moneda),
        new("Tasa EA", TipoDeColumna.Porcentaje),
        new("Plazo de emisión"),
        new("Instrucción"),
        new("_cuenta", Clave: "_cuenta"),
    ];

    public async Task<Result<TablaExportable>> Handle(TermDepositMaturitiesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        IssueTermBand? plazo = null;
        if (!string.IsNullOrWhiteSpace(request.IssueTermBand))
        {
            if (!Enum.TryParse<IssueTermBand>(request.IssueTermBand, true, out var p) || !Enum.IsDefined(p))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("issueTermBand", request.IssueTermBand, Enum.GetNames<IssueTermBand>()));
            plazo = p;
        }
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var cuentas = await (await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Deposit, ct))
            .Where(a => a.DepositClass == DepositClass.TermDeposit && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Matured))
            .Select(a => new { a.Id, a.Number, a.PublicId, a.HolderPersonId, a.MaturityInstructionJson }).ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var versiones = (await db.MemberAccountTermsVersions.AsNoTracking().Where(v => ids.Contains(v.MemberAccountId) && !v.IsDeleted).ToListAsync(ct))
            .GroupBy(v => v.MemberAccountId).Select(g => g.MaxBy(v => v.VersionNumber)!)
            .Where(v => v.MaturityDate >= desde && v.MaturityDate <= hasta && (plazo == null || v.IssueTermBand == plazo)).ToList();
        var porId = cuentas.ToDictionary(c => c.Id);
        var personas = await LectorDeCuentas.PersonasAsync(db, cuentas.Select(c => c.HolderPersonId).Distinct(), ct);
        var filas = versiones.OrderBy(v => v.MaturityDate).ThenBy(v => porId[v.MemberAccountId].Number, StringComparer.Ordinal).Select(v =>
        {
            var c = porId[v.MemberAccountId];
            var instruccion = TermDeposits.InstruccionDeCdat.Leer(c.MaturityInstructionJson)?.Instruccion ?? v.MaturityInstruction;
            return new FilaExportable([v.MaturityDate, c.Number, Domain.Entities.Lending.Deposits.Accounts.MemberAccountTermsVersion.NumeroDeTitulo(c.Number, v.VersionNumber),
                personas.GetValueOrDefault(c.HolderPersonId)?.FullName, v.PrincipalAmount, v.AppliedAnnualRate * 100m, v.IssueTermBand?.ToString(), instruccion?.ToString(),
                c.PublicId.ToString()]);
        }).ToList();
        var totales = new FilaExportable(["Total", null, null, null, filas.Sum(r => (decimal)r.Valores[4]!), null, null, null, null]);
        return Result.Success(new TablaExportable("Vencimientos de CDAT", $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd}", Columnas, filas, totales,
            ["El vencimiento que cae en un día no hábil se paga el siguiente hábil sin interés adicional (D19)."]));
    }
}

/// <summary>
/// La vista <c>negotiated-rates</c> (api.md §18.1; FR-127): los CDAT constituidos, prorrogados o renovados en el rango con tasa negociada —la tasa de la
/// tabla, el margen, la regla, quién lo aprobó— y su <b>costo frente a la tabla</b>: el interés del plan de la versión menos el que habría dado la tasa de la
/// tabla. (nuevo)
/// </summary>
public sealed record NegotiatedRatesReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class NegotiatedRatesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<NegotiatedRatesReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "negotiated-rates", "Tasas negociadas",
        "Los CDAT con tasa negociada en un rango, con la tasa de la tabla, el margen, la regla, el aprobador y su costo frente a la tabla.", "tasas-negociadas",
        ["from", "to", "branch", "line"], null, EntregaDeCartera.K9);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha),
        new("CDAT"),
        new("Titular"),
        new("Capital", TipoDeColumna.Moneda),
        new("Tasa de tabla", TipoDeColumna.Porcentaje),
        new("Margen", TipoDeColumna.Porcentaje),
        new("Regla"),
        new("Aprobador"),
        new("Costo frente a la tabla", TipoDeColumna.Moneda),
        new("_cuenta", Clave: "_cuenta"),
    ];

    public async Task<Result<TablaExportable>> Handle(NegotiatedRatesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var cuentas = await (await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Deposit, ct))
            .Where(a => a.DepositClass == DepositClass.TermDeposit).Select(a => new { a.Id, a.Number, a.PublicId, a.HolderPersonId }).ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var versiones = await db.MemberAccountTermsVersions.AsNoTracking()
            .Where(v => ids.Contains(v.MemberAccountId) && !v.IsDeleted && v.NegotiatedSpread != null && v.EffectiveValueDate >= desde && v.EffectiveValueDate <= hasta)
            .OrderBy(v => v.EffectiveValueDate).ThenBy(v => v.Id).ToListAsync(ct);
        var vIds = versiones.Select(v => v.Id).ToList();
        var interes = await db.TermDepositScheduleItems.AsNoTracking().Where(p => vIds.Contains(p.TermsVersionId) && !p.IsDeleted)
            .GroupBy(p => p.TermsVersionId).Select(g => new { g.Key, Total = g.Sum(p => p.Interest) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var reglaIds = versiones.Where(v => v.NegotiatedRateRuleId != null).Select(v => v.NegotiatedRateRuleId!.Value).Distinct().ToList();
        var reglas = await db.NegotiatedRateRules.AsNoTracking().Where(r => reglaIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Code, ct);
        var aprobadores = versiones.Where(v => v.NegotiationApprovedByUserId != null).Select(v => v.NegotiationApprovedByUserId!.Value).Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => aprobadores.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);
        var porId = cuentas.ToDictionary(c => c.Id);
        var personas = await LectorDeCuentas.PersonasAsync(db, cuentas.Select(c => c.HolderPersonId).Distinct(), ct);
        var filas = versiones.Select(v =>
        {
            var c = porId[v.MemberAccountId];
            var conTabla = Costo(v, interes.GetValueOrDefault(v.Id));
            return new FilaExportable([v.EffectiveValueDate, c.Number, personas.GetValueOrDefault(c.HolderPersonId)?.FullName, v.PrincipalAmount, v.TableAnnualRate * 100m,
                v.NegotiatedSpread * 100m, v.NegotiatedRateRuleId is { } r ? reglas.GetValueOrDefault(r) : null,
                v.NegotiationApprovedByUserId is { } u ? usuarios.GetValueOrDefault(u) : null, conTabla, c.PublicId.ToString()]);
        }).ToList();
        var totales = new FilaExportable(["Total", null, null, filas.Sum(r => (decimal)r.Valores[3]!), null, null, null, null, filas.Sum(r => (decimal)r.Valores[8]!), null]);
        return Result.Success(new TablaExportable("Tasas negociadas", $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd}", Columnas, filas, totales,
            ["El costo frente a la tabla es el interés del plan con la tasa negociada menos el que habría dado la tasa de la tabla con el mismo plan."]));
    }

    /// <summary>El interés del plan menos el de la tasa de la tabla en el mismo plazo (la proporción de las efectivas sobre el plan, al peso).</summary>
    private static decimal Costo(Domain.Entities.Lending.Deposits.Accounts.MemberAccountTermsVersion v, decimal interesDelPlan)
    {
        if (interesDelPlan == 0m || v.AppliedAnnualRate == 0m) return 0m;
        var c = TermDeposits.LectorDeCdat.Condiciones(v);
        var calendario = new Domain.Common.Calendar.CalendarioHabil([], []);
        var conTabla = Domain.Lending.Deposits.PlanDeCdat.Generar(c with { TasaEfectivaAnual = v.TableAnnualRate }, calendario).InteresTotal;
        return interesDelPlan - conTabla;
    }
}

/// <summary>
/// La vista <c>depositor-concentration</c> (api.md §18.1; FR-107): los depositantes con mayor saldo en captaciones al corte (<c>asOf</c>), su participación en el
/// total y el acumulado; <c>top</c> opcional (20 por defecto). Los aportes no entran: son patrimonio. (nuevo)
/// </summary>
public sealed record DepositorConcentrationReportQuery(FiltrosDeInformeDeCartera Filtros, int? Top = null) : IRequest<Result<TablaExportable>>;

public sealed class DepositorConcentrationReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<DepositorConcentrationReportQuery, Result<TablaExportable>>
{
    /// <summary>Cuántos depositantes muestra la vista sin <c>top</c>.</summary>
    public const int TopPorDefecto = 20;

    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "depositor-concentration", "Concentración de depositantes",
        "Los depositantes con mayor saldo en captaciones al corte, con su participación y el acumulado.", "concentracion-de-depositantes",
        ["asOf", "branch", "line"], ["top"], EntregaDeCartera.K8);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Titular"),
        new("Documento"),
        new("Saldo total", TipoDeColumna.Moneda),
        new("Participación", TipoDeColumna.Porcentaje),
        new("Acumulado", TipoDeColumna.Porcentaje),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(DepositorConcentrationReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var top = request.Top is > 0 ? request.Top.Value : TopPorDefecto;
        var cuentas = await (await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Deposit, ct))
            .Where(a => a.OpenedOn <= corte).Select(a => new { a.Id, a.HolderPersonId }).ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var saldos = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= corte
                        && (t.Concept == MemberAccountConcept.Principal || t.Concept == MemberAccountConcept.InterestPayable))
            .GroupBy(t => t.MemberAccountId).Select(g => new { Cuenta = g.Key, Saldo = g.Sum(t => t.Amount) }).ToListAsync(ct);
        var titular = cuentas.ToDictionary(c => c.Id, c => c.HolderPersonId);
        var porPersona = saldos.GroupBy(s => titular[s.Cuenta]).Select(g => new { Persona = g.Key, Saldo = g.Sum(s => s.Saldo) })
            .Where(x => x.Saldo > 0m).OrderByDescending(x => x.Saldo).ToList();
        var total = porPersona.Sum(x => x.Saldo);
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => porPersona.Select(x => x.Persona).Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var acumulado = 0m;
        var filas = new List<FilaExportable>();
        foreach (var x in porPersona.Take(top))
        {
            var participacion = total == 0m ? 0m : Math.Round(x.Saldo / total, 6, MidpointRounding.AwayFromZero);
            acumulado += participacion;
            var p = personas[x.Persona];
            filas.Add(new FilaExportable([ReferenciasDeGobierno.Nombre(p), p.TaxId, x.Saldo, participacion * 100m, acumulado * 100m, p.PublicId.ToString()]));
        }
        var totales = new FilaExportable(["Total de captaciones", null, total, null, null, null]);
        return Result.Success(new TablaExportable("Concentración de depositantes", $"Al {corte:yyyy-MM-dd}", Columnas, filas, totales,
            ["La participación es sobre el total de captaciones en el alcance de quien consulta; los aportes no entran (son patrimonio)."]));
    }
}
