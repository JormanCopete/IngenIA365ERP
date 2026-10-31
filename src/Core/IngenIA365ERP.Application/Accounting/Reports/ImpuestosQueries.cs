using FluentValidation;
using IngenIA365ERP.Application.Accounting.Taxes;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

// Informes tributarios (feature 009 E4, US10, FR-066): leen el libro por MovimientosContables —mismos filtros, mismo alcance de
// sucursal, la apertura fuera del período— y se quedan con las cuentas marcadas de impuesto. No calculan impuestos: suman lo que las
// líneas trajeron (valor y base gravable, que AccountLineRules ya exigió y comprobó al contabilizar).

/// <summary>
/// Las vistas por clase de impuesto: <c>withholdings</c> (retención en la fuente, de IVA y de ICA), <c>vat</c>, <c>ica</c> (el propio y su
/// retención) y <c>gmf</c>. Una fila por cuenta y tercero con base y valor de cada lado y el neto según la naturaleza de la cuenta.
/// </summary>
public sealed record TaxReportQuery(string Vista, FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class TaxReportQueryValidator : AbstractValidator<TaxReportQuery>
{
    public TaxReportQueryValidator()
    {
        RuleFor(x => x.Vista).Must(v => ImpuestosDeLaVista.Clases(v) is not null).WithMessage("Vista tributaria desconocida.");
        RuleFor(x => x.Filtros).NotNull();
    }
}

public static class ImpuestosDeLaVista
{
    public const string Retenciones = "withholdings";
    public const string Iva = "vat";
    public const string Ica = "ica";
    public const string Gmf = "gmf";

    public static IReadOnlyList<TaxKind>? Clases(string? vista) => vista switch
    {
        Retenciones => [TaxKind.Withholding, TaxKind.VatWithholding, TaxKind.IcaWithholding],
        Iva => [TaxKind.Vat],
        Ica => [TaxKind.Ica, TaxKind.IcaWithholding],
        Gmf => [TaxKind.Gmf],
        _ => null,
    };

    public static string Titulo(string vista) => vista switch
    {
        Retenciones => "Retenciones practicadas",
        Iva => "IVA generado y descontable",
        Ica => "Industria y comercio",
        _ => "Gravamen a los movimientos financieros",
    };
}

public sealed class TaxReportQueryHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<TaxReportQuery, Result<TablaExportable>>
{
    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Cuenta"), new("Nombre de la cuenta"), new("Clase"), new("Concepto"), new("Tercero"), new("Identificación"),
        new("Base débitos", TipoDeColumna.Moneda), new("Débitos", TipoDeColumna.Moneda),
        new("Base créditos", TipoDeColumna.Moneda), new("Créditos", TipoDeColumna.Moneda),
        new("Neto", TipoDeColumna.Moneda), new("Tarifa vigente %", TipoDeColumna.Porcentaje),
        new("Cuenta", Clave: "_cuenta"),
    ];

    public async Task<Result<TablaExportable>> Handle(TaxReportQuery request, CancellationToken ct)
    {
        var contexto = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (contexto.IsFailure) return Result.Failure<TablaExportable>(contexto.Error);
        var c = contexto.Value;
        var clases = ImpuestosDeLaVista.Clases(request.Vista)!.ToList();

        var grupos = await MovimientosContables.DelRango(MovimientosContables.Base(db, c), c)
            .Where(e => clases.Contains(e.Account!.TaxKind))
            .GroupBy(e => new { e.AccountId, e.PersonId })
            .Select(g => new
            {
                g.Key.AccountId, g.Key.PersonId,
                Debitos = g.Sum(e => e.Debit), Creditos = g.Sum(e => e.Credit),
                BaseDebitos = g.Sum(e => e.Debit > 0 ? e.TaxBase ?? 0m : 0m),
                BaseCreditos = g.Sum(e => e.Credit > 0 ? e.TaxBase ?? 0m : 0m),
            })
            .ToListAsync(ct);

        var idsDeCuenta = grupos.Select(g => g.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Include(a => a.TaxRates).Where(a => idsDeCuenta.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var idsDePersona = grupos.Where(g => g.PersonId != null).Select(g => g.PersonId!.Value).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => idsDePersona.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName), p.TaxId })
            .ToDictionaryAsync(p => p.Id, ct);

        var filas = new List<FilaExportable>();
        decimal totBaseD = 0, totD = 0, totBaseC = 0, totC = 0;
        foreach (var g in grupos.OrderBy(g => cuentas[g.AccountId].Code).ThenBy(g => g.PersonId is { } p && personas.TryGetValue(p, out var x) ? x.Nombre : string.Empty))
        {
            var cuenta = cuentas[g.AccountId];
            var persona = g.PersonId is { } id ? personas.GetValueOrDefault(id) : null;
            var neto = cuenta.Nature == AccountNature.Credit ? g.Creditos - g.Debitos : g.Debitos - g.Creditos;
            var tarifa = TarifaA(cuenta, c.Hasta);
            filas.Add(new FilaExportable(
            [
                cuenta.Code, cuenta.Name, ClasesDeImpuesto.Nombre(cuenta.TaxKind), cuenta.TaxConceptCode, persona?.Nombre ?? "(sin tercero)", persona?.TaxId,
                g.BaseDebitos, g.Debitos, g.BaseCreditos, g.Creditos, neto, tarifa is { } t ? t * 100m : null,
                cuenta.PublicId,
            ], Seccion: $"{cuenta.Code} {cuenta.Name}"));
            totBaseD += g.BaseDebitos; totD += g.Debitos; totBaseC += g.BaseCreditos; totC += g.Creditos;
        }
        var totales = new FilaExportable(["Total", null, null, null, null, null, totBaseD, totD, totBaseC, totC, null, null, null]);
        var notas = new List<string>(await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct))
        {
            "Sólo las cuentas marcadas como de impuesto en el plan (clase de impuesto en las reglas de la cuenta). Base = la base gravable que trae cada línea.",
            "Neto según la naturaleza de la cuenta: en un pasivo, créditos menos débitos (lo retenido o generado por pagar).",
        };
        var tabla = new TablaExportable(ImpuestosDeLaVista.Titulo(request.Vista), c.PeriodoTexto, Columnas, filas, totales, notas);
        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(request.Vista, new { filtros = request.Filtros }, request.Filtros.FormatoNormalizado, filas.Count, ct);
        return Result.Success(tabla);
    }

    /// <summary>La tarifa vigente de la cuenta a la fecha (fracción), o nula si no tiene.</summary>
    public static decimal? TarifaA(ChartOfAccount cuenta, DateOnly fecha) =>
        cuenta.TaxRates.Where(t => !t.IsDeleted && t.ValidFrom <= fecha).OrderByDescending(t => t.ValidFrom).Select(t => (decimal?)t.Rate).FirstOrDefault();
}

