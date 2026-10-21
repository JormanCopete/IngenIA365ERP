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

// Feature 014, US4 (T121; api.md §18.1; research R24; FR-107): las vistas de captaciones del centro de informes de la Cartera —saldos agregados, movimientos
// del libro, uso del GMF y cargos—. Los saldos no traen datos del titular; las demás sí (Lending.Accounts.Read para consultar, ExportPersonalData para
// exportar). Las cuentas se filtran por el alcance de oficina y línea de quien pregunta. (nuevo)

/// <summary>
/// La vista <c>deposit-balances</c> (api.md §18.1): el saldo de las cuentas de captación al corte <c>asOf</c> agrupado por línea, oficina o clase
/// (<c>groupBy</c>), con el número de cuentas, el capital, el interés por pagar y el total. La suma del libro con fecha valor ≤ corte. (nuevo)
/// </summary>
public sealed record DepositBalancesReportQuery(FiltrosDeInformeDeCartera Filtros, string? GroupBy = null) : IRequest<Result<TablaExportable>>;

public sealed class DepositBalancesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<DepositBalancesReportQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<string> Agrupaciones = ["line", "office", "class"];

    public static readonly VistaDeInformeDeCartera Vista = new("deposit-balances", "Saldos de captaciones",
        "El saldo de las cuentas de captación al corte, por línea, oficina o clase, sin datos de titulares.", "saldos-de-captaciones",
        ["asOf", "branch", "line"], ["groupBy"], AvailableFrom: EntregaDeCartera.K7);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Dimensión"),
        new("Cuentas", TipoDeColumna.Entero),
        new("Capital", TipoDeColumna.Moneda),
        new("Interés por pagar", TipoDeColumna.Moneda),
        new("Total", TipoDeColumna.Moneda),
    ];

    public async Task<Result<TablaExportable>> Handle(DepositBalancesReportQuery request, CancellationToken ct)
    {
        var agrupar = FiltrosDeInformeDeCartera.Elegir("groupBy", request.GroupBy, Agrupaciones, Agrupaciones[0]);
        if (agrupar.IsFailure) return Result.Failure<TablaExportable>(agrupar.Error);
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var cuentas = await (await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Deposit, ct))
            .Where(a => a.OpenedOn <= corte).Select(a => new { a.Id, a.CreditLineId, a.BranchId, a.DepositClass }).ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var saldos = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= corte
                        && (t.Concept == MemberAccountConcept.Principal || t.Concept == MemberAccountConcept.InterestPayable))
            .GroupBy(t => new { t.MemberAccountId, t.Concept }).Select(g => new { g.Key.MemberAccountId, g.Key.Concept, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => cuentas.Select(c => c.CreditLineId).Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => $"{l.Code} {l.Name}", ct);
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => cuentas.Select(c => c.BranchId).Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        string Dimension(int linea, int oficina, DepositClass? clase) => agrupar.Value switch
        {
            "office" => oficinas.GetValueOrDefault(oficina) ?? string.Empty,
            "class" => clase?.ToString() ?? string.Empty,
            _ => lineas.GetValueOrDefault(linea) ?? string.Empty,
        };
        var filas = cuentas.GroupBy(c => Dimension(c.CreditLineId, c.BranchId, c.DepositClass)).Select(g =>
        {
            var deEllas = g.Select(c => c.Id).ToHashSet();
            var capital = saldos.Where(s => deEllas.Contains(s.MemberAccountId) && s.Concept == MemberAccountConcept.Principal).Sum(s => s.Total);
            var interes = saldos.Where(s => deEllas.Contains(s.MemberAccountId) && s.Concept == MemberAccountConcept.InterestPayable).Sum(s => s.Total);
            return new FilaExportable([g.Key, g.Count(), capital, interes, capital + interes]);
        }).OrderBy(r => (string)r.Valores[0]!, StringComparer.CurrentCulture).ToList();
        var totales = new FilaExportable(["Total", filas.Sum(r => (int)r.Valores[1]!), filas.Sum(r => (decimal)r.Valores[2]!),
            filas.Sum(r => (decimal)r.Valores[3]!), filas.Sum(r => (decimal)r.Valores[4]!)]);
        return Result.Success(new TablaExportable("Saldos de captaciones", $"Al {corte:yyyy-MM-dd} · por {agrupar.Value}", Columnas, filas, totales,
            ["Suma del libro de cuentas con fecha valor hasta el corte (capital e interés por pagar); los cargos por cobrar no restan aquí."]));
    }
}

