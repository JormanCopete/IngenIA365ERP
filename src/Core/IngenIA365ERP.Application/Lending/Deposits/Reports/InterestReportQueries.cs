using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Reports;

/// <summary>
/// La vista <c>interest-and-withholding</c> (feature 014, US5, T137; api.md §18.1; FR-107): las liquidaciones de intereses de las cuentas de captación con
/// fin de período en el rango (<c>from</c>, <c>to</c>) y clase opcional (<c>class</c>): la cuenta, el titular, el interés bruto, el componente
/// inflacionario, la base, la retención y el neto, sin las reversadas. Con datos del titular (consultar con <c>Lending.Accounts.Read</c>, exportar con
/// <c>ExportPersonalData</c>, como las demás vistas de captaciones con titular). (nuevo)
/// </summary>
public sealed record InterestAndWithholdingReportQuery(FiltrosDeInformeDeCartera Filtros, string? Class = null) : IRequest<Result<TablaExportable>>;

public sealed class InterestAndWithholdingReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<InterestAndWithholdingReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "interest-and-withholding", "Intereses y retención",
        "Las liquidaciones de intereses de las cuentas de captación en un rango: interés, componente inflacionario, base, retención y neto, por cuenta.",
        "intereses-y-retencion", ["from", "to", "branch", "line"], ["class"], EntregaDeCartera.K8);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Cuenta"),
        new("Titular"),
        new("Documento"),
        new("Período"),
        new("Interés", TipoDeColumna.Moneda),
        new("Componente inflacionario", TipoDeColumna.Moneda),
        new("Base", TipoDeColumna.Moneda),
        new("Retención", TipoDeColumna.Moneda),
        new("Neto", TipoDeColumna.Moneda),
        new("_cuenta", Clave: "_cuenta"),
    ];

    public async Task<Result<TablaExportable>> Handle(InterestAndWithholdingReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        DepositClass? clase = null;
        if (!string.IsNullOrWhiteSpace(request.Class))
        {
            if (!Enum.TryParse<DepositClass>(request.Class, true, out var c) || !Enum.IsDefined(c))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("class", request.Class, Enum.GetNames<DepositClass>()));
            clase = c;
        }
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        var cuentas = await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Deposit, ct);
        if (clase is { } k) cuentas = cuentas.Where(a => a.DepositClass == k);
        var filas = await (from s in db.InterestSettlements.AsNoTracking()
                           join a in cuentas on s.MemberAccountId equals a.Id
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.HolderPersonId equals p.Id
                           where !s.IsDeleted && s.ReversedByOperationId == null && s.PeriodTo >= desde && s.PeriodTo <= hasta
                           orderby a.Number, s.PeriodTo
                           select new { a.Number, a.PublicId, Persona = p, s.PeriodFrom, s.PeriodTo, s.GrossInterest, s.InflationaryComponentAmount, s.WithholdingBase,
                               s.WithholdingAmount, s.NetInterest })
            .ToListAsync(ct);
        var tabla = filas.Select(x => new FilaExportable([x.Number, ReferenciasDeGobierno.Nombre(x.Persona), x.Persona.TaxId,
            $"{x.PeriodFrom:yyyy-MM-dd} a {x.PeriodTo:yyyy-MM-dd}", x.GrossInterest, x.InflationaryComponentAmount ?? 0m, x.WithholdingBase, x.WithholdingAmount,
            x.NetInterest, x.PublicId.ToString()])).ToList();
        var totales = new FilaExportable(["Total", null, null, null, filas.Sum(x => x.GrossInterest), filas.Sum(x => x.InflationaryComponentAmount ?? 0m),
            filas.Sum(x => x.WithholdingBase), filas.Sum(x => x.WithholdingAmount), filas.Sum(x => x.NetInterest), null]);
        return Result.Success(new TablaExportable("Intereses y retención", $"{desde:yyyy-MM-dd} a {hasta:yyyy-MM-dd}", Columnas, tabla, totales,
            ["La retención se practica antes de capitalizar: el neto es lo que se abonó al capital. Sin las liquidaciones reversadas."]));
    }
}