/// <summary>El resumen de un formulario por renglón (vista <c>tax-form</c>): cada renglón suma lo que su selector dice sobre sus prefijos, en el período.</summary>
public sealed record TaxFormSummaryQuery(string Code, FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class TaxFormSummaryQueryValidator : AbstractValidator<TaxFormSummaryQuery>
{
    public TaxFormSummaryQueryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Filtros).NotNull();
    }
}

public sealed class TaxFormSummaryQueryHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<TaxFormSummaryQuery, Result<TablaExportable>>
{
    public const string Vista = "tax-form";

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Renglón"), new("Descripción"), new("Suma"), new("Cuentas"), new("Valor", TipoDeColumna.Moneda),
    ];

    public async Task<Result<TablaExportable>> Handle(TaxFormSummaryQuery request, CancellationToken ct)
    {
        var codigo = request.Code.Trim().ToUpperInvariant();
        var formulario = await db.TaxForms.AsNoTracking().Include(f => f.Lines).FirstOrDefaultAsync(f => f.Code == codigo && !f.IsDeleted, ct);
        if (formulario is null) return Result.Failure<TablaExportable>(TaxErrors.FormNotFound);
        var contexto = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (contexto.IsFailure) return Result.Failure<TablaExportable>(contexto.Error);
        var c = contexto.Value;

        var renglones = formulario.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.Order).ToList();
        var todos = renglones.SelectMany(l => FormulariosTributarios.Prefijos(l.AccountPrefixes)).Distinct().ToList();
        // Una sola lectura agregada por cuenta; cada renglón suma después las cuentas de sus prefijos. Las cuentas se eligen en memoria
        // (el plan son unos miles de filas) porque «empieza por alguno de estos prefijos» no se traduce a SQL.
        var codigos = await db.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted).Select(a => new { a.Id, a.Code }).ToListAsync(ct);
        var ids = codigos.Where(a => todos.Any(p => a.Code.StartsWith(p, StringComparison.Ordinal))).Select(a => a.Id).ToList();
        var porCuenta = await MovimientosContables.DelRango(MovimientosContables.Base(db, c), c)
            .Where(e => ids.Contains(e.AccountId))
            .GroupBy(e => new { e.Account!.Code, e.Account.Nature })
            .Select(g => new SumaDeCuenta(g.Key.Code, g.Key.Nature,
                g.Sum(e => e.Debit), g.Sum(e => e.Credit),
                g.Sum(e => e.Debit > 0 ? e.TaxBase ?? 0m : 0m), g.Sum(e => e.Credit > 0 ? e.TaxBase ?? 0m : 0m)))
            .ToListAsync(ct);

        var filas = renglones.Select(l =>
        {
            var prefijos = FormulariosTributarios.Prefijos(l.AccountPrefixes);
            var valor = porCuenta.Where(s => prefijos.Any(p => s.Code.StartsWith(p, StringComparison.Ordinal))).Sum(s => Valor(s, l.Selector)) * l.Sign;
            return new FilaExportable([l.LineCode, l.Description, Selector(l.Selector), l.AccountPrefixes, valor]);
        }).ToList();
        var notas = new List<string>(await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct))
        {
            "Cada renglón suma, en el período, las cuentas cuyos códigos empiezan por sus prefijos. El formulario lo configura la empresa; los sugeridos están pendientes de validar con el contador.",
            "Los valores no van redondeados a miles: el redondeo lo hace quien diligencia el formulario.",
        };
        var tabla = new TablaExportable($"Formulario {formulario.Code} · {formulario.Name}", c.PeriodoTexto, Columnas, filas, null, notas);
        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync($"{Vista}/{formulario.Code}", new { filtros = request.Filtros }, request.Filtros.FormatoNormalizado, filas.Count, ct);
        return Result.Success(tabla);
    }

    private sealed record SumaDeCuenta(string Code, AccountNature Nature, decimal Debitos, decimal Creditos, decimal BaseDebitos, decimal BaseCreditos);

    private static decimal Valor(SumaDeCuenta s, TaxFormSelector selector) => selector switch
    {
        TaxFormSelector.Debit => s.Debitos,
        TaxFormSelector.Credit => s.Creditos,
        TaxFormSelector.BaseDebit => s.BaseDebitos,
        TaxFormSelector.BaseCredit => s.BaseCreditos,
        // La base neta va con el lado de la naturaleza: en una retención (pasivo), la de lo retenido menos la de lo reversado.
        TaxFormSelector.Base => s.Nature == AccountNature.Credit ? s.BaseCreditos - s.BaseDebitos : s.BaseDebitos - s.BaseCreditos,
        _ => s.Nature == AccountNature.Credit ? s.Creditos - s.Debitos : s.Debitos - s.Creditos,
    };

    private static string Selector(TaxFormSelector s) => s switch
    {
        TaxFormSelector.Base => "Base neta",
        TaxFormSelector.Amount => "Valor neto",
        TaxFormSelector.Debit => "Débitos",
        TaxFormSelector.Credit => "Créditos",
        TaxFormSelector.BaseDebit => "Base de los débitos",
        TaxFormSelector.BaseCredit => "Base de los créditos",
        _ => s.ToString(),
    };
}