/// <summary>
/// La vista <c>deposit-transactions</c> (api.md §18.1): las filas del libro de las cuentas de captación y aportes en un rango, por fecha valor, contable
/// o de registro (<c>dateKind</c>), con <c>account</c> (número de cuenta) y <c>kind</c> (clase de la fila) opcionales. (nuevo)
/// </summary>
public sealed record DepositTransactionsReportQuery(FiltrosDeInformeDeCartera Filtros, string? Account = null, string? Kind = null, string? DateKind = null)
    : IRequest<Result<TablaExportable>>;

public sealed class DepositTransactionsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<DepositTransactionsReportQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<string> TiposDeFecha = ["value", "accounting", "registered"];

    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "deposit-transactions", "Movimientos de cuentas",
        "Las filas del libro de las cuentas de captación y aportes en un rango, por fecha valor, contable o de registro.", "movimientos-de-cuentas",
        ["from", "to", "branch", "line"], ["account", "kind", "dateKind"], EntregaDeCartera.K7);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha valor", TipoDeColumna.Fecha),
        new("Fecha contable", TipoDeColumna.Fecha),
        new("Cuenta"),
        new("Titular"),
        new("Tipo"),
        new("Concepto"),
        new("Valor", TipoDeColumna.Moneda),
        new("Operación"),
        new("_cuenta", Clave: "_cuenta"),
        new("_operacion", Clave: "_operacion"),
        new("_mensaje", Clave: "_mensaje"),
    ];

    public async Task<Result<TablaExportable>> Handle(DepositTransactionsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        var tipoDeFecha = FiltrosDeInformeDeCartera.Elegir("dateKind", request.DateKind, TiposDeFecha, TiposDeFecha[0]);
        if (tipoDeFecha.IsFailure) return Result.Failure<TablaExportable>(tipoDeFecha.Error);
        MemberAccountTransactionKind? clase = null;
        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            if (!Enum.TryParse<MemberAccountTransactionKind>(request.Kind, true, out var c) || !Enum.IsDefined(c))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("kind", request.Kind, Enum.GetNames<MemberAccountTransactionKind>()));
            clase = c;
        }
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var deposito = await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Deposit, ct);
        var aportes = await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Contribution, ct);
        var cuentas = deposito.Concat(aportes);
        if (!string.IsNullOrWhiteSpace(request.Account)) cuentas = cuentas.Where(a => a.Number == request.Account.Trim());
        var filtradas = await cuentas.Select(a => new { a.Id, a.Number, a.PublicId, a.HolderPersonId }).ToListAsync(ct);
        var ids = filtradas.Select(a => a.Id).ToList();
        var consulta = db.MemberAccountTransactions.AsNoTracking().Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted);
        if (clase is { } k) consulta = consulta.Where(t => t.Kind == k);
        var (inicio, fin) = (desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        consulta = tipoDeFecha.Value switch
        {
            "accounting" => consulta.Where(t => t.AccountingDate >= desde && t.AccountingDate <= hasta),
            "registered" => consulta.Where(t => t.RegisteredAt >= inicio && t.RegisteredAt < fin),
            _ => consulta.Where(t => t.ValueDate >= desde && t.ValueDate <= hasta),
        };
        var movimientos = await consulta.OrderBy(t => t.ValueDate).ThenBy(t => t.Id).ToListAsync(ct);
        var idsDeOperacion = movimientos.Where(m => m.OperationId is not null).Select(m => m.OperationId!.Value).Distinct().ToList();
        var operaciones = await db.MemberAccountOperations.AsNoTracking().Where(o => idsDeOperacion.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => new { o.Number, o.PublicId }, ct);
        var porId = filtradas.ToDictionary(a => a.Id);
        var personas = await LectorDeCuentas.PersonasAsync(db, filtradas.Select(a => a.HolderPersonId).Distinct(), ct);
        var filas = movimientos.Select(m =>
        {
            var cuenta = porId[m.MemberAccountId];
            var operacion = m.OperationId is { } o ? operaciones.GetValueOrDefault(o) : null;
            return new FilaExportable([m.ValueDate, m.AccountingDate, cuenta.Number, personas.GetValueOrDefault(cuenta.HolderPersonId)?.FullName, m.Kind.ToString(),
                m.Concept.ToString(), m.Amount, operacion?.Number, cuenta.PublicId.ToString(), operacion?.PublicId.ToString(), m.MessagePublicId?.ToString()]);
        }).ToList();
        return Result.Success(new TablaExportable("Movimientos de cuentas", $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd} · fecha {tipoDeFecha.Value}",
            Columnas, filas, null,
            ["Signo del libro: positivo, la Cartera debe más al titular; las filas de retención y GMF van en pareja con la del capital."]));
    }
}

