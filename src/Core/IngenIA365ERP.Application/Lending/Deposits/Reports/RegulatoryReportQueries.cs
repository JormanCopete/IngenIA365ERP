using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Application.Lending.Regulatory.Captaciones;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Reports;

// Feature 014, US17 (T219; api.md §18.1; FR-107, FR-111, FR-112; research §25): las vistas que faltaban del centro de informes de captaciones —las cuentas
// por estado y la vista previa de cada formato regulatorio (SICSES de captaciones, cotitulares, aportes, saldos diarios y fondo de liquidez; exógena 1019 y
// 1020)—. La vista previa es la proyección del formato tal como la escribiría el archivo (los mismos textos, campo a campo), con sus inconsistencias y
// advertencias en las notas; el archivo se genera en /api/lending/regulatory/deposit-formats. (nuevo)

/// <summary>
/// La vista <c>deposit-accounts-by-status</c> (api.md §18.1): las cuentas de captación y de aportes abiertas al corte <c>asOf</c> con su estado
/// (<c>status</c> opcional), su saldo del libro con fecha valor hasta el corte y sus marcas (embargo, exención de GMF, causación suspendida, datos por
/// actualizar). El estado es el de hoy: el ERP no guarda el estado de cada día. (nuevo)
/// </summary>
public sealed record DepositAccountsByStatusReportQuery(FiltrosDeInformeDeCartera Filtros, string? Status = null) : IRequest<Result<TablaExportable>>;

public sealed class DepositAccountsByStatusReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<DepositAccountsByStatusReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "deposit-accounts-by-status", "Cuentas por estado",
        "Las cuentas de captación y de aportes al corte con su estado, su saldo y sus marcas (embargo, exención de GMF, causación suspendida).",
        "cuentas-por-estado", ["asOf", "branch", "line"], ["status"], EntregaDeCartera.K7);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Cuenta"),
        new("Titular"),
        new("Documento"),
        new("Línea"),
        new("Oficina"),
        new("Estado"),
        new("Saldo", TipoDeColumna.Moneda),
        new("Marcas"),
        new("_cuenta", Clave: "_cuenta"),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(DepositAccountsByStatusReportQuery request, CancellationToken ct)
    {
        MemberAccountStatus? estado = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<MemberAccountStatus>(request.Status, true, out var e) || !Enum.IsDefined(e))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("status", request.Status, Enum.GetNames<MemberAccountStatus>()));
            estado = e;
        }
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var deposito = await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Deposit, ct);
        var aportes = await VistasDeCaptaciones.CuentasAsync(db, alcanceDeLaPeticion, f, LineNature.Contribution, ct);
        var consulta = deposito.Concat(aportes).Where(a => a.OpenedOn <= corte && (a.ClosedOn == null || a.ClosedOn >= corte));
        if (estado is { } s) consulta = consulta.Where(a => a.Status == s);
        var cuentas = await consulta.OrderBy(a => a.Number).ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var delTitular = MemberAccountTransactionConceptos.DelTitular;
        var saldos = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= corte && delTitular.Contains(t.Concept))
            .GroupBy(t => t.MemberAccountId).Select(g => new { g.Key, Total = g.Sum(t => t.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var exentas = (await db.GmfExemptAccounts.AsNoTracking()
            .Where(g => ids.Contains(g.MemberAccountId) && !g.IsDeleted && g.ValidFrom <= corte && (g.ValidTo == null || g.ValidTo >= corte))
            .Select(g => g.MemberAccountId).ToListAsync(ct)).ToHashSet();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => cuentas.Select(c => c.CreditLineId).Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => $"{l.Code} {l.Name}", ct);
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => cuentas.Select(c => c.BranchId).Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var personas = await LectorDeCuentas.PersonasAsync(db, cuentas.Select(c => c.HolderPersonId), ct);

        var filas = cuentas.Select(c =>
        {
            var marcas = new List<string>();
            if (c.IsGarnished) marcas.Add("Embargada");
            if (exentas.Contains(c.Id)) marcas.Add("Exenta de GMF");
            if (c.AccrualSuspendedSince is not null && c.AccrualResumedOn is null) marcas.Add("Causación suspendida");
            if (c.RequiresDataUpdate) marcas.Add("Datos por actualizar");
            var p = personas.GetValueOrDefault(c.HolderPersonId);
            return new FilaExportable([c.Number, p?.FullName, p?.DocumentNumber, lineas.GetValueOrDefault(c.CreditLineId), oficinas.GetValueOrDefault(c.BranchId),
                Estado(c.Status), saldos.GetValueOrDefault(c.Id), string.Join(", ", marcas), c.PublicId.ToString(), p?.PersonPublicId.ToString()]);
        }).ToList();
        var porEstado = cuentas.GroupBy(c => c.Status).OrderBy(g => g.Key).Select(g => $"{Estado(g.Key)}: {g.Count()}");
        var totales = new FilaExportable(["Total", null, null, null, null, $"{filas.Count} cuentas", filas.Sum(r => (decimal)r.Valores[6]!), null, null, null]);
        return Result.Success(new TablaExportable("Cuentas por estado", $"Al {corte:yyyy-MM-dd}", Columnas, filas, totales,
        [
            "Saldo del titular: la suma del libro con fecha valor hasta el corte (capital, interés por pagar y bonificación; en aportes, las cuatro clases).",
            "El estado es el vigente hoy: el ERP no guarda el estado de cada día. " + string.Join(" · ", porEstado),
        ]));
    }

    public static string Estado(MemberAccountStatus estado) => estado switch
    {
        MemberAccountStatus.Opening => "En apertura",
        MemberAccountStatus.Active => "Activa",
        MemberAccountStatus.Inactive => "Inactiva",
        MemberAccountStatus.Matured => "Vencida",
        MemberAccountStatus.Closing => "En cancelación",
        MemberAccountStatus.Closed => "Cancelada",
        _ => estado.ToString(),
    };
}