/// <summary>
/// La vista <c>gmf</c> (api.md §18.1; FR-129): el uso del GMF de cada persona en un mes (<c>year</c>, <c>month</c>; el mes en curso si no vienen): lo
/// retirado, lo exento usado, lo gravado y el GMF cobrado, con la cuenta exenta. (nuevo)
/// </summary>
public sealed record GmfReportQuery(FiltrosDeInformeDeCartera Filtros, int? Year = null, int? Month = null) : IRequest<Result<TablaExportable>>;

public sealed class GmfReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<GmfReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "gmf", "GMF del mes",
        "El uso del tope exento del GMF y el GMF cobrado de cada persona en un mes.", "gmf",
        ["branch", "line"], ["year", "month"], EntregaDeCartera.K8);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Persona"),
        new("Documento"),
        new("Cuenta exenta"),
        new("Retirado", TipoDeColumna.Moneda),
        new("Exento usado", TipoDeColumna.Moneda),
        new("Gravado", TipoDeColumna.Moneda),
        new("GMF", TipoDeColumna.Moneda),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(GmfReportQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var anio = request.Year ?? hoy.Year;
        var mes = request.Month ?? hoy.Month;
        if (mes is < 1 or > 12)
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("month", mes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Enumerable.Range(1, 12).Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        var cuentas = (await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, request.Filtros, LineNature.Deposit, ct)).Select(a => a.Id);
        var usos = await db.GmfMonthlyUsages.AsNoTracking()
            .Where(u => u.Year == anio && u.Month == mes && !u.IsDeleted && cuentas.Contains(u.MemberAccountId))
            .GroupBy(u => u.PersonId)
            .Select(g => new { Persona = g.Key, Retirado = g.Sum(u => u.DisposedAmount), Exento = g.Sum(u => u.ExemptAmount), Gravado = g.Sum(u => u.TaxableBase),
                Gmf = g.Sum(u => u.GmfAmount) })
            .ToListAsync(ct);
        var ids = usos.Select(u => u.Persona).ToList();
        var fin = new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes));
        var exentas = await (from e in db.GmfExemptAccounts.AsNoTracking()
                             join a in db.MemberAccounts.AsNoTracking() on e.MemberAccountId equals a.Id
                             where ids.Contains(a.HolderPersonId) && !e.IsDeleted && e.ValidFrom <= fin && (e.ValidTo == null || e.ValidTo >= fin)
                             select new { a.HolderPersonId, a.Number }).ToListAsync(ct);
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var filas = usos.Select(u =>
        {
            var p = personas[u.Persona];
            return new FilaExportable([ReferenciasDeGobierno.Nombre(p), p.TaxId, exentas.FirstOrDefault(e => e.HolderPersonId == u.Persona)?.Number, u.Retirado,
                u.Exento, u.Gravado, u.Gmf, p.PublicId.ToString()]);
        }).OrderBy(r => (string)r.Valores[0]!, StringComparer.CurrentCulture).ToList();
        var totales = new FilaExportable(["Total", null, null, filas.Sum(r => (decimal)r.Valores[3]!), filas.Sum(r => (decimal)r.Valores[4]!),
            filas.Sum(r => (decimal)r.Valores[5]!), filas.Sum(r => (decimal)r.Valores[6]!), null]);
        return Result.Success(new TablaExportable("GMF del mes", $"{anio:0000}-{mes:00}", Columnas, filas, totales,
            ["Lo exento cuenta contra el tope mensual de la persona en UVT; un reverso devuelve el uso al mes de su original."]));
    }
}

/// <summary>
/// La vista <c>charges</c> (api.md §18.1; FR-128): los cargos causados en un rango (por la fecha valor de la operación que los causó), con <c>status</c>
/// opcional y el estado de la factura que informa Comercial. (nuevo)
/// </summary>
public sealed record ChargesReportQuery(FiltrosDeInformeDeCartera Filtros, string? Status = null) : IRequest<Result<TablaExportable>>;

public sealed class ChargesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ChargesReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "charges", "Cargos de captaciones",
        "Los cargos causados a las cuentas de captación en un rango, con su estado y el de su factura.", "cargos-de-captaciones",
        ["from", "to", "branch", "line"], ["status"], EntregaDeCartera.K8);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha),
        new("Cuenta"),
        new("Titular"),
        new("Cargo"),
        new("Valor", TipoDeColumna.Moneda),
        new("IVA"),
        new("Estado"),
        new("Factura"),
        new("_cuenta", Clave: "_cuenta"),
    ];

    public async Task<Result<TablaExportable>> Handle(ChargesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        AccountChargeStatus? estado = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AccountChargeStatus>(request.Status, true, out var e) || !Enum.IsDefined(e))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("status", request.Status, Enum.GetNames<AccountChargeStatus>()));
            estado = e;
        }
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var cuentas = await (await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Deposit, ct))
            .Select(a => new { a.Id, a.Number, a.PublicId, a.HolderPersonId }).ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var cargos = await (from c in db.AccountCharges.AsNoTracking()
                            join o in db.MemberAccountOperations.AsNoTracking() on c.AppliedOperationId equals o.Id
                            where ids.Contains(c.MemberAccountId) && !c.IsDeleted && o.ValueDate >= desde && o.ValueDate <= hasta
                                  && (estado == null || c.Status == estado)
                            orderby o.ValueDate, c.Id
                            select new { o.ValueDate, c.MemberAccountId, c.ChargeCode, c.Amount, c.IsVatTaxed, c.Status, c.CommercialInvoiceNumber,
                                c.CommercialInvoiceStatus }).ToListAsync(ct);
        var porId = cuentas.ToDictionary(c => c.Id);
        var personas = await LectorDeCuentas.PersonasAsync(db, cuentas.Select(c => c.HolderPersonId).Distinct(), ct);
        var filas = cargos.Select(c =>
        {
            var cuenta = porId[c.MemberAccountId];
            return new FilaExportable([c.ValueDate, cuenta.Number, personas.GetValueOrDefault(cuenta.HolderPersonId)?.FullName, c.ChargeCode, c.Amount,
                c.IsVatTaxed ? "Sí" : "No", c.Status.ToString(),
                c.CommercialInvoiceNumber is null ? c.CommercialInvoiceStatus : $"{c.CommercialInvoiceNumber} ({c.CommercialInvoiceStatus})", cuenta.PublicId.ToString()]);
        }).ToList();
        var totales = new FilaExportable(["Total", null, null, null, filas.Sum(r => (decimal)r.Valores[4]!), null, null, null, null]);
        return Result.Success(new TablaExportable("Cargos de captaciones", $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd}", Columnas, filas, totales,
            ["El IVA de un cargo gravado lo factura Comercial; el estado de la factura es informativo y no cambia el cargo."]));
    }
}