/// <summary>Los conceptos del libro que son del titular (sin cargos por cobrar ni no reclamados). (nuevo)</summary>
internal static class MemberAccountTransactionConceptos
{
    public static readonly MemberAccountConcept[] DelTitular =
    [
        MemberAccountConcept.Principal, MemberAccountConcept.InterestPayable, MemberAccountConcept.Bonus, MemberAccountConcept.ContributionMinimum,
        MemberAccountConcept.ContributionOrdinary, MemberAccountConcept.ContributionExtraordinary, MemberAccountConcept.ContributionRevaluation,
    ];
}

/// <summary>
/// La vista previa de un formato regulatorio de captaciones (api.md §18.1: <c>sicses-captaciones</c>, <c>sicses-cotitulares</c>, <c>sicses-aportes</c>,
/// <c>sicses-saldos-diarios</c>, <c>sicses-fondo-liquidez</c>, <c>exogena-1019</c>, <c>exogena-1020</c>): las filas del detalle con una columna por campo del
/// layout vigente, con el texto que iría en el archivo; el encabezado (fondo de liquidez), las advertencias y las inconsistencias, en las notas. Período por
/// <c>year</c> y <c>month</c> (el mes anterior si no vienen; en la exógena, el año anterior). Exige alcance total: el formato es de toda la cooperativa. (nuevo)
/// </summary>
public sealed record DepositRegulatoryPreviewReportQuery(FiltrosDeInformeDeCartera Filtros, string FormatCode, int? Year = null, int? Month = null)
    : IRequest<Result<TablaExportable>>;

public sealed class DepositRegulatoryPreviewReportQueryHandler(PreparacionDeArchivoRegulatorio preparacion, IAlcanceDeCartera alcance, IDateTimeService reloj)
    : IRequestHandler<DepositRegulatoryPreviewReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera SicsesCaptaciones = Vista(FormatosDeCaptaciones.Captaciones, "sicses-captaciones");
    public static readonly VistaDeInformeDeCartera SicsesCotitulares = Vista(FormatosDeCaptaciones.Cotitulares, "sicses-cotitulares");
    public static readonly VistaDeInformeDeCartera SicsesAportes = Vista(FormatosDeCaptaciones.Aportes, "sicses-aportes");
    public static readonly VistaDeInformeDeCartera SicsesSaldosDiarios = Vista(FormatosDeCaptaciones.SaldosDiarios, "sicses-saldos-diarios");
    public static readonly VistaDeInformeDeCartera SicsesFondoDeLiquidez = Vista(FormatosDeCaptaciones.FondoDeLiquidez, "sicses-fondo-liquidez");
    public static readonly VistaDeInformeDeCartera Exogena1019 = Vista(FormatosDeCaptaciones.Exogena1019, "exogena-1019");
    public static readonly VistaDeInformeDeCartera Exogena1020 = Vista(FormatosDeCaptaciones.Exogena1020, "exogena-1020");

    /// <summary>Las siete vistas previas, en el orden del registro.</summary>
    public static IReadOnlyList<VistaDeInformeDeCartera> Todas { get; } =
        [SicsesCaptaciones, SicsesCotitulares, SicsesAportes, SicsesSaldosDiarios, SicsesFondoDeLiquidez, Exogena1019, Exogena1020];

    private static VistaDeInformeDeCartera Vista(string codigo, string archivo)
    {
        var f = FormatosDeCaptaciones.Buscar(codigo)!;
        IReadOnlyList<string> propios = f.Anual ? ["year"] : ["year", "month"];
        return f.ConDatosDelTitular
            ? VistasDeCaptaciones.ConDatosDelTitular(codigo, f.Nombre, f.Descripcion + " Vista previa del archivo regulatorio.", archivo, [], propios, EntregaDeCartera.K11)
            : new VistaDeInformeDeCartera(codigo, f.Nombre, f.Descripcion + " Vista previa del archivo regulatorio.", archivo, [], propios,
                AvailableFrom: EntregaDeCartera.K11);
    }

    public async Task<Result<TablaExportable>> Handle(DepositRegulatoryPreviewReportQuery request, CancellationToken ct)
    {
        var formato = FormatosDeCaptaciones.Buscar(request.FormatCode);
        if (formato is null) return Result.Failure<TablaExportable>(ErroresDeFormatosDeCaptaciones.FormatoDesconocido(request.FormatCode));
        if (await AlcanceDeFormatosDeCaptaciones.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<TablaExportable>(fuera);
        if (request.Month is { } m && m is < 1 or > 12)
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("month", m.ToString(CultureInfo.InvariantCulture),
                Enumerable.Range(1, 12).Select(n => n.ToString(CultureInfo.InvariantCulture))));
        var anterior = reloj.HoyLocal.AddMonths(-1);
        var anio = request.Year ?? (formato.Anual ? reloj.HoyLocal.Year - 1 : anterior.Year);
        int? mes = formato.Anual ? null : request.Month ?? anterior.Month;

        var p = await preparacion.CaptacionesAsync(formato.Codigo, anio, mes, ct);
        if (p.IsFailure) return Result.Failure<TablaExportable>(p.Error);
        return Result.Success(Tabla(formato, p.Value));
    }

    /// <summary>La tabla de la vista previa: una columna por campo del detalle, con el texto del archivo (o el problema del campo).</summary>
    public static TablaExportable Tabla(FormatoDeCaptaciones formato, ArchivoRegulatorioPreparado p)
    {
        var layout = p.Layout.Layout;
        var detalle = layout.Registro(FilasDeCaptaciones.Detalle)!;
        var campos = detalle.EnOrden(layout.FileKind).ToList();
        var columnas = campos.Select(c => new ColumnaExportable(c.Name)).ToList();
        string Texto(CampoDeLayout campo, FilaDeLayout fila)
        {
            var r = FormateadorDeCampo.Crudo(layout, campo, fila);
            return r.Error is { } e ? $"¡{e.Mensaje}" : r.Texto ?? string.Empty;
        }
        var filas = p.Filas.Where(f => f.Registro == FilasDeCaptaciones.Detalle)
            .Select(f => new FilaExportable(campos.Select(c => (object?)Texto(c, f)).ToList())).ToList();

        var notas = new List<string>();
        foreach (var otra in p.Filas.Where(f => f.Registro != FilasDeCaptaciones.Detalle))
        {
            var registro = layout.Registro(otra.Registro);
            if (registro is null) continue;
            notas.Add($"{otra.Registro}: " + string.Join(" · ", registro.EnOrden(layout.FileKind).Select(c => $"{c.Name} = {Texto(c, otra)}")));
        }
        notas.Add($"Layout {p.Layout.Code} (versión {layout.Version}, vigente desde {layout.ValidFrom:yyyy-MM-dd}): {layout.VerifiedFields} de {layout.TotalFields} campos "
                  + $"cotejados. Fuente: {layout.Source}.");
        notas.AddRange(p.Advertencias.Select(a => a.Message));
        if (p.Bloqueantes > 0)
            notas.Add($"El archivo tiene {p.Bloqueantes} inconsistencias que impiden generarlo: "
                      + string.Join(" | ", p.Escritura.Inconsistencias.Where(i => i.Severidad == SeveridadDeInconsistencia.Blocking).Take(20).Select(i => i.Mensaje)));
        if (p.Plazo is { } plazo) notas.Add($"Plazo informativo: a más tardar el {plazo:yyyy-MM-dd} (la entidad decide cuándo reporta).");
        notas.Add("El archivo se genera, se descarga y se marca cargado en Captaciones y aportes › Reportes regulatorios.");
        var periodo = formato.Anual ? $"Año {p.Anio}" : string.Create(CultureInfo.InvariantCulture, $"{p.Anio:0000}-{p.Mes:00}");
        return new TablaExportable(formato.Nombre, $"{periodo} · {filas.Count} filas", columnas, filas, null, notas);
    }
}
